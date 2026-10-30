using InterviewFingerprint.Classes;
using InterviewFingerprint.Records;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                Usage();
                return 2;
            }
            String command = arguments[0].ToLowerInvariant();
            var options = Parse(arguments.Skip(1).ToArray());
            ValidateOptions(command, options);
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
                    String outputDirectory = Path.GetDirectoryName(Path.GetFullPath(output))!;
                    // Do not silently place a marked image through a linked output parent.
                    EvidenceFiles.RejectLinkedAncestors(outputDirectory);
                    using SKBitmap image = SKBitmap.Decode(input) ?? throw new InvalidDataException("Unable to decode input image.");
                    Watermark.Mark(image, Watermark.Payload(key, id), strength);
                    Directory.CreateDirectory(outputDirectory);
                    EvidenceFiles.RejectLinkedAncestors(outputDirectory);
                    using (SKImage marked = SKImage.FromBitmap(image))
                    using (SKData encoded = marked.Encode(SKEncodedImageFormat.Png, 100))
                    using (FileStream stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        encoded.SaveTo(stream);
                    }
                    Console.WriteLine($"Marked PNG: {JsonSerializer.Serialize(output)}");
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
                        using SKBitmap referenceImage = SKBitmap.Decode(reference)
                            ?? throw new InvalidDataException("Unable to decode reference image.");
                        var referenceResult = Watermark.Verify(referenceImage, payload);
                        if (!Watermark.IsVerified(referenceResult))
                        {
                            Console.WriteLine("Reference image does not verify with this key and submission ID.");
                            return ReportVerification(referenceResult);
                        }

                        if (image.Width != referenceImage.Width || image.Height != referenceImage.Height)
                        {
                            if (Watermark.TryVerifyCropped(image, payload, referenceImage,
                                out var croppedResult, out Int32 offsetX, out Int32 offsetY, out Int32 observedBits))
                            {
                                Console.WriteLine($"Found candidate crop offset ({offsetX}, {offsetY}); " +
                                    "this does not uniquely establish the crop position.");
                                return ReportVerification(croppedResult, observedBits);
                            }

                            if ((Int64)image.Width * referenceImage.Height !=
                                (Int64)image.Height * referenceImage.Width)
                            {
                                if (image.Width <= referenceImage.Width &&
                                    image.Height <= referenceImage.Height &&
                                    referenceImage.Width - image.Width <= 64 &&
                                    referenceImage.Height - image.Height <= 64)
                                {
                                    Console.WriteLine("No candidate crop offset met verification thresholds.");
                                    return ReportVerification(croppedResult,
                                        observedBits == 0 ? PayloadBits : observedBits);
                                }

                                throw new ArgumentException(
                                    "Aspect ratio differs and the image is outside supported crop limits.");
                            }

                            using SKBitmap resized = image.Resize(
                                new SKImageInfo(referenceImage.Width, referenceImage.Height),
                                SKSamplingOptions.Default)
                                ?? throw new InvalidOperationException("Unable to resize image.");
                            return ReportVerification(Watermark.Verify(resized, payload));
                        }
                    }

                    return ReportVerification(Watermark.Verify(image, payload));
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
                    if (result.TokenCountA < 5 || result.TokenCountB < 5)
                    {
                        Console.WriteLine("WARNING: Too few tokens for reliable five-token shingle comparison.");
                    }

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
                    // An output path through a linked parent can alias the source tree.
                    // Reject it before hashing so the manifest cannot become part of its own evidence.
                    EvidenceFiles.RejectLinkedAncestors(Path.GetDirectoryName(fullOutput)!);
                    var files = EvidenceFiles.Enumerate(fullInput)
                        .Order(StringComparer.Ordinal)
                        .Select(file =>
                        {
                            // Narrow the window for a file being swapped to a link after enumeration.
                            EvidenceFiles.RejectLinkedAncestors(file);
                            var snapshot = HashFileAndLength(file);
                            EvidenceFiles.RejectLinkedAncestors(file);
                            return new FileRecord(EvidenceFiles.ToManifestPath(fullInput, file),
                                snapshot.Sha256, snapshot.Bytes);
                        }).ToArray();
                    if (files.Length == 0)
                    {
                        throw new InvalidDataException("Cannot archive an empty evidence directory.");
                    }

                    // A file added or removed while other evidence was being hashed
                    // must not silently produce a manifest missing part of the submission.
                    // This is a best-effort inventory check, not an atomic snapshot.
                    HashSet<String> archivedPaths = new HashSet<String>(
                        files.Select(entry => entry.Path), StringComparer.Ordinal);
                    String[] finalArchivePaths = EvidenceFiles.Enumerate(fullInput)
                        .Select(file => EvidenceFiles.ToManifestPath(fullInput, file)).ToArray();
                    if (!archivedPaths.SetEquals(finalArchivePaths))
                    {
                        throw new InvalidDataException("Evidence file list changed during archive creation.");
                    }

                    var record = new Manifest(1, id, DateTimeOffset.UtcNow, files);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
                    // Recheck after creating parents: a linked directory could otherwise
                    // redirect the manifest into the evidence tree during hashing.
                    EvidenceFiles.RejectLinkedAncestors(Path.GetDirectoryName(fullOutput)!);
                    // Preserve an existing evidence manifest instead of silently replacing it.
                    using (FileStream stream = new FileStream(fullOutput, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        JsonSerializer.Serialize(stream, record, new JsonSerializerOptions { WriteIndented = true });
                    }
                    Console.WriteLine($"Archived {files.Length} file hashes: {JsonSerializer.Serialize(output)}");
                    Console.WriteLine("Preserve original files, email headers, and a trustworthy external timestamp.");
                    return 0;
                }
                case "check-archive":
                {
                    String input = Required(options, "in");
                    String manifestFile = Required(options, "manifest");
                    if (!Directory.Exists(input))
                    {
                        throw new ArgumentException("--in must be an existing directory.");
                    }

                    // Reject a linked evidence root before any manifest entry can be reported as OK.
                    EvidenceFiles.RejectLinkedAncestors(input);
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestFile));
                    RejectDuplicateJsonProperties(document.RootElement);
                    var manifest = document.RootElement.Deserialize<Manifest>(
                        new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
                        ?? throw new InvalidDataException("Invalid manifest.");
                    if (manifest.SchemaVersion != 1 || manifest.Files is null ||
                        manifest.Files.Length == 0 || String.IsNullOrWhiteSpace(manifest.SubmissionId) ||
                        manifest.RecordedUtc == default || manifest.RecordedUtc.Offset != TimeSpan.Zero)
                    {
                        throw new InvalidDataException("Unsupported or invalid manifest.");
                    }

                    String root = Path.GetFullPath(input);
                    StringComparer pathComparer = OperatingSystem.IsWindows()
                        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                    // Reject case-only duplicates even on case-sensitive Unix hosts.
                    HashSet<String> expected = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                    List<(String File, String Relative, FileRecord Record)> validated = new();

                    // Validate the entire manifest before printing any OK results.
                    // A malformed or duplicate entry must not yield partial verification output.
                    foreach (FileRecord entry in manifest.Files)
                    {
                        if (entry is null || entry.Bytes < 0 ||
                            String.IsNullOrEmpty(entry.Sha256) ||
                            entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit))
                        {
                            throw new InvalidDataException("Manifest contains invalid file metadata.");
                        }

                        String file = EvidenceFiles.Resolve(root, entry.Path);
                        String relative = EvidenceFiles.ToManifestPath(root, file);
                        if (!expected.Add(relative))
                        {
                            throw new InvalidDataException("Manifest contains duplicate file paths.");
                        }

                        validated.Add((file, relative, entry));
                    }

                    // Validate the complete filesystem snapshot, including extra names and
                    // links, before printing any successful verification records.
                    String[] actualPaths = EvidenceFiles.Enumerate(root)
                        .Select(file => EvidenceFiles.ToManifestPath(root, file)).ToArray();

                    // Finish every hash check before printing successful records.
                    // If a later file throws an I/O or link-validation error, no
                    // earlier OK line should be mistaken for a completed verification.
                    List<(String Relative, Boolean Matches)> results = new();
                    foreach (var item in validated)
                    {
                        Boolean matches = false;
                        if (File.Exists(item.File))
                        {
                            EvidenceFiles.RejectLinkedAncestors(item.File);
                            var snapshot = HashFileAndLength(item.File);
                            EvidenceFiles.RejectLinkedAncestors(item.File);
                            matches = snapshot.Bytes == item.Record.Bytes &&
                                snapshot.Sha256.Equals(item.Record.Sha256, StringComparison.OrdinalIgnoreCase);
                        }

                        results.Add((item.Relative, matches));
                    }

                    // Re-enumerate after hashing. An addition, removal or link swap
                    // during a long check must not yield an apparently complete report.
                    // This remains a best-effort check, not an atomic snapshot.
                    HashSet<String> initialPaths = new HashSet<String>(actualPaths, pathComparer);
                    HashSet<String> finalPaths = new HashSet<String>(
                        EvidenceFiles.Enumerate(root)
                            .Select(file => EvidenceFiles.ToManifestPath(root, file)), pathComparer);
                    if (!initialPaths.SetEquals(finalPaths))
                    {
                        throw new InvalidDataException("Evidence file list changed during verification.");
                    }

                    Int32 matched = 0;
                    Int32 errors = 0;
                    foreach (var result in results)
                    {
                        Console.WriteLine($"{(result.Matches ? "OK" : "CHANGED/MISSING")} {JsonSerializer.Serialize(result.Relative)}");
                        if (result.Matches)
                        {
                            matched++;
                        }
                        else
                        {
                            errors++;
                        }
                    }

                    Int32 extras = 0;
                    foreach (String relative in actualPaths)
                    {
                        if (!expected.Contains(relative))
                        {
                            Console.WriteLine($"EXTRA {JsonSerializer.Serialize(relative)}");
                            extras++;
                        }
                    }

                    Console.WriteLine($"{matched}/{manifest.Files.Length} match; {extras} unrecorded files");
                    return errors == 0 && extras == 0 ? 0 : 1;
                }
                default:
                {
                    Usage();
                    return 2;
                }
            }
        }
        catch (Exception problem)
        {
            Console.Error.WriteLine($"Error: {JsonSerializer.Serialize(problem.Message)}");
            return 2;
        }
    }

    // Duplicate JSON keys can be interpreted differently by different parsers.
    // Reject them at every level before trusting any evidence metadata.
    private static void RejectDuplicateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<String> names = new HashSet<String>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("Manifest contains duplicate JSON property names.");
                }

                RejectDuplicateJsonProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateJsonProperties(item);
            }
        }
    }

    private static Int32 ReportVerification(
        (Int32 MatchingBits, Double Agreement, Double Weakest) result, Int32 observedBits = PayloadBits)
    {
        Console.WriteLine($"Matched bits: {result.MatchingBits}/{observedBits} observed (of {PayloadBits} total)");
        Console.WriteLine($"Average vote agreement: {result.Agreement:P1}");
        Console.WriteLine($"Weakest bit agreement: {result.Weakest:P1}");
        Boolean valid = observedBits == PayloadBits
            ? Watermark.IsVerified(result)
            : Watermark.IsCropVerified(result, observedBits);
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
            String name = values[index][2..];
            if (String.IsNullOrWhiteSpace(name) || !result.TryAdd(name, values[index + 1]))
            {
                throw new ArgumentException($"Empty or duplicate option: --{name}");
            }
        }

        return result;
    }

    private static void ValidateOptions(String command, Dictionary<String, String> options)
    {
        String[] allowed = command switch
        {
            "mark" => ["in", "out", "id", "strength"],
            "verify" => ["in", "id", "reference"],
            "code-fingerprint" => ["in"],
            "compare-code" => ["a", "b"],
            "archive" => ["in", "out", "id"],
            "check-archive" => ["in", "manifest"],
            _ => []
        };

        foreach (String name in options.Keys)
        {
            if (!allowed.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Unknown option --{name} for {command}.");
            }
        }
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
        return HashFileAndLength(path).Sha256;
    }

    private static (String Sha256, Int64 Bytes) HashFileAndLength(String path)
    {
        using FileStream file = File.OpenRead(path);
        Int64 lengthBefore = file.Length;
        String digest = Convert.ToHexString(SHA256.HashData(file));
        if (file.Position != lengthBefore || file.Length != lengthBefore)
        {
            throw new IOException("Evidence file changed size while being hashed: " + path);
        }

        return (digest, lengthBefore);
    }
    private static String Secret()
    {
        String? secret = Environment.GetEnvironmentVariable("IFP_SECRET");
        if (String.IsNullOrWhiteSpace(secret) || secret.Length < 16)
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
