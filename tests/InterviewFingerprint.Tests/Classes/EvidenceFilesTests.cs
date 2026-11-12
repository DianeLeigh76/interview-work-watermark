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

    [Fact]
    public void RejectsFilesOutsideEvidenceRoot()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => EvidenceFiles.ToManifestPath(directory.Root, outside));
    }
}
