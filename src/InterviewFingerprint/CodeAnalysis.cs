using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace InterviewFingerprint;

internal static class CodeAnalysis
{
    internal sealed record FingerprintResult(string ExactSha256, string SyntaxSha256, int TokenCount);
    internal sealed record ComparisonResult(bool SyntaxIdentical, double Jaccard, double Containment, int TokenCountA, int TokenCountB);

    private static string[] Tokens(string file)
    {
        // C# lexer, not regex: preserves string literals and syntax but ignores trivia.
        string source = File.ReadAllText(file);
        return SyntaxFactory.ParseTokens(source)
            .Where(t => t.Kind() != Microsoft.CodeAnalysis.CSharp.SyntaxKind.EndOfFileToken)
            .Select(t => $"{(int)t.Kind()}:{t.Text}")
            .ToArray();
    }

    private static string Digest(IEnumerable<string> tokens)
    {
        string serialized = string.Join("\n", tokens.Select(t => $"{t.Length}:{t}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }

    internal static FingerprintResult Fingerprint(string file)
    {
        using var stream = File.OpenRead(file);
        string exact = Convert.ToHexString(SHA256.HashData(stream));
        var tokens = Tokens(file);
        return new(exact, Digest(tokens), tokens.Length);
    }

    private static HashSet<string> Shingles(string[] tokens)
    {
        const int k = 5;
        var result = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index + k <= tokens.Length; index++)
            result.Add(Digest(tokens.Skip(index).Take(k)));
        return result;
    }

    internal static ComparisonResult Compare(string a, string b)
    {
        var first = Tokens(a);
        var second = Tokens(b);
        bool identical = Digest(first) == Digest(second);
        var x = Shingles(first);
        var y = Shingles(second);
        if (x.Count == 0 || y.Count == 0)
            return new(identical, identical ? 1 : 0, identical ? 1 : 0, first.Length, second.Length);
        int common = x.Intersect(y).Count();
        return new(identical, (double)common / (x.Count + y.Count - common),
            (double)common / Math.Min(x.Count, y.Count), first.Length, second.Length);
    }
}