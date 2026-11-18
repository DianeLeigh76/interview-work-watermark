using InterviewFingerprint.Classes;
using Xunit;

namespace InterviewFingerprint.Tests.Classes;

public sealed class EvidenceFilesTests
{
    [Fact]
    public void EnumeratesNestedFilesUsingPortableRelativePaths()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "nested");
        Directory.CreateDirectory(nested);
        String file = Path.Combine(nested, "sample.txt");
        File.WriteAllText(file, "Synthetic content only.");

        IReadOnlyList<String> files = EvidenceFiles.Enumerate(directory.Root);
        Assert.Single(files);
        Assert.Equal("nested/sample.txt", EvidenceFiles.ToManifestPath(directory.Root, files[0]));
        Assert.Equal(Path.GetFullPath(file), EvidenceFiles.Resolve(directory.Root, "nested/sample.txt"));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("nested/../escape.txt")]
    [InlineData("bad:name.txt")]
    [InlineData("bad\\name.txt")]
    [InlineData("CON.txt")]
    [InlineData("trailingdot.")]
    [InlineData("nested//empty.txt")]
    public void RejectsUnsafeOrNonportableManifestPaths(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("COM1.txt")]
    [InlineData("LPT9.log")]
    [InlineData("conin$.txt")]
    [InlineData("COM¹.txt")]
    public void RejectsAdditionalWindowsDeviceNames(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("leading/space .txt")]
    [InlineData("nested/trailing ")]
    [InlineData("nested/question?.txt")]
    [InlineData("nested/star*.txt")]
    [InlineData("nested/pipe|.txt")]
    public void RejectsAdditionalNonportableFilenameCharacters(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("nested/control\nname.txt")]
    [InlineData("nested/control\tname.txt")]
    [InlineData("nested/control\u001fname.txt")]
    public void RejectsControlCharactersInManifestPaths(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("PRN.log")]
    [InlineData("AUX.txt")]
    [InlineData("NUL")]
    [InlineData("CONOUT$.txt")]
    [InlineData("LPT³.txt")]
    public void RejectsOtherReservedWindowsDeviceNames(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("ordinary.txt")]
    [InlineData("nested/multiple.dots.txt")]
    [InlineData("nested/file with spaces.txt")]
    [InlineData("COM0.txt")]
    [InlineData("COM10.txt")]
    [InlineData("LPT0.txt")]
    [InlineData("nested/café.txt")]
    public void ResolvesValidPortableManifestPaths(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String expected = Path.GetFullPath(Path.Combine(directory.Root,
            relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(expected, EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("./file.txt")]
    [InlineData("nested/./file.txt")]
    [InlineData("nested/../file.txt")]
    [InlineData("/absolute.txt")]
    public void RejectsEmptyRootedOrNoncanonicalPaths(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("nested//file.txt")]
    [InlineData("nested/")]
    [InlineData("nested\\file.txt")]
    [InlineData("nested:stream.txt")]
    [InlineData("nested/file.txt:stream")]
    public void RejectsEmptySegmentsAndPlatformSpecificSeparators(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Fact]
    public void ManifestPathRoundTripsForNestedPortableFile()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "folder with spaces", "second");
        Directory.CreateDirectory(nested);
        String file = Path.Combine(nested, "sample.multi.part.txt");
        File.WriteAllText(file, "Synthetic regression fixture.");

        String relative = EvidenceFiles.ToManifestPath(directory.Root, file);

        Assert.Equal("folder with spaces/second/sample.multi.part.txt", relative);
        Assert.Equal(Path.GetFullPath(file), EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("nested/CON.txt")]
    [InlineData("nested/COM2.log")]
    [InlineData("nested/LPT1")]
    [InlineData("nested/aux.backup")]
    public void RejectsReservedDeviceNamesInsideNestedPaths(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Theory]
    [InlineData("nested/trailingdot.")]
    [InlineData("nested/trailing ")]
    [InlineData("nested/invalid<name.txt")]
    [InlineData("nested/invalid>name.txt")]
    [InlineData("nested/invalid\"name.txt")]
    public void RejectsNonportableCharactersInNestedSegments(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Fact]
    public void RejectsSiblingDirectoryWithMatchingRootPrefix()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String root = Path.Combine(directory.Root, "evidence");
        String sibling = Path.Combine(directory.Root, "evidence-other", "sample.txt");
        Directory.CreateDirectory(root);

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.ToManifestPath(root, sibling));
    }

    [Fact]
    public void RejectsFilesOutsideEvidenceRoot()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.ToManifestPath(directory.Root, outside));
    }
    [Fact]
    public void RejectsCaseCollidingEvidenceFiles()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Report.txt"), "First synthetic file.");
        File.WriteAllText(Path.Combine(directory.Root, "report.txt"), "Second synthetic file.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsCaseCollidingFileAndDirectory()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "Records"), "Synthetic file.");
        Directory.CreateDirectory(Path.Combine(directory.Root, "records"));

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsUnicodeNormalizationCollisions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "caf\u00e9.txt"), "Composed filename.");
        File.WriteAllText(Path.Combine(directory.Root, "cafe\u0301.txt"), "Decomposed filename.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsNestedCaseCollidingFiles()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Report.txt"), "First synthetic file.");
        File.WriteAllText(Path.Combine(nested, "report.txt"), "Second synthetic file.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsNestedUnicodeNormalizationCollisions()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "caf\u00e9.txt"), "Composed filename.");
        File.WriteAllText(Path.Combine(nested, "cafe\u0301.txt"), "Decomposed filename.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsNestedFileAndDirectoryCaseCollision()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Records"), "Synthetic file.");
        Directory.CreateDirectory(Path.Combine(nested, "records"));

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsCaseCollidingDirectories()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Root, "Records"));
        Directory.CreateDirectory(Path.Combine(directory.Root, "records"));

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }


    [Fact]
    public void RejectsUnicodeNormalizationCollidingDirectories()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Root, "caf\u00e9"));
        Directory.CreateDirectory(Path.Combine(directory.Root, "cafe\u0301"));

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }


    [Fact]
    public void RejectsUnicodeNormalizationFileAndDirectoryCollision()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "caf\u00e9"), "Synthetic file.");
        Directory.CreateDirectory(Path.Combine(directory.Root, "cafe\u0301"));

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }


    [Fact]
    public void RejectsEvidenceRootItselfAsManifestFile()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, directory.Root));
    }

    [Fact]
    public void RejectsParentDirectoryAsManifestFile()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String parent = Directory.GetParent(directory.Root)!.FullName;

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, parent));
    }

    [Fact]
    public void ResolvesNestedPortablePathWithoutCreatingFile()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String relative = "future/subfolder/evidence.txt";
        String expected = Path.Combine(directory.Root, "future", "subfolder", "evidence.txt");

        Assert.Equal(Path.GetFullPath(expected), EvidenceFiles.Resolve(directory.Root, relative));
        Assert.False(File.Exists(expected));
    }

    [Fact]
    public void RejectsDanglingFileSymbolicLinkDuringResolution()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String link = Path.Combine(directory.Root, "dangling.txt");
        File.CreateSymbolicLink(link, Path.Combine(directory.Root, "missing.txt"));

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Resolve(directory.Root, "dangling.txt"));
    }

    [Fact]
    public void RejectsDanglingDirectorySymbolicLinkDuringResolution()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String link = Path.Combine(directory.Root, "dangling");
        Directory.CreateSymbolicLink(link, Path.Combine(directory.Root, "missing"));

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Resolve(directory.Root, "dangling/sample.txt"));
    }

    [Fact]
    public void RejectsLinkedEvidenceRootDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        String link = Path.Combine(directory.Root, "linked");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(link, target);

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(link));
    }

    [Fact]
    public void RejectsLinkedIntermediateDirectoryDuringResolution()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(Path.Combine(directory.Root, "linked"), target);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Resolve(directory.Root, "linked/sample.txt"));
    }

    [Fact]
    public void RejectsLinkedEvidenceFileDuringResolution()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target.txt");
        File.WriteAllText(target, "Synthetic test content.");
        File.CreateSymbolicLink(Path.Combine(directory.Root, "linked.txt"), target);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Resolve(directory.Root, "linked.txt"));
    }

    [Fact]
    public void RejectsDanglingSymbolicLinkDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String link = Path.Combine(directory.Root, "missing-link.txt");
        File.CreateSymbolicLink(link, Path.Combine(directory.Root, "absent.txt"));

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsNestedDirectorySymbolicLinkDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "nested");
        String target = Path.Combine(directory.Root, "target");
        Directory.CreateDirectory(nested);
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(Path.Combine(nested, "linked"), target);

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsLinkedAncestorOfEvidenceRootDuringResolution()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        Directory.CreateDirectory(target);
        String linkedParent = Path.Combine(directory.Root, "linked-parent");
        Directory.CreateSymbolicLink(linkedParent, target);
        String evidenceRoot = Path.Combine(linkedParent, "evidence");
        Directory.CreateDirectory(evidenceRoot);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Resolve(evidenceRoot, "sample.txt"));
    }

    [Fact]
    public void RejectsDanglingAncestorOfEvidenceRootDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String link = Path.Combine(directory.Root, "dangling-parent");
        Directory.CreateSymbolicLink(link, Path.Combine(directory.Root, "missing-parent"));

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Enumerate(Path.Combine(link, "evidence")));
    }


    [Fact]
    public void RejectsLinkedAncestorOfEvidenceRootDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        String link = Path.Combine(directory.Root, "linked-parent");
        Directory.CreateDirectory(Path.Combine(target, "evidence"));
        Directory.CreateSymbolicLink(link, target);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.Enumerate(Path.Combine(link, "evidence")));
    }


    [Fact]
    public void RejectsColonInEnumeratedEvidenceFilename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "sample:stream.txt"), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsBackslashInEnumeratedEvidenceFilename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, @"sample\name.txt"), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsWindowsDeviceNameDuringEvidenceEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "CON.txt"), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void AllowsNonexistentEvidenceRootInAncestorLinkCheck()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String futureRoot = Path.Combine(directory.Root, "future", "evidence");

        EvidenceFiles.RejectLinkedAncestors(futureRoot);

        Assert.False(Directory.Exists(futureRoot));
    }

    [Fact]
    public void EnumeratesEvidenceFilesInOrdinalPathOrder()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "zeta.txt"), "Synthetic zeta.");
        File.WriteAllText(Path.Combine(directory.Root, "alpha.txt"), "Synthetic alpha.");
        Directory.CreateDirectory(Path.Combine(directory.Root, "nested"));
        File.WriteAllText(Path.Combine(directory.Root, "nested", "beta.txt"), "Synthetic beta.");

        String[] paths = EvidenceFiles.Enumerate(directory.Root)
            .Select(file => EvidenceFiles.ToManifestPath(directory.Root, file))
            .ToArray();

        Assert.Equal(new[] { "alpha.txt", "nested/beta.txt", "zeta.txt" }, paths);
    }

    [Fact]
    public void EnumeratesNoFilesFromEmptyNestedDirectories()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Root, "empty", "nested"));

        Assert.Empty(EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void EnumeratesDistinctSiblingDirectoriesWithSharedPrefixes()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String first = Path.Combine(directory.Root, "evidence");
        String second = Path.Combine(directory.Root, "evidence-other");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        File.WriteAllText(Path.Combine(first, "one.txt"), "Synthetic first.");
        File.WriteAllText(Path.Combine(second, "two.txt"), "Synthetic second.");

        String[] paths = EvidenceFiles.Enumerate(directory.Root)
            .Select(file => EvidenceFiles.ToManifestPath(directory.Root, file))
            .ToArray();

        Assert.Equal(new[] { "evidence-other/two.txt", "evidence/one.txt" }.OrderBy(value => value, StringComparer.Ordinal), paths);
    }
    [Fact]
    public void RejectsTrailingDotInEnumeratedEvidenceFilename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "report."), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }
    [Fact]
    public void RejectsControlCharacterInEnumeratedEvidenceFilename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "report\nname.txt"), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }
    [Fact]
    public void RejectsReservedDeviceDirectoryDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "COM1");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "sample.txt"), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }

    [Fact]
    public void RejectsTrailingSpaceInEnumeratedEvidenceFilename()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "report "), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }


    [Fact]
    public void RejectsSuperscriptWindowsDeviceNameDuringEnumeration()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "COM¹.txt"), "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() => EvidenceFiles.Enumerate(directory.Root));
    }


    [Fact]
    public void RejectsLinkedEvidenceFileDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target.txt");
        String link = Path.Combine(directory.Root, "linked.txt");
        File.WriteAllText(target, "Synthetic evidence.");
        File.CreateSymbolicLink(link, target);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, link));
    }

    [Fact]
    public void RejectsLinkedEvidenceRootDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        String link = Path.Combine(directory.Root, "linked-root");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "sample.txt"), "Synthetic evidence.");
        Directory.CreateSymbolicLink(link, target);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(link, Path.Combine(link, "sample.txt")));
    }

    [Fact]
    public void EnumeratesDistinctUnicodeNamesWithoutNormalizationCollision()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "cafe.txt"), "Plain synthetic evidence.");
        File.WriteAllText(Path.Combine(directory.Root, "café.txt"), "Accented synthetic evidence.");

        String[] paths = EvidenceFiles.Enumerate(directory.Root)
            .Select(file => EvidenceFiles.ToManifestPath(directory.Root, file))
            .ToArray();

        Assert.Equal(new[] { "cafe.txt", "café.txt" }, paths);
    }


    [Fact]
    public void RejectsDanglingLinkedEvidenceFileDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String link = Path.Combine(directory.Root, "dangling.txt");
        File.CreateSymbolicLink(link, Path.Combine(directory.Root, "missing.txt"));

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, link));
    }


    [Fact]
    public void RejectsLinkedIntermediateDirectoryDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        Directory.CreateDirectory(target);
        String link = Path.Combine(directory.Root, "linked");
        Directory.CreateSymbolicLink(link, target);

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, Path.Combine(link, "sample.txt")));
    }


    [Fact]
    public void RejectsLinkedAncestorOfEvidenceRootDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String target = Path.Combine(directory.Root, "target");
        Directory.CreateDirectory(Path.Combine(target, "evidence"));
        String link = Path.Combine(directory.Root, "linked-parent");
        Directory.CreateSymbolicLink(link, target);
        String root = Path.Combine(link, "evidence");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(root, Path.Combine(root, "sample.txt")));
    }


    [Fact]
    public void RejectsLiteralBackslashDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String file = Path.Combine(directory.Root, @"report\draft.txt");
        File.WriteAllText(file, "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, file));
    }


    [Fact]
    public void RejectsColonDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String file = Path.Combine(directory.Root, "report:stream.txt");
        File.WriteAllText(file, "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, file));
    }


    [Fact]
    public void RejectsReservedDirectoryDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "LPT1");
        Directory.CreateDirectory(nested);
        String file = Path.Combine(nested, "report.txt");
        File.WriteAllText(file, "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, file));
    }


    [Fact]
    public void RejectsSiblingPrefixPathDuringManifestConversion()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String sibling = directory.Root + "-other";
        String outside = Path.Combine(sibling, "sample.txt");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, outside));
    }


    [Fact]
    public void ConvertsFutureNestedEvidencePathWithoutCreatingIt()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String future = Path.Combine(directory.Root, "future", "nested", "sample.txt");

        Assert.Equal("future/nested/sample.txt",
            EvidenceFiles.ToManifestPath(directory.Root, future));
        Assert.False(File.Exists(future));
    }


    [Fact]
    public void RejectsNonportableDirectorySegmentDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String nested = Path.Combine(directory.Root, "bad.", "sample.txt");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, nested));
    }


    [Fact]
    public void RejectsTrailingSpaceDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String file = Path.Combine(directory.Root, "report ");
        File.WriteAllText(file, "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, file));
    }

    [Fact]
    public void RejectsControlCharacterDuringManifestConversion()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using TemporaryDirectory directory = new TemporaryDirectory();
        String file = Path.Combine(directory.Root, "report\\nnotes.txt");
        File.WriteAllText(file, "Synthetic evidence.");

        Assert.Throws<InvalidDataException>(() =>
            EvidenceFiles.ToManifestPath(directory.Root, file));
    }

}
