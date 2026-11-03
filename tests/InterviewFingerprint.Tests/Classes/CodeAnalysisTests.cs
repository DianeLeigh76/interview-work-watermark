using System.Security.Cryptography;
using System.Text;
using InterviewFingerprint.Classes;
using Xunit;

namespace InterviewFingerprint.Tests.Classes;

public sealed class CodeAnalysisTests
{
    [Fact]
    public void FormattingAndCommentsDoNotChangeSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 Count = 3; }\n", Encoding.UTF8);
        File.WriteAllText(secondPath,
            "class Example\n{\n    // This comment is not code.\n    Int32 Count = 3;\n}\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var first = CodeAnalysis.Fingerprint(firstPath);
        var second = CodeAnalysis.Fingerprint(secondPath);
        Assert.NotEqual(first.ExactSha256, second.ExactSha256);
        Assert.Equal(first.SyntaxSha256, second.SyntaxSha256);
        Assert.True(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(firstPath))), first.ExactSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(secondPath))), second.ExactSha256);
    }

    [Fact]
    public void DirectivesAndInactiveSourceAffectSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath,
            "class Example\n{\n#if DEBUG\n    Int32 Count = 3;\n#endif\n}\n");
        File.WriteAllText(secondPath,
            "class Example\n{\n#if DEBUG\n    Int32 Count = 4;\n#endif\n}\n");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }

    [Fact]
    public void ShortSourceDoesNotInventShingleSimilarity()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "Value");
        File.WriteAllText(secondPath, "Value");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.True(comparison.SyntaxIdentical);
        Assert.Equal(0.0, comparison.Jaccard);
        Assert.Equal(0.0, comparison.Containment);
        Assert.True(comparison.TokenCountA < 5);
    }


    [Fact]
    public void Utf8BomChangesExactHashButNotSyntaxHash()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "utf8-bom.cs");
        String secondPath = Path.Combine(directory.Root, "utf8-no-bom.cs");
        String source = "class Example { Int32 Count = 3; }";
        File.WriteAllText(firstPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(secondPath, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var first = CodeAnalysis.Fingerprint(firstPath);
        var second = CodeAnalysis.Fingerprint(secondPath);
        Assert.NotEqual(first.ExactSha256, second.ExactSha256);
        Assert.Equal(first.SyntaxSha256, second.SyntaxSha256);
        Assert.True(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
    }

    [Fact]
    public void InvalidUtf8CannotBeSilentlyReplaced()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "invalid.cs");
        File.WriteAllBytes(path, [0xC3, 0x28]);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }
}
