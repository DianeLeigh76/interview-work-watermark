using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace InterviewFingerprint;

internal static class Program
{
    private const int PayloadBits = 64;

    public static int Main(string[] arguments)
    {
        try
        {
            if (arguments.Length == 0) { Usage(); return 2; }
            string command = arguments[0].ToLowerInvariant();
            var options = Parse(arguments.Skip(1).ToArray());
            switch (command)
            {
                case "mark":
                {
                    string input = Required(options, "in");
                    string output = Required(options, "out");
                    string key = Secret();
                    string id = Required(options, "id");
                    double strength = GetStrength(options);
                    CheckDistinct(input, output);
                    using var image = Image.Load<Rgba32>(input);
                    Watermark.Mark(image, Watermark.Payload(key, id), strength);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                    image.SaveAsPng(output);
                    Console.WriteLine($"Marked PNG: {output}");
                    Console.WriteLine($"SHA-256: {HashFile(output)}");
                    Console.WriteLine("Keep the secret key private. Test verification on the delivered copy.");
                    return 0;
                }
                case "verify":
                {
                    using var image = Image.Load<Rgba32>(Required(options, "in"));
                    byte[] payload = Watermark.Payload(Secret(), Required(options, "id"));
                    if (options.TryGetValue("reference", out string? reference))
                    {
                        using var referenceImage = Image.Load<Rgba32>(reference);
                        if ((long)image.Width * referenceImage.Height != (long)image.Height * referenceImage.Width)
                            throw new ArgumentException("Aspect ratio differs: this verifier cannot handle cropping or distortion.");
                        if (image.Width != referenceImage.Width || image.Height != referenceImage.Height)
                            image.Mutate(x => x.Resize(referenceImage.Width, referenceImage.Height));
                    }
                    var result = Watermark.Verify(image, payload);
                    Console.WriteLine($"Matched bits: {result.MatchingBits}/{PayloadBits}");
                    Console.WriteLine($"Average vote agreement: {result.Agreement:P1}");
                    Console.WriteLine($"Weakest bit agreement: {result.Weakest:P1}");
                    bool valid = result.MatchingBits == PayloadBits && result.Agreement >= .75 && result.Weakest >= .55;
                    Console.WriteLine($"Result: {(valid ? "MATCH" : "NOT VERIFIED")}");
                    Console.WriteLine("This is supporting evidence, not a forensic or copyright determination.");
                    return valid ? 0 : 1;
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
                    string input = Required(options, "in");
                    string output = Required(options, "out");
                    string id = Required(options, "id");
                    if (!Directory.Exists(input)) throw new ArgumentException("--in must be a directory.");
                    string fullInput = Path.GetFullPath(input);
                    string fullOutput = Path.GetFullPath(output);
                    if (fullOutput.StartsWith(fullInput + Path.DirectorySeparatorChar, PathComparison))
                        throw new ArgumentException("Output must be outside the source directory.");
                    var files = Directory.GetFiles(fullInput, "*", SearchOption.AllDirectories)
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
                    string input = Required(options, "in");
                    string manifestFile = Required(options, "manifest");
                    var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestFile))
                        ?? throw new InvalidDataException("Invalid manifest.");
                    string root = Path.GetFullPath(input);
                    int errors = 0;
                    foreach (var entry in manifest.Files)
                    {
                        string file = Path.GetFullPath(Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
                        if (!file.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
                            throw new InvalidDataException("Manifest contains a path outside the root.");
                        bool matches = File.Exists(file) && HashFile(file).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
                        Console.WriteLine($"{(matches ? "OK" : "CHANGED/MISSING")} {entry.Path}");
                        if (!matches) errors++;
                    }
                    Console.WriteLine($"{manifest.Files.Length - errors}/{manifest.Files.Length} match");
                    return errors == 0 ? 0 : 1;
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

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static Dictionary<string, string> Parse(string[] values)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < values.Length; index += 2)
        {
            if (!values[index].StartsWith("--") || index + 1 >= values.Length)
                throw new ArgumentException("Expected --name value pairs.");
            result[values[index][2..]] = values[index + 1];
        }
        return result;
    }
    private static string Required(Dictionary<string, string> options, string name) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing --{name}");
    private static double GetStrength(Dictionary<string, string> options) =>
        options.TryGetValue("strength", out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
        parsed is >= 8 and <= 60
            ? parsed : options.ContainsKey("strength") ? throw new ArgumentException("--strength must be 8..60") : 24;
    private static void CheckDistinct(string a, string b)
    {
        if (Path.GetFullPath(a).Equals(Path.GetFullPath(b), PathComparison))
            throw new ArgumentException("Input and output must differ.");
    }
    private static string HashFile(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(file));
    }
    private static string Secret()
    {
        string? secret = Environment.GetEnvironmentVariable("IFP_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Set IFP_SECRET to a private random secret (at least 16 characters).");
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

    private sealed record FileRecord(string Path, string Sha256, long Bytes);
    private sealed record Manifest(int SchemaVersion, string SubmissionId, DateTimeOffset RecordedUtc, FileRecord[] Files);
}

internal static class Watermark
{
    private const int Side = 8;
    private const int BitCount = 64;
    private static readonly double[,] Cos = BuildCos();

    public static byte[] Payload(string key, string submissionId)
    {
        if (key.Length < 16) throw new ArgumentException("Use a secret key of at least 16 characters.");
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes("InterviewFingerprint:v1:" + submissionId))[..8];
    }

    public static void Mark(Image<Rgba32> image, byte[] payload, double strength)
    {
        if (image.Width < Side || image.Height < Side) throw new ArgumentException("Image too small.");
        int count = (image.Width / Side) * (image.Height / Side);
        if (count < BitCount * 5)
            throw new ArgumentException("Image too small. Need at least 320 full 8x8 blocks (e.g., 160x160 pixels).");
        int columns = image.Width / Side;
        for (int index = 0; index < count; index++)
        {
            int x = (index % columns) * Side;
            int y = (index / columns) * Side;
            double[,] pixels = ReadLuma(image, x, y);
            double[,] coefficients = Transform(pixels, false);
            int bit = (payload[(index % BitCount) / 8] >> (7 - ((index % BitCount) % 8))) & 1;
            double difference = coefficients[2, 3] - coefficients[3, 2];
            double wanted = bit == 1 ? strength : -strength;
            double delta = wanted - difference;
            coefficients[2, 3] += delta / 2;
            coefficients[3, 2] -= delta / 2;
            double[,] changed = Transform(coefficients, true);
            for (int row = 0; row < Side; row++)
            for (int col = 0; col < Side; col++)
            {
                Rgba32 original = image[x + col, y + row];
                double adjustment = changed[row, col] - pixels[row, col];
                image[x + col, y + row] = new Rgba32(
                    Clamp(original.R + adjustment), Clamp(original.G + adjustment),
                    Clamp(original.B + adjustment), original.A);
            }
        }
    }

    public static (int MatchingBits, double Agreement, double Weakest) Verify(Image<Rgba32> image, byte[] payload)
    {
        int columns = image.Width / Side;
        int count = columns * (image.Height / Side);
        if (count < BitCount * 5) throw new ArgumentException("Image too small to verify.");
        int[] positive = new int[BitCount];
        int[] total = new int[BitCount];
        for (int index = 0; index < count; index++)
        {
            int x = (index % columns) * Side;
            int y = (index / columns) * Side;
            double[,] coeff = Transform(ReadLuma(image, x, y), false);
            int slot = index % BitCount;
            if (coeff[2, 3] - coeff[3, 2] >= 0) positive[slot]++;
            total[slot]++;
        }
        int matched = 0;
        double agreement = 0;
        double weakest = 1;
        for (int bit = 0; bit < BitCount; bit++)
        {
            bool expected = ((payload[bit / 8] >> (7 - bit % 8)) & 1) == 1;
            bool actual = positive[bit] * 2 >= total[bit];
            if (expected == actual) matched++;
            double vote = expected ? (double)positive[bit] / total[bit] : (double)(total[bit] - positive[bit]) / total[bit];
            agreement += vote;
            weakest = Math.Min(weakest, vote);
        }
        return (matched, agreement / BitCount, weakest);
    }

    private static double[,] ReadLuma(Image<Rgba32> image, int x, int y)
    {
        var result = new double[Side, Side];
        for (int row = 0; row < Side; row++)
        for (int col = 0; col < Side; col++)
        {
            Rgba32 p = image[x + col, y + row];
            result[row, col] = .2126 * p.R + .7152 * p.G + .0722 * p.B;
        }
        return result;
    }

    private static double[,] Transform(double[,] values, bool inverse)
    {
        var output = new double[Side, Side];
        for (int u = 0; u < Side; u++)
        for (int v = 0; v < Side; v++)
        {
            double sum = 0;
            for (int x = 0; x < Side; x++)
            for (int y = 0; y < Side; y++)
            {
                if (inverse)
                    sum += Scale(x) * Scale(y) * values[x, y] * Cos[x, u] * Cos[y, v];
                else
                    sum += values[x, y] * Cos[u, x] * Cos[v, y];
            }
            output[u, v] = inverse ? sum : Scale(u) * Scale(v) * sum;
        }
        return output;
    }
    private static double Scale(int index) => index == 0 ? 1.0 / Math.Sqrt(8) : .5;
    private static double[,] BuildCos()
    {
        var result = new double[Side, Side];
        for (int u = 0; u < Side; u++)
        for (int x = 0; x < Side; x++)
            result[u, x] = Math.Cos((2 * x + 1) * u * Math.PI / 16);
        return result;
    }
    private static byte Clamp(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}