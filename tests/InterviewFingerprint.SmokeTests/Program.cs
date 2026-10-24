using System.Diagnostics;
using SkiaSharp;

namespace InterviewFingerprint.SmokeTests;

internal static class Program
{
    private const String TestSecret = "TEST-ONLY-DO-NOT-USE-IN-PRODUCTION-00001";

    private static Int32 Main(String[] arguments)
    {
        if (arguments.Length != 1)
        {
            Console.Error.WriteLine("Usage: SmokeTests <path-to-InterviewFingerprint.dll>");
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
        RequireExit(application, 0, "verify", "--in", marked, "--id", "submission-one");
        RequireExit(application, 1, "verify", "--in", marked, "--id", "different-submission");
        RequireExit(application, 1, "verify", "--in", input, "--id", "submission-one");

        String first = Path.Combine(folder, "First.cs");
        String second = Path.Combine(folder, "Second.cs");
        File.WriteAllText(first, "class Example { Int32 Answer() { return 42; } }");
        File.WriteAllText(second, "/* comment */ class Example\n{\n    Int32 Answer() { return 42; }\n}");
        RequireExit(application, 0, "code-fingerprint", "--in", first);
        (Int32 comparisonExit, String comparisonOutput) = Execute(application, "compare-code", "--a", first, "--b", second);
        Require(comparisonExit == 0 && comparisonOutput.Contains("Syntax-identical: True", StringComparison.Ordinal),
            "comment/whitespace-only changes should preserve the syntax fingerprint: " + comparisonOutput);

        String evidence = Path.Combine(folder, "submission");
        Directory.CreateDirectory(evidence);
        String submittedFile = Path.Combine(evidence, "submission.txt");
        File.WriteAllText(submittedFile, "original");
        String manifest = Path.Combine(folder, "manifest.json");
        RequireExit(application, 0, "archive", "--in", evidence, "--out", manifest, "--id", "submission-one");
        RequireExit(application, 0, "check-archive", "--in", evidence + Path.DirectorySeparatorChar, "--manifest", manifest);
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
