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
    public void ResolvesValidPortableManifestPaths(String relative)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String expected = Path.GetFullPath(Path.Combine(directory.Root,
            relative.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(expected, EvidenceFiles.Resolve(directory.Root, relative));
    }

    [Fact]
    public void RejectsFilesOutsideEvidenceRoot()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.ToManifestPath(directory.Root, outside));
    }
}
