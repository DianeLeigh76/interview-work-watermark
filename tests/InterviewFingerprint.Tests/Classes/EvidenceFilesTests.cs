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

}
