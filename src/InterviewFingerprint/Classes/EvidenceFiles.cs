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
        RejectLink(fullRoot);

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

        return files.OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    internal static String Resolve(String root, String relative)
    {
        if (String.IsNullOrWhiteSpace(relative) ||
            relative.Contains('\\') ||
            Path.IsPathRooted(relative) ||
            (relative.Length >= 2 && Char.IsLetter(relative[0]) && relative[1] == ':'))
        {
            throw new InvalidDataException("Manifest contains an invalid relative path.");
        }

        String[] segments = relative.Split('/');
        if (segments.Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidDataException("Manifest contains a non-canonical relative path.");
        }

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
        RejectLink(current);
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

    private static void RejectLink(String path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Evidence path contains a symbolic link or junction: " + path);
        }
    }
}
