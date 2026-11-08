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


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf16WithBomPreservesSyntaxButNotExactBytes(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String utf16Path = Path.Combine(directory.Root, "utf16.cs");
        String utf8Path = Path.Combine(directory.Root, "utf8.cs");
        String source = "class Example { String Name = \"caf\\u00e9\"; }";
        File.WriteAllText(utf16Path, source,
            new UnicodeEncoding(bigEndian: bigEndian, byteOrderMark: true, throwOnInvalidBytes: true));
        File.WriteAllText(utf8Path, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var utf16 = CodeAnalysis.Fingerprint(utf16Path);
        var utf8 = CodeAnalysis.Fingerprint(utf8Path);
        Assert.NotEqual(utf16.ExactSha256, utf8.ExactSha256);
        Assert.Equal(utf16.SyntaxSha256, utf8.SyntaxSha256);
        Assert.True(CodeAnalysis.Compare(utf16Path, utf8Path).SyntaxIdentical);
    }


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidUtf16SurrogateCannotBeSilentlyReplaced(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "invalid-utf16.cs");
        Byte[] malformed = bigEndian
            ? new Byte[] { 0xFE, 0xFF, 0xD8, 0x00 }
            : new Byte[] { 0xFF, 0xFE, 0x00, 0xD8 };
        File.WriteAllBytes(path, malformed);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Fact]
    public void InvalidUtf8CannotBeSilentlyReplaced()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "invalid.cs");
        File.WriteAllBytes(path, [0xC3, 0x28]);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf32WithBomPreservesSyntaxButNotExactBytes(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String utf32Path = Path.Combine(directory.Root, "utf32.cs");
        String utf8Path = Path.Combine(directory.Root, "utf8.cs");
        String source = "class Example { String Name = \"caf\\u00e9\"; }";
        File.WriteAllText(utf32Path, source,
            new UTF32Encoding(bigEndian: bigEndian, byteOrderMark: true, throwOnInvalidCharacters: true));
        File.WriteAllText(utf8Path, source, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var utf32 = CodeAnalysis.Fingerprint(utf32Path);
        var utf8 = CodeAnalysis.Fingerprint(utf8Path);
        Assert.NotEqual(utf32.ExactSha256, utf8.ExactSha256);
        Assert.Equal(utf32.SyntaxSha256, utf8.SyntaxSha256);
        Assert.True(CodeAnalysis.Compare(utf32Path, utf8Path).SyntaxIdentical);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidUtf32ScalarCannotBeSilentlyReplaced(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "invalid-utf32.cs");
        Byte[] malformed = bigEndian
            ? new Byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x11, 0x00, 0x00 }
            : new Byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x11, 0x00 };
        File.WriteAllBytes(path, malformed);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Fact]
    public void BomlessUtf16CannotBeMistakenForUtf8Source()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "bomless-utf16.cs");
        File.WriteAllBytes(path, Encoding.Unicode.GetBytes("class Example { }"));

        Assert.Throws<InvalidDataException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Fact]
    public void CommentOnlyFilesDoNotProducePositiveSimilarityEvidence()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "// First comment only");
        File.WriteAllText(secondPath, "/* Different comment only */");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.True(comparison.SyntaxIdentical);
        Assert.Equal(0.0, comparison.Jaccard);
        Assert.Equal(0.0, comparison.Containment);
        Assert.Equal(0, comparison.TokenCountA);
        Assert.Equal(0, comparison.TokenCountB);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).ExactSha256,
            CodeAnalysis.Fingerprint(secondPath).ExactSha256);
    }

    [Fact]
    public void RenamedIdentifierChangesSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 Count = 3; }");
        File.WriteAllText(secondPath, "class Example { Int32 Total = 3; }");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TruncatedUtf8BomIsRejected(Int32 byteCount)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "truncated-bom.cs");
        Byte[] bytes = new Byte[] { 0xEF, 0xBB };
        File.WriteAllBytes(path, bytes.Take(byteCount).ToArray());

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Fact]
    public void LineEndingsChangeExactHashButNotSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "lf.cs");
        String secondPath = Path.Combine(directory.Root, "crlf.cs");
        String source = "class Example\n{\n    Int32 Count = 3;\n}\n";
        File.WriteAllText(firstPath, source, new UTF8Encoding(false));
        File.WriteAllText(secondPath, source.Replace("\n", "\r\n"), new UTF8Encoding(false));

        var first = CodeAnalysis.Fingerprint(firstPath);
        var second = CodeAnalysis.Fingerprint(secondPath);
        Assert.NotEqual(first.ExactSha256, second.ExactSha256);
        Assert.Equal(first.SyntaxSha256, second.SyntaxSha256);
        Assert.True(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
    }


    [Fact]
    public void ChangedStringLiteralIsNotSyntaxIdentical()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { String Name = \"first\"; }");
        File.WriteAllText(secondPath, "class Example { String Name = \"second\"; }");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }


    [Fact]
    public void SimilarityComparisonIsSymmetric()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 Count = 3; Int32 Total = 4; }");
        File.WriteAllText(secondPath, "class Example { Int32 Count = 3; Int32 Total = 5; }");

        var forward = CodeAnalysis.Compare(firstPath, secondPath);
        var reverse = CodeAnalysis.Compare(secondPath, firstPath);
        Assert.Equal(forward.SyntaxIdentical, reverse.SyntaxIdentical);
        Assert.Equal(forward.Jaccard, reverse.Jaccard);
        Assert.Equal(forward.Containment, reverse.Containment);
        Assert.Equal(forward.TokenCountA, reverse.TokenCountB);
        Assert.Equal(forward.TokenCountB, reverse.TokenCountA);
    }

    [Fact]
    public void EmptyFilesHaveNoSimilarityEvidence()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "empty-first.cs");
        String secondPath = Path.Combine(directory.Root, "empty-second.cs");
        File.WriteAllBytes(firstPath, Array.Empty<Byte>());
        File.WriteAllBytes(secondPath, Array.Empty<Byte>());

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.True(comparison.SyntaxIdentical);
        Assert.Equal(0.0, comparison.Jaccard);
        Assert.Equal(0.0, comparison.Containment);
        Assert.Equal(0, comparison.TokenCountA);
        Assert.Equal(0, comparison.TokenCountB);
    }

    [Fact]
    public void MissingSourceFileIsNotTreatedAsEmptySource()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String missingPath = Path.Combine(directory.Root, "missing.cs");

        Assert.Throws<FileNotFoundException>(() => CodeAnalysis.Fingerprint(missingPath));
    }

    [Fact]
    public void DifferentNumericLiteralsChangeSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 Count = 3; }");
        File.WriteAllText(secondPath, "class Example { Int32 Count = 4; }");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }
    [Fact]
    public void EmptySourceComparedWithNonemptySourceHasNoSimilarityEvidence()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String emptyPath = Path.Combine(directory.Root, "empty.cs");
        String populatedPath = Path.Combine(directory.Root, "populated.cs");
        File.WriteAllBytes(emptyPath, Array.Empty<Byte>());
        File.WriteAllText(populatedPath, "class Example { Int32 Count = 3; }");

        var comparison = CodeAnalysis.Compare(emptyPath, populatedPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.Equal(0.0, comparison.Jaccard);
        Assert.Equal(0.0, comparison.Containment);
        Assert.Equal(0, comparison.TokenCountA);
        Assert.True(comparison.TokenCountB > 0);
    }
    [Fact]
    public void IdenticalNonemptySourceHasFullShingleAgreement()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        String source = "class Example { Int32 Count = 3; Int32 Total = 4; }";
        File.WriteAllText(firstPath, source);
        File.WriteAllText(secondPath, source);

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.True(comparison.SyntaxIdentical);
        Assert.True(comparison.TokenCountA >= 5);
        Assert.Equal(1.0, comparison.Jaccard);
        Assert.Equal(1.0, comparison.Containment);
    }
    [Fact]
    public void DisjointTokenSequencesHaveZeroShingleAgreement()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "alpha beta gamma delta epsilon zeta eta theta");
        File.WriteAllText(secondPath, "one two three four five six seven eight");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.True(comparison.TokenCountA >= 5);
        Assert.True(comparison.TokenCountB >= 5);
        Assert.Equal(0.0, comparison.Jaccard);
        Assert.Equal(0.0, comparison.Containment);
    }
    [Fact]
    public void MissingComparisonInputThrowsInsteadOfReportingSimilarity()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String existingPath = Path.Combine(directory.Root, "existing.cs");
        String missingPath = Path.Combine(directory.Root, "missing.cs");
        File.WriteAllText(existingPath, "class Example { Int32 Count = 3; }");

        Assert.Throws<FileNotFoundException>(() => CodeAnalysis.Compare(existingPath, missingPath));
        Assert.Throws<FileNotFoundException>(() => CodeAnalysis.Compare(missingPath, existingPath));
    }

    [Fact]
    public void MalformedUtf8ComparisonInputThrowsInEitherPosition()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String validPath = Path.Combine(directory.Root, "valid.cs");
        String invalidPath = Path.Combine(directory.Root, "invalid.cs");
        File.WriteAllText(validPath, "class Example { Int32 Count = 3; }");
        File.WriteAllBytes(invalidPath, new Byte[] { 0xC3, 0x28 });

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Compare(validPath, invalidPath));
        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Compare(invalidPath, validPath));
    }

    [Fact]
    public void BomlessUtf16ComparisonInputThrowsInEitherPosition()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String validPath = Path.Combine(directory.Root, "valid.cs");
        String invalidPath = Path.Combine(directory.Root, "bomless-utf16.cs");
        File.WriteAllText(validPath, "class Example { Int32 Count = 3; }");
        File.WriteAllBytes(invalidPath, Encoding.Unicode.GetBytes("class Example { }"));

        Assert.Throws<InvalidDataException>(() => CodeAnalysis.Compare(validPath, invalidPath));
        Assert.Throws<InvalidDataException>(() => CodeAnalysis.Compare(invalidPath, validPath));
    }

    [Fact]
    public void NonemptySourceComparedWithEmptySourceHasNoSimilarityEvidence()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String emptyPath = Path.Combine(directory.Root, "empty.cs");
        String populatedPath = Path.Combine(directory.Root, "populated.cs");
        File.WriteAllBytes(emptyPath, Array.Empty<Byte>());
        File.WriteAllText(populatedPath, "class Example { Int32 Count = 3; }");

        var comparison = CodeAnalysis.Compare(populatedPath, emptyPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.Equal(0.0, comparison.Jaccard);
        Assert.Equal(0.0, comparison.Containment);
        Assert.True(comparison.TokenCountA > 0);
        Assert.Equal(0, comparison.TokenCountB);
    }

    [Fact]
    public void IdenticalSourceAtDifferentPathsHasEqualExactAndSyntaxHashes()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        String source = "class Example { Int32 Count = 3; Int32 Total = 4; }";
        File.WriteAllText(firstPath, source);
        File.WriteAllText(secondPath, source);

        var first = CodeAnalysis.Fingerprint(firstPath);
        var second = CodeAnalysis.Fingerprint(secondPath);
        Assert.Equal(first.ExactSha256, second.ExactSha256);
        Assert.Equal(first.SyntaxSha256, second.SyntaxSha256);
    }

    [Fact]
    public void DifferentCommentOnlyFilesHaveNoSimilarityEvidenceInEitherOrder()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "// alpha");
        File.WriteAllText(secondPath, "// beta");

        var forward = CodeAnalysis.Compare(firstPath, secondPath);
        var reverse = CodeAnalysis.Compare(secondPath, firstPath);
        Assert.Equal(0.0, forward.Jaccard);
        Assert.Equal(0.0, forward.Containment);
        Assert.Equal(0.0, reverse.Jaccard);
        Assert.Equal(0.0, reverse.Containment);
        Assert.Equal(0, forward.TokenCountA);
        Assert.Equal(0, forward.TokenCountB);
        Assert.Equal(0, reverse.TokenCountA);
        Assert.Equal(0, reverse.TokenCountB);
    }

    [Fact]
    public void SharedFiveTokenWindowProducesPartialSimilarity()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "alpha beta gamma delta epsilon zeta");
        File.WriteAllText(secondPath, "alpha beta gamma delta epsilon theta");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.True(comparison.Jaccard > 0.0);
        Assert.True(comparison.Jaccard < 1.0);
        Assert.True(comparison.Containment > 0.0);
        Assert.True(comparison.Containment < 1.0);
    }

    [Fact]
    public void FullyContainedShingleSetHasFullContainmentButNotFullJaccard()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String shorterPath = Path.Combine(directory.Root, "shorter.cs");
        String longerPath = Path.Combine(directory.Root, "longer.cs");
        File.WriteAllText(shorterPath, "alpha beta gamma delta epsilon");
        File.WriteAllText(longerPath, "alpha beta gamma delta epsilon zeta eta");

        var comparison = CodeAnalysis.Compare(shorterPath, longerPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.Equal(1.0, comparison.Containment);
        Assert.True(comparison.Jaccard > 0.0);
        Assert.True(comparison.Jaccard < 1.0);
    }

    [Fact]
    public void RepeatedShinglesDoNotInflateSimilarity()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "alpha alpha alpha alpha alpha alpha");
        File.WriteAllText(secondPath, "alpha alpha alpha alpha alpha");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.Equal(1.0, comparison.Jaccard);
        Assert.Equal(1.0, comparison.Containment);
        Assert.Equal(6, comparison.TokenCountA);
        Assert.Equal(5, comparison.TokenCountB);
    }

    [Fact]
    public void IdentifierCaseChangesSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 Count = 3; }");
        File.WriteAllText(secondPath, "class Example { Int32 count = 3; }");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }


    [Fact]
    public void WhitespaceInsideStringLiteralChangesSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { String Name = \"alpha beta\"; }");
        File.WriteAllText(secondPath, "class Example { String Name = \"alpha  beta\"; }");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }


    [Fact]
    public void UnicodeIdentifierSpellingChangesSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 café = 3; }");
        File.WriteAllText(secondPath, "class Example { Int32 cafe = 3; }");

        var comparison = CodeAnalysis.Compare(firstPath, secondPath);
        Assert.False(comparison.SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }


    [Fact]
    public void CommentMarkersInsideStringLiteralsRemainSignificant()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { String Url = \"https://example.test/a\"; }");
        File.WriteAllText(secondPath, "class Example { String Url = \"https://example.test/b\"; }");

        Assert.False(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
    }


    [Fact]
    public void Utf8SourceContainingNullCharacterIsRejected()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "nul.cs");
        File.WriteAllBytes(path, new Byte[] { 0x63, 0x6C, 0x61, 0x73, 0x73, 0x00 });

        Assert.Throws<InvalidDataException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Fact]
    public void CommentsBetweenTokensDoNotChangeSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "class Example { Int32 Count = 3; }");
        File.WriteAllText(secondPath, "class /* explanation */ Example { Int32 /* note */ Count = 3; }");

        Assert.Equal(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
        Assert.True(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
    }

    [Fact]
    public void DisabledPreprocessorBranchTextChangesSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        File.WriteAllText(firstPath, "#if false\nclass Hidden { Int32 Value = 1; }\n#endif\nclass Visible { }");
        File.WriteAllText(secondPath, "#if false\nclass Hidden { Int32 Value = 2; }\n#endif\nclass Visible { }");

        Assert.False(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }

    [Fact]
    public void LineEndingsInsideVerbatimStringLiteralChangeSyntaxFingerprint()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String firstPath = Path.Combine(directory.Root, "first.cs");
        String secondPath = Path.Combine(directory.Root, "second.cs");
        String source = "class Example { String Value = @\"first\nsecond\"; }";
        File.WriteAllText(firstPath, source, new UTF8Encoding(false));
        File.WriteAllText(secondPath, source.Replace("\n", "\r\n"), new UTF8Encoding(false));

        Assert.False(CodeAnalysis.Compare(firstPath, secondPath).SyntaxIdentical);
        Assert.NotEqual(CodeAnalysis.Fingerprint(firstPath).SyntaxSha256,
            CodeAnalysis.Fingerprint(secondPath).SyntaxSha256);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TruncatedUtf16CodeUnitIsRejected(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "truncated-utf16.cs");
        Byte[] malformed = bigEndian
            ? new Byte[] { 0xFE, 0xFF, 0x00 }
            : new Byte[] { 0xFF, 0xFE, 0x00 };
        File.WriteAllBytes(path, malformed);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TruncatedUtf32CodeUnitIsRejected(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "truncated-utf32.cs");
        Byte[] malformed = bigEndian
            ? new Byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00 }
            : new Byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x00 };
        File.WriteAllBytes(path, malformed);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf16SourceContainingNullCharacterIsRejected(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "nul-utf16.cs");
        File.WriteAllText(path, "class Example { }\0",
            new UnicodeEncoding(bigEndian: bigEndian, byteOrderMark: true, throwOnInvalidBytes: true));

        Assert.Throws<InvalidDataException>(() => CodeAnalysis.Fingerprint(path));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf32SourceContainingNullCharacterIsRejected(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "nul-utf32.cs");
        File.WriteAllText(path, "class Example { }\0",
            new UTF32Encoding(bigEndian: bigEndian, byteOrderMark: true, throwOnInvalidCharacters: true));

        Assert.Throws<InvalidDataException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Fact]
    public void MalformedBomMarkedUtf8IsRejected()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String path = Path.Combine(directory.Root, "invalid-bom-utf8.cs");
        File.WriteAllBytes(path, new Byte[] { 0xEF, 0xBB, 0xBF, 0xC3, 0x28 });

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Fingerprint(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedUtf16ComparisonInputThrowsInEitherPosition(Boolean bigEndian)
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String validPath = Path.Combine(directory.Root, "valid.cs");
        String invalidPath = Path.Combine(directory.Root, "invalid-utf16.cs");
        File.WriteAllText(validPath, "class Example { Int32 Count = 3; }");
        Byte[] malformed = bigEndian
            ? new Byte[] { 0xFE, 0xFF, 0xD8, 0x00 }
            : new Byte[] { 0xFF, 0xFE, 0x00, 0xD8 };
        File.WriteAllBytes(invalidPath, malformed);

        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Compare(validPath, invalidPath));
        Assert.Throws<DecoderFallbackException>(() => CodeAnalysis.Compare(invalidPath, validPath));
    }

}
