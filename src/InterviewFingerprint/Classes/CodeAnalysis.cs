using InterviewFingerprint.Records;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace InterviewFingerprint.Classes;

internal static class CodeAnalysis
{

    private static String[] Tokens(String source)
    {
        // Ignore ordinary whitespace/comments, but retain directives and disabled text:
        // changing #if or its inactive branch must not appear syntax-identical.
        List<String> tokens = new List<String>();
        foreach (SyntaxToken token in SyntaxFactory.ParseTokens(source))
        {
            AddSignificantTrivia(tokens, token.LeadingTrivia);
            if (!token.IsKind(SyntaxKind.EndOfFileToken))
            {
                tokens.Add($"{(Int32)token.Kind()}:{token.Text}");
            }

            AddSignificantTrivia(tokens, token.TrailingTrivia);
        }

        return tokens.ToArray();
    }

    private static void AddSignificantTrivia(List<String> tokens, SyntaxTriviaList triviaList)
    {
        foreach (SyntaxTrivia trivia in triviaList)
        {
            if (trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            {
                tokens.Add($"trivia:{(Int32)trivia.Kind()}:{trivia.ToString().Trim()}");
            }
        }
    }

    private static String Digest(IEnumerable<String> tokens)
    {
        String serialized = String.Join("\n", tokens.Select(t => $"{t.Length}:{t}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }

    internal static FingerprintResult Fingerprint(String file)
    {
        // Hash and tokenize the same byte snapshot, so a file modified between reads
        // cannot produce an exact hash and a syntax hash for different versions.
        Byte[] bytes = File.ReadAllBytes(file);
        String exact = Convert.ToHexString(SHA256.HashData(bytes));
        String source = ReadSource(bytes);
        var tokens = Tokens(source);
        return new(exact, Digest(tokens), tokens.Length);
    }

    // Decode BOM-marked UTF-8, UTF-16 and UTF-32 strictly. StreamReader's
    // automatic BOM detection can use replacement fallbacks for UTF-16/32,
    // making distinct malformed files appear syntax-identical.
    private static String ReadSource(Byte[] bytes)
    {
        Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        Int32 bomLength = 0;
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 &&
            bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true);
            bomLength = 4;
        }
        else if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE &&
            bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);
            bomLength = 4;
        }
        else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            bomLength = 3;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
            bomLength = 2;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
            bomLength = 2;
        }

        String source = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        // NUL bytes in BOM-less UTF-16 can decode as valid UTF-8 while
        // producing misleading C# tokens. Require an explicit Unicode BOM.
        if (source.Contains('\0'))
        {
            throw new InvalidDataException("C# source contains NUL characters; use a supported Unicode BOM.");
        }

        return source;
    }

    private static HashSet<String> Shingles(String[] tokens)
    {
        const Int32 k = 5;
        var result = new HashSet<String>(StringComparer.Ordinal);
        for (Int32 index = 0; index + k <= tokens.Length; index++)
        {
            result.Add(Digest(tokens.Skip(index).Take(k)));
        }
        return result;
    }

    internal static ComparisonResult Compare(String a, String b)
    {
        var first = Tokens(ReadSource(File.ReadAllBytes(a)));
        var second = Tokens(ReadSource(File.ReadAllBytes(b)));
        Boolean identical = Digest(first) == Digest(second);
        // An empty or comment-only source file contains no code evidence.
        // Equality of two empty token streams must not be scored as 100% similarity.
        if (first.Length == 0 || second.Length == 0)
        {
            return new(identical, 0, 0, first.Length, second.Length);
        }

        var x = Shingles(first);
        var y = Shingles(second);
        if (x.Count == 0 || y.Count == 0)
        {
            // With fewer than five tokens there are no shingles to compare.
            // Syntax equality is reported separately; do not invent 100% similarity.
            return new(identical, 0, 0, first.Length, second.Length);
        }
        Int32 common = x.Intersect(y).Count();
        return new(identical, (Double)common / (x.Count + y.Count - common),
            (Double)common / Math.Min(x.Count, y.Count), first.Length, second.Length);
    }
}