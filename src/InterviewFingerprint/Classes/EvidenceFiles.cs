namespace InterviewFingerprint.Classes;

/// <summary>
/// Enumerates evidence files without traversing symbolic links or junctions.
/// Manifest paths are canonical, relative and separated with forward slashes.
/// </summary>
internal static class EvidenceFiles
{
    internal static IReadOnlyList<String> Enumerate(String root)
    {
        String fullRoot = Path.GetFullPath(root);
        RejectLinkedAncestors(fullRoot);

        List<String> files = new List<String>();
        Stack<String> pending = new Stack<String>();
        pending.Push(fullRoot);
        while (pending.Count > 0)
        {
            String directory = pending.Pop();
            foreach (String entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Evidence contains a symbolic link or junction: " + entry);
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
                else
                {
                    files.Add(entry);
                }
            }
        }

        // A Unix directory can contain names differing only by case, while
        // a typical Windows evidence directory cannot represent both.
        HashSet<String> portablePaths = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
        foreach (String file in files)
        {
            if (!portablePaths.Add(ToManifestPath(fullRoot, file)))
            {
                throw new InvalidDataException("Evidence contains case-colliding file paths.");
            }
        }

        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Converts an actual file path to the manifest's unambiguous slash-separated format.
    /// A literal backslash in a Unix filename cannot be represented by this schema.
    /// </summary>
    internal static String ToManifestPath(String root, String file)
    {
        String relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(file));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (relative == "." || relative == ".." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, comparison) ||
            Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Evidence file is outside the root.");
        }

        if (!OperatingSystem.IsWindows() && relative.Contains('\\'))
        {
            throw new InvalidDataException(
                "Evidence filename contains a literal backslash and cannot be represented in the manifest.");
        }

        // Colons can identify NTFS alternate data streams on Windows. Reject them
        // on every platform so a Unix-created manifest cannot name an ADS.
        if (relative.Contains(':'))
        {
            throw new InvalidDataException("Evidence filename contains a colon and is not portable in the manifest.");
        }

        String canonical = relative.Replace(Path.DirectorySeparatorChar, '/');
        ValidatePortableSegments(canonical);
        return canonical;
    }

    internal static String Resolve(String root, String relative)
    {
        if (String.IsNullOrWhiteSpace(relative) ||
            relative.Contains('\\') ||
            relative.Contains(':') ||
            Path.IsPathRooted(relative))
        {
            throw new InvalidDataException("Manifest contains an invalid relative path.");
        }

        String[] segments = relative.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("Manifest contains a non-canonical relative path.");
        }

        ValidatePortableSegments(relative);
        String fullRoot = Path.GetFullPath(root);
        String file = Path.GetFullPath(Path.Combine(fullRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        String withinRoot = Path.GetRelativePath(fullRoot, file);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (withinRoot == "." || withinRoot == ".." ||
            withinRoot.StartsWith(".." + Path.DirectorySeparatorChar, comparison) ||
            Path.IsPathRooted(withinRoot))
        {
            throw new InvalidDataException("Manifest contains a path outside the root.");
        }

        String current = fullRoot;
        RejectLinkedAncestors(current);
        foreach (String segment in withinRoot.Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if (File.Exists(current) || Directory.Exists(current))
            {
                RejectLink(current);
            }
        }

        return file;
    }

    // Windows treats these names as devices even when followed by an extension.
    // Validate on every platform so a Unix archive is portable to Windows.
    private static void ValidatePortableSegments(String relative)
    {
        foreach (String segment in relative.Split('/'))
        {
            if (segment.EndsWith(' ') || segment.EndsWith('.') ||
                segment.Any(character => character < ' ' || "\"<>|?*".Contains(character)))
            {
                throw new InvalidDataException("Evidence path contains a non-portable Windows filename.");
            }

            String stem = segment.Split('.')[0];
            Boolean reserved = stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
                (stem.Length == 4 &&
                 (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                  stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 "123456789¹²³".Contains(stem[3]));
            if (reserved)
            {
                throw new InvalidDataException("Evidence path contains a Windows device name.");
            }
        }
    }

    internal static void RejectLinkedAncestors(String path)
    {
        String current = Path.GetFullPath(path);
        while (true)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                RejectLink(current);
            }

            String? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
            if (String.IsNullOrEmpty(parent) || parent == current)
            {
                break;
            }

            current = parent;
        }
    }

    private static void RejectLink(String path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Evidence path contains a symbolic link or junction: " + path);
        }
    }
}
