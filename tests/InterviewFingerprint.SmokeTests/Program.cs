using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using InterviewFingerprint.SmokeTests.Classes;
using SkiaSharp;

namespace InterviewFingerprint.SmokeTests;

internal static class Program
{
    private const String TestSecret = "TEST-ONLY-DO-NOT-USE-IN-PRODUCTION-00001";

    private static Int32 Main(String[] arguments)
    {
        if (arguments.Length != 1 &&
            (arguments.Length != 3 || arguments[1] != "--report"))
        {
            Console.Error.WriteLine("Usage: SmokeTests <path-to-InterviewFingerprint.dll> [--report path.json]");
            return 2;
        }

        String application = Path.GetFullPath(arguments[0]);
        if (!File.Exists(application))
        {
            Console.Error.WriteLine("CLI DLL not found: " + application);
            return 2;
        }

        String folder = Path.Combine(Path.GetTempPath(), "InterviewFingerprint-Smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            RunAll(application, folder);
            if (arguments.Length == 3)
            {
                RobustnessMatrix.Run(application, folder, Path.GetFullPath(arguments[2]), TestSecret);
            }

            Console.WriteLine("PASS: all CLI smoke tests");
            return 0;
        }
        catch (Exception problem)
        {
            Console.Error.WriteLine("FAIL: " + problem);
            return 1;
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void RunAll(String application, String folder)
    {
        String input = Path.Combine(folder, "original.png");
        String marked = Path.Combine(folder, "marked.png");

        using (SKBitmap bitmap = new SKBitmap(256, 256))
        {
            for (Int32 y = 0; y < bitmap.Height; y++)
            {
                for (Int32 x = 0; x < bitmap.Width; x++)
                {
                    Byte red = (Byte)(50 + (x + y / 2) % 130);
                    Byte green = (Byte)(55 + (x / 2 + y) % 120);
                    Byte blue = (Byte)(60 + (x / 3 + y / 3) % 110);
                    bitmap.SetPixel(x, y, new SKColor(red, green, blue));
                }
            }

            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream output = File.Create(input);
            data.SaveTo(output);
        }

        RequireExit(application, 0, "mark", "--in", input, "--out", marked, "--id", "submission-one", "--strength", "48");
        Require(File.Exists(marked), "mark did not produce a PNG");
        Byte[] originalMarkedBytes = File.ReadAllBytes(marked);
        RequireExit(application, 2, "mark", "--in", input, "--out", marked, "--id", "submission-one");
        Require(File.ReadAllBytes(marked).SequenceEqual(originalMarkedBytes),
            "A second mark must not overwrite an existing output image");
        // Typos and duplicate switches must fail rather than silently change evidence settings.
        String typoOutput = Path.Combine(folder, "typo-marked.png");
        RequireExit(application, 2, "mark", "--in", input, "--out", typoOutput,
            "--id", "submission-one", "--strenth", "48");
        Require(!File.Exists(typoOutput), "Unknown option must not produce a marked image");
        RequireExit(application, 2, "mark", "--in", input, "--out", typoOutput,
            "--id", "submission-one", "--id", "submission-two");
        Require(!File.Exists(typoOutput), "Duplicate option must not produce a marked image");

        RequireExit(application, 0, "verify", "--in", marked, "--id", "submission-one");
        RequireExit(application, 1, "verify", "--in", marked, "--id", "different-submission");
        RequireExit(application, 1, "verify", "--in", input, "--id", "submission-one");

        // A watermark hidden in transparent RGB can disappear when an image is composited.
        // Reject such inputs before producing misleading evidence.
        String transparent = Path.Combine(folder, "transparent.png");
        String rejectedOutput = Path.Combine(folder, "transparent-marked.png");
        using (SKBitmap translucent = new SKBitmap(256, 256))
        {
            translucent.Erase(new SKColor(60, 80, 100, 128));
            using SKImage image = SKImage.FromBitmap(translucent);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            using FileStream output = File.Create(transparent);
            data.SaveTo(output);
        }

        RequireExit(application, 2, "mark", "--in", transparent, "--out", rejectedOutput,
            "--id", "submission-one");
        Require(!File.Exists(rejectedOutput), "Rejected transparent image must not produce a marked output");
        RequireExit(application, 2, "verify", "--in", transparent, "--id", "submission-one");

        String first = Path.Combine(folder, "First.cs");
        String second = Path.Combine(folder, "Second.cs");
        File.WriteAllText(first, "class Example { Int32 Answer() { return 42; } }");
        File.WriteAllText(second, "/* comment */ class Example\n{\n    Int32 Answer() { return 42; }\n}");
        RequireExit(application, 0, "code-fingerprint", "--in", first);
        (Int32 comparisonExit, String comparisonOutput) = Execute(application, "compare-code", "--a", first, "--b", second);
        Require(comparisonExit == 0 && comparisonOutput.Contains("Syntax-identical: True", StringComparison.Ordinal),
            "comment/whitespace-only changes should preserve the syntax fingerprint: " + comparisonOutput);

        // The exact hash includes the original BOM bytes, while syntax tokens do not.
        // This also exercises the single-snapshot fingerprinting path.
        String withBom = Path.Combine(folder, "WithBom.cs");
        String withoutBom = Path.Combine(folder, "WithoutBom.cs");
        String bomSource = "class BomExample { Int32 Value = 42; }";
        File.WriteAllText(withBom, bomSource, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllText(withoutBom, bomSource, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        (Int32 bomExit, String bomOutput) = Execute(application, "code-fingerprint", "--in", withBom);
        (Int32 plainExit, String plainOutput) = Execute(application, "code-fingerprint", "--in", withoutBom);
        Require(bomExit == 0 && plainExit == 0, "BOM fingerprint commands must succeed");
        String expectedBomHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(withBom)));
        String expectedPlainHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(withoutBom)));
        Require(expectedBomHash != expectedPlainHash, "BOM must affect exact byte hashes");
        Require(bomOutput.Contains("Exact SHA-256: " + expectedBomHash, StringComparison.Ordinal),
            "BOM exact hash must cover original file bytes");
        Require(plainOutput.Contains("Exact SHA-256: " + expectedPlainHash, StringComparison.Ordinal),
            "Plain exact hash must cover original file bytes");
        (Int32 bomCompareExit, String bomCompareOutput) = Execute(application,
            "compare-code", "--a", withBom, "--b", withoutBom);
        Require(bomCompareExit == 0 &&
            bomCompareOutput.Contains("Syntax-identical: True", StringComparison.Ordinal),
            "UTF-8 BOM alone must not change syntax tokens: " + bomCompareOutput);

        // Preprocessor directives and disabled branches can change compiled behavior.
        // They must not be discarded along with ordinary comments and whitespace.
        String directiveA = Path.Combine(folder, "DirectiveA.cs");
        String directiveB = Path.Combine(folder, "DirectiveB.cs");
        File.WriteAllText(directiveA, "#if FEATURE_A\nclass Choice { Int32 Value = 1; }\n#endif\n");
        File.WriteAllText(directiveB, "#if FEATURE_B\nclass Choice { Int32 Value = 1; }\n#endif\n");
        (Int32 directiveExit, String directiveOutput) = Execute(application,
            "compare-code", "--a", directiveA, "--b", directiveB);
        Require(directiveExit == 0 &&
            directiveOutput.Contains("Syntax-identical: False", StringComparison.Ordinal),
            "Different #if directives must not have identical syntax fingerprints: " + directiveOutput);

        File.WriteAllText(directiveB, "#if FEATURE_A\nclass Choice { Int32 Value = 2; }\n#endif\n");
        (Int32 disabledExit, String disabledOutput) = Execute(application,
            "compare-code", "--a", directiveA, "--b", directiveB);
        Require(disabledExit == 0 &&
            disabledOutput.Contains("Syntax-identical: False", StringComparison.Ordinal),
            "Different disabled branches must not have identical syntax fingerprints: " + disabledOutput);

        String evidence = Path.Combine(folder, "submission");
        Directory.CreateDirectory(evidence);
        String submittedFile = Path.Combine(evidence, "submission.txt");
        File.WriteAllText(submittedFile, "original");
        File.WriteAllBytes(Path.Combine(evidence, "empty.txt"), Array.Empty<Byte>());
        String manifest = Path.Combine(folder, "manifest.json");
        RequireExit(application, 0, "archive", "--in", evidence, "--out", manifest, "--id", "submission-one");
        String originalManifestJson = File.ReadAllText(manifest);
        RequireExit(application, 2, "archive", "--in", evidence, "--out", manifest, "--id", "submission-one");
        Require(File.ReadAllText(manifest) == originalManifestJson,
            "A second archive must not overwrite an existing evidence manifest");
        RequireExit(application, 0, "check-archive", "--in", evidence + Path.DirectorySeparatorChar, "--manifest", manifest);

        // Invalid manifest metadata must fail before any file is reported as OK.
        String badHashManifest = Path.Combine(folder, "bad-hash.json");
        String badHashJson = originalManifestJson.Replace("\"Sha256\": \"", "\"Sha256\": \"Z",
            StringComparison.Ordinal);
        Require(badHashJson != originalManifestJson, "Expected SHA-256 metadata in manifest");
        File.WriteAllText(badHashManifest, badHashJson);
        (Int32 badHashExit, String badHashOutput) = Execute(application,
            "check-archive", "--in", evidence, "--manifest", badHashManifest);
        Require(badHashExit == 2 && !badHashOutput.Contains("OK ", StringComparison.Ordinal),
            "Malformed hash must fail before reporting verified files: " + badHashOutput);

        String badLengthManifest = Path.Combine(folder, "bad-length.json");
        String badLengthJson = originalManifestJson.Replace("\"Bytes\": 8", "\"Bytes\": -1",
            StringComparison.Ordinal);
        Require(badLengthJson != originalManifestJson, "Expected archived 8-byte test file");
        File.WriteAllText(badLengthManifest, badLengthJson);
        (Int32 badLengthExit, String badLengthOutput) = Execute(application,
            "check-archive", "--in", evidence, "--manifest", badLengthManifest);
        Require(badLengthExit == 2 && !badLengthOutput.Contains("OK ", StringComparison.Ordinal),
            "Negative file length must fail before reporting verified files: " + badLengthOutput);

        String extra = Path.Combine(evidence, "not-submitted.txt");
        File.WriteAllText(extra, "extra");
        RequireExit(application, 1, "check-archive", "--in", evidence, "--manifest", manifest);
        File.Delete(extra);

        String manifestJsonForSize = File.ReadAllText(manifest);
        String sizeAltered = manifestJsonForSize.Replace("\"Bytes\": 8", "\"Bytes\": 9", StringComparison.Ordinal);
        Require(sizeAltered != manifestJsonForSize, "Expected the archived test file size in JSON");
        String alteredSizeManifest = Path.Combine(folder, "altered-size.json");
        File.WriteAllText(alteredSizeManifest, sizeAltered);
        RequireExit(application, 1, "check-archive", "--in", evidence, "--manifest", alteredSizeManifest);

        RequireExit(application, 2, "archive", "--in", evidence + Path.DirectorySeparatorChar,
            "--out", Path.Combine(evidence, "inside-trailing.json"), "--id", "submission-one");

        if (!OperatingSystem.IsWindows())
        {
            String outside = Path.Combine(folder, "outside.txt");
            File.WriteAllText(outside, "outside");
            String linked = Path.Combine(evidence, "linked.txt");
            File.CreateSymbolicLink(linked, outside);
            RequireExit(application, 2, "archive", "--in", evidence, "--out",
                Path.Combine(folder, "linked-archive.json"), "--id", "submission-one");
            String linkManifest = Path.Combine(folder, "linked-manifest.json");
            File.WriteAllText(linkManifest, manifestJsonForSize.Replace("submission.txt", "linked.txt", StringComparison.Ordinal));
            RequireExit(application, 2, "check-archive", "--in", evidence, "--manifest", linkManifest);
            File.Delete(linked);

            String linkedParent = Path.Combine(folder, "linked-parent");
            Directory.CreateSymbolicLink(linkedParent, evidence);
            Directory.CreateDirectory(Path.Combine(evidence, "nested"));
            String nestedThroughLink = Path.Combine(linkedParent, "nested");
            RequireExit(application, 2, "archive", "--in", nestedThroughLink, "--out",
                Path.Combine(folder, "linked-parent-archive.json"), "--id", "submission-one");
            RequireExit(application, 2, "check-archive", "--in", nestedThroughLink, "--manifest", manifest);
            Directory.Delete(linkedParent);
        }

        String maliciousManifest = Path.Combine(folder, "malicious-manifest.json");
        String manifestJson = File.ReadAllText(manifest);
        Require(manifestJson.Contains("submission.txt", StringComparison.Ordinal), "Expected test file in manifest");
        File.WriteAllText(maliciousManifest,
            manifestJson.Replace("submission.txt", "../outside.txt", StringComparison.Ordinal));
        RequireExit(application, 2, "check-archive", "--in", evidence, "--manifest", maliciousManifest);
        RequireExit(application, 2, "archive", "--in", evidence, "--out", Path.Combine(evidence, "inside.json"), "--id", "submission-one");
        File.WriteAllText(submittedFile, "modified");
        RequireExit(application, 1, "check-archive", "--in", evidence, "--manifest", manifest);

        Console.WriteLine("PASS: image, syntax, manifest and tampering checks");
    }

    private static void RequireExit(String application, Int32 expected, params String[] arguments)
    {
        (Int32 actual, String output) = Execute(application, arguments);
        Require(actual == expected,
            "Expected exit " + expected + " but got " + actual + " for " + arguments[0] + ": " + output);
    }

    private static (Int32 ExitCode, String Output) Execute(String application, params String[] arguments)
    {
        using Process process = new Process();
        process.StartInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo.ArgumentList.Add(application);
        foreach (String argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.Environment["IFP_SECRET"] = TestSecret;
        process.Start();
        String stdout = process.StandardOutput.ReadToEnd();
        String stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(60000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("CLI process exceeded 60 seconds.");
        }

        return (process.ExitCode, stdout + stderr);
    }

    private static void Require(Boolean condition, String message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
