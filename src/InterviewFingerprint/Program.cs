using InterviewFingerprint.Classes;
using InterviewFingerprint.Records;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace InterviewFingerprint;

internal static class Program
{
    private const Int32 PayloadBits = 64;

    public static Int32 Main(String[] arguments)
    {
        try
        {
            if (arguments.Length == 0)
            {
                Usage(); return 2;
            }
            String command = arguments[0].ToLowerInvariant();
            var options = Parse(arguments.Skip(1).ToArray());
            switch (command)
            {
                case "mark":
                {
                    String input = Required(options, "in");
                    String output = Required(options, "out");
                    String key = Secret();
                    String id = Required(options, "id");
                    Double strength = GetStrength(options);
                    CheckDistinct(input, output);
                    using SKBitmap image = SKBitmap.Decode(input) ?? throw new InvalidDataException("Unable to decode input image.");
                    Watermark.Mark(image, Watermark.Payload(key, id), strength);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                    using (SKImage marked = SKImage.FromBitmap(image))
                    using (SKData encoded = marked.Encode(SKEncodedImageFormat.Png, 100))
                    using (FileStream stream = File.Create(output))
                    {
                        encoded.SaveTo(stream);
                    }
                    Console.WriteLine($"Marked PNG: {output}");
                    Console.WriteLine($"SHA-256: {HashFile(output)}");
                    Console.WriteLine("Keep the secret key private. Test verification on the delivered copy.");
                    return 0;
                }
                case "verify":
                {
                    using SKBitmap image = SKBitmap.Decode(Required(options, "in")) ?? throw new InvalidDataException("Unable to decode suspected image.");
                    Byte[] payload = Watermark.Payload(Secret(), Required(options, "id"));
                    if (options.TryGetValue("reference", out String? reference))
                    {
                        using SKBitmap referenceImage = SKBitmap.Decode(reference) ?? throw new InvalidDataException("Unable to decode reference image.");
                        if ((Int64)image.Width * referenceImage.Height != (Int64)image.Height * referenceImage.Width)
                        {
                            throw new ArgumentException("Aspect ratio differs: this verifier cannot handle cropping or distortion.");
                        }
                        if (image.Width != referenceImage.Width || image.Height != referenceImage.Height)
                        {
                            using SKBitmap resized = image.Resize(new SKImageInfo(referenceImage.Width, referenceImage.Height), SKSamplingOptions.Default) ?? throw new InvalidOperationException("Unable to resize image.");
                            return VerifyImage(resized, payload);
                        }
                    }
                    return VerifyImage(image, payload);
                }
                case "code-fingerprint":
                {
                    var result = CodeAnalysis.Fingerprint(Required(options, "in"));
                    Console.WriteLine($"Exact SHA-256: {result.ExactSha256}");
                    Console.WriteLine($"Syntax token SHA-256: {result.SyntaxSha256}");
                    Console.WriteLine($"Tokens: {result.TokenCount}");
                    Console.WriteLine("Syntax fingerprint ignores comments and spacing; it does not prove authorship.");
                    return 0;
                }
                case "compare-code":
                {
                    var result = CodeAnalysis.Compare(Required(options, "a"), Required(options, "b"));
                    Console.WriteLine($"Syntax-identical: {result.SyntaxIdentical}");
                    Console.WriteLine($"Shared five-token shingles (Jaccard): {result.Jaccard:P2}");
                    Console.WriteLine($"Containment of smaller shingle set: {result.Containment:P2}");
                    Console.WriteLine($"Tokens A/B: {result.TokenCountA}/{result.TokenCountB}");
                    Console.WriteLine("Similarity is supporting investigative evidence only, not proof of copying.");
                    return 0;
                }
                case "archive":
                {
                    String input = Required(options, "in");
                    String output = Required(options, "out");
                    String id = Required(options, "id");
                    if (!Directory.Exists(input))
                    {
                        throw new ArgumentException("--in must be a directory.");
                    }
                    String fullInput = Path.GetFullPath(input);
                    String fullOutput = Path.GetFullPath(output);
                    String outputRelative = Path.GetRelativePath(fullInput, fullOutput);
                    if (outputRelative == "." ||
                        (outputRelative != ".." &&
                         !outputRelative.StartsWith(".." + Path.DirectorySeparatorChar, PathComparison) &&
                         !Path.IsPathRooted(outputRelative)))
                    {
                        throw new ArgumentException("Output must be outside the source directory.");
                    }
                    var files = EvidenceFiles.Enumerate(fullInput)
                        .Order(StringComparer.Ordinal)
                        .Select(file => new FileRecord(Path.GetRelativePath(fullInput, file).Replace('\\', '/'),
                            HashFile(file), new FileInfo(file).Length)).ToArray();
                    var record = new Manifest(1, id, DateTimeOffset.UtcNow, files);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
                    File.WriteAllText(fullOutput, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
                    Console.WriteLine($"Archived {files.Length} file hashes: {output}");
                    Console.WriteLine("Preserve original files, email headers, and a trustworthy external timestamp.");
                    return 0;
                }
                case "check-archive":
                {
                    String input = Required(options, "in");
                    String manifestFile = Required(options, "manifest");
                    var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestFile))
                        ?? throw new InvalidDataException("Invalid manifest.");
                    if (manifest.SchemaVersion != 1 || manifest.Files is null)
                    {
                        throw new InvalidDataException("Unsupported or invalid manifest.");
                    }

                    String root = Path.GetFullPath(input);
                    StringComparer pathComparer = OperatingSystem.IsWindows()
                        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                    HashSet<String> expected = new HashSet<String>(pathComparer);
                    Int32 matched = 0;
                    Int32 errors = 0;
                    foreach (FileRecord entry in manifest.Files)
                    {
                        String file = EvidenceFiles.Resolve(root, entry.Path);
                        String relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                        if (!expected.Add(relative))
                        {
                            throw new InvalidDataException("Manifest contains duplicate file paths.");
                        }

                        Boolean matches = File.Exists(file) &&
                            new FileInfo(file).Length == entry.Bytes &&
                            HashFile(file).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
                        Console.WriteLine($"{(matches ? "OK" : "CHANGED/MISSING")} {relative}");
                        if (matches)
                        {
                            matched++;
                        }
                        else
                        {
                            errors++;
                        }
                    }

                    Int32 extras = 0;
                    foreach (String file in EvidenceFiles.Enumerate(root))
                    {
                        String relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                        if (!expected.Contains(relative))
                        {
                            Console.WriteLine($"EXTRA {relative}");
                            extras++;
                        }
                    }

                    Console.WriteLine($"{matched}/{manifest.Files.Length} match; {extras} unrecorded files");
                    return errors == 0 && extras == 0 ? 0 : 1;
                }
                default: Usage(); return 2;
            }
        }
        catch (Exception problem)
        {
            Console.Error.WriteLine($"Error: {problem.Message}");
            return 2;
        }
    }

    private static Int32 VerifyImage(SKBitmap image, Byte[] payload)
    {
        var result = Watermark.Verify(image, payload);
        Console.WriteLine($"Matched bits: {result.MatchingBits}/{PayloadBits}");
        Console.WriteLine($"Average vote agreement: {result.Agreement:P1}");
        Console.WriteLine($"Weakest bit agreement: {result.Weakest:P1}");
        Boolean valid = result.MatchingBits == PayloadBits && result.Agreement >= .75 && result.Weakest >= .55;
        Console.WriteLine($"Result: {(valid ? "MATCH" : "NOT VERIFIED")}");
        Console.WriteLine("This is supporting evidence, not a forensic or copyright determination.");
        return valid ? 0 : 1;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static Dictionary<String, String> Parse(String[] values)
    {
        var result = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        for (Int32 index = 0; index < values.Length; index += 2)
        {
            if (!values[index].StartsWith("--") || index + 1 >= values.Length)
            {
                throw new ArgumentException("Expected --name value pairs.");
            }
            result[values[index][2..]] = values[index + 1];
        }
        return result;
    }
    private static String Required(Dictionary<String, String> options, String name) =>
        options.TryGetValue(name, out var value) && !String.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing --{name}");
    private static Double GetStrength(Dictionary<String, String> options) =>
        options.TryGetValue("strength", out var value) &&
        Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
        parsed is >= 8 and <= 60
            ? parsed : options.ContainsKey("strength") ? throw new ArgumentException("--strength must be 8..60") : 24;
    private static void CheckDistinct(String a, String b)
    {
        if (Path.GetFullPath(a).Equals(Path.GetFullPath(b), PathComparison))
        {
            throw new ArgumentException("Input and output must differ.");
        }
    }
    private static String HashFile(String path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
    private static String Secret()
    {
        String? secret = Environment.GetEnvironmentVariable("IFP_SECRET");
        if (String.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Set IFP_SECRET to a private random secret (at least 16 characters).");
        }
        return secret;
    }
    private static void Usage() => Console.WriteLine("""
        InterviewFingerprint — an experimental image fingerprint and evidence manifest tool
        Commands:
          mark          --in art.png --out marked.png --id submission-123 [--strength 24]
          verify        --in suspected.png --id submission-123 [--reference originally-marked.png]
          code-fingerprint --in file.cs
          compare-code  --a original.cs --b suspected.cs
          archive       --in submission-folder --out evidence.json --id submission-123
          check-archive --in submission-folder --manifest evidence.json
        Set secret as IFP_SECRET environment variable for mark/verify;
        protect it and your manifests; don't commit secrets or private evidence to GitHub.
        """);

}
