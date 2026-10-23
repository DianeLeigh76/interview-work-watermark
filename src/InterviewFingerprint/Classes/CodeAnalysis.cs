using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace InterviewFingerprint;

internal static class CodeAnalysis
{

    private static String[] Tokens(String file)
    {
        // C# lexer, not regex: preserves String literals and syntax but ignores trivia.
        String source = File.ReadAllText(file);
        return SyntaxFactory.ParseTokens(source)
            .Where(t => t.Kind() != Microsoft.CodeAnalysis.CSharp.SyntaxKind.EndOfFileToken)
            .Select(t => $"{(Int32)t.Kind()}:{t.Text}")
            .ToArray();
    }

    private static String Digest(IEnumerable<String> tokens)
    {
        String serialized = String.Join("\n", tokens.Select(t => $"{t.Length}:{t}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }

    internal static FingerprintResult Fingerprint(String file)
    {
        using var stream = File.OpenRead(file);
        String exact = Convert.ToHexString(SHA256.HashData(stream));
        var tokens = Tokens(file);
        return new(exact, Digest(tokens), tokens.Length);
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
        var first = Tokens(a);
        var second = Tokens(b);
        Boolean identical = Digest(first) == Digest(second);
        var x = Shingles(first);
        var y = Shingles(second);
        if (x.Count == 0 || y.Count == 0)
        {
            return new(identical, identical ? 1 : 0, identical ? 1 : 0, first.Length, second.Length);
        }
        Int32 common = x.Intersect(y).Count();
        return new(identical, (Double)common / (x.Count + y.Count - common),
            (Double)common / Math.Min(x.Count, y.Count), first.Length, second.Length);
    }
}