using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;

namespace InterviewFingerprint.SmokeTests.Classes;

/// <summary>
/// Diagnostic-only synthetic image matrix. Transform outcomes are measured, not asserted.
/// </summary>
internal static class RobustnessMatrix
{
    internal static void Run(String application, String folder, String reportPath, String secret)
    {
        // Fail before expensive diagnostics if the requested report already exists.
        if (File.Exists(reportPath))
        {
            throw new IOException("Refusing to overwrite an existing diagnostic report: " + reportPath);
        }

        List<Dictionary<String, Object>> rows = new List<Dictionary<String, Object>>();

        String[] patterns =
        [
            "gradient",
            "noise"
        ];
        foreach (String pattern in patterns)
        {
            String original = Path.Combine(folder, "robust-" + pattern + "-original.png");
            String marked = Path.Combine(folder, "robust-" + pattern + "-marked.png");
            String id = "diagnostic-" + pattern;
            CreateSyntheticImage(original, pattern);

            (Int32 markExit, String markOutput) = Execute(application, secret,
                "mark", "--in", original, "--out", marked, "--id", id, "--strength", "48");
            if (markExit != 0)
            {
                throw new InvalidOperationException("Diagnostic mark failed: " + markOutput);
            }

            Double psnr = PeakSignalToNoiseRatio(original, marked);
            AddResult(rows, application, secret, pattern, "marked-png", marked, id, true, null, psnr);
            AddResult(rows, application, secret, pattern, "unmarked-control", original, id, false, null, null);
            AddResult(rows, application, secret, pattern, "wrong-id-control", marked, id + "-wrong", false, null, null);
            AddResult(rows, application, secret + "-incorrect", pattern, "wrong-key-control", marked, id, false, null, null);

            // Unlike transformation diagnostics, these four baseline controls must always pass.
            Int32 baseline = rows.Count - 4;
            if (!(Boolean)rows[baseline]["Detected"] ||
                (Boolean)rows[baseline + 1]["Detected"] ||
                (Boolean)rows[baseline + 2]["Detected"] ||
                (Boolean)rows[baseline + 3]["Detected"])
            {
                throw new InvalidOperationException("Watermark baseline or negative-control regression for " + pattern);
            }

            Int32[] qualities = [90, 70];
            foreach (Int32 quality in qualities)
            {
                String variant = Path.Combine(folder, "robust-" + pattern + "-jpeg-" + quality + ".jpg");
                CreateVariant(marked, variant, "jpeg", quality);
                AddResult(rows, application, secret, pattern, "jpeg-" + quality, variant, id, true, null, null);
            }

            String resized = Path.Combine(folder, "robust-" + pattern + "-resized.png");
            CreateVariant(marked, resized, "resize", 0);
            AddResult(rows, application, secret, pattern, "resize-75-reference", resized, id, true, marked, null);

            String gray = Path.Combine(folder, "robust-" + pattern + "-grayscale.png");
            CreateVariant(marked, gray, "grayscale", 0);
            AddResult(rows, application, secret, pattern, "grayscale", gray, id, true, null, null);

            String cropped = Path.Combine(folder, "robust-" + pattern + "-cropped.png");
            CreateVariant(marked, cropped, "crop", 0);
            AddResult(rows, application, secret, pattern, "crop-16px", cropped, id, true, null, null);
            AddResult(rows, application, secret, pattern, "crop-16px-reference", cropped, id, true, marked, null);
            if (!(Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Reference-assisted 16px crop recovery failed for " + pattern);
            }

            AddResult(rows, application, secret, pattern, "crop-wrong-id-reference",
                cropped, id + "-wrong", false, marked, null);
            if ((Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Wrong ID was accepted for cropped image: " + pattern);
            }

            String asymmetric = Path.Combine(folder, "robust-" + pattern + "-asymmetric-crop.png");
            CreateVariant(marked, asymmetric, "crop-asymmetric", 0);
            AddResult(rows, application, secret, pattern, "crop-asymmetric-reference",
                asymmetric, id, true, marked, null);
            if (!(Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Reference-assisted asymmetric crop recovery failed for " + pattern);
            }

            String unaligned = Path.Combine(folder, "robust-" + pattern + "-unaligned-crop.png");
            CreateVariant(marked, unaligned, "crop-unaligned", 0);
            AddResult(rows, application, secret, pattern, "crop-unaligned-reference",
                unaligned, id, true, marked, null);
            if (!(Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Reference-assisted unaligned crop recovery failed for " + pattern);
            }

            AddResult(rows, application, secret, pattern, "crop-unaligned-wrong-id",
                unaligned, id + "-wrong", false, marked, null);
            if ((Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Wrong ID accepted for unaligned crop: " + pattern);
            }

            String cleanCrop = Path.Combine(folder, "robust-" + pattern + "-clean-crop.png");
            CreateVariant(original, cleanCrop, "crop-unaligned", 0);
            AddResult(rows, application, secret, pattern, "crop-unaligned-unmarked",
                cleanCrop, id, false, marked, null);
            if ((Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Unmarked crop accepted against marked reference: " + pattern);
            }

            String onePixel = Path.Combine(folder, "robust-" + pattern + "-one-pixel-crop.png");
            CreateVariant(marked, onePixel, "crop-one-pixel", 0);
            AddResult(rows, application, secret, pattern, "crop-one-pixel-reference",
                onePixel, id, true, marked, null);
            if (!(Boolean)rows[^1]["Detected"])
            {
                throw new InvalidOperationException("Reference-assisted one-pixel crop recovery failed for " + pattern);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        Object report = new
        {
            SchemaVersion = 1,
            CreatedUtc = DateTimeOffset.UtcNow,
            Description = "Synthetic diagnostic results; not calibrated error rates or forensic validation.",
            Rows = rows
        };
        // CreateNew also guards against another process creating the report
        // while diagnostics were running.
        using (FileStream stream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, report, new JsonSerializerOptions { WriteIndented = true });
        }

        Int32 detected = rows.Count(row => (Boolean)row["Detected"]);
        Int32 negativeControls = rows.Count(row => !(Boolean)row["ExpectedWatermark"]);
        Int32 falsePositives = rows.Count(row => !(Boolean)row["ExpectedWatermark"] && (Boolean)row["Detected"]);
        Console.WriteLine($"DIAGNOSTIC: {detected}/{rows.Count} detected; " +
            $"{falsePositives}/{negativeControls} synthetic negative controls detected; report: {reportPath}");
        foreach (IGrouping<String, Dictionary<String, Object>> group in
            rows.GroupBy(row => (String)row["Transform"]).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Int32 matches = group.Count(row => (Boolean)row["Detected"]);
            Console.WriteLine($"DIAGNOSTIC {group.Key}: {matches}/{group.Count()} detected");
        }
    }

    private static void AddResult(List<Dictionary<String, Object>> rows, String application, String secret,
        String pattern, String variant, String path, String id, Boolean expected, String? reference, Double? psnr)
    {
        List<String> arguments = new List<String>
        {
            "verify",
            "--in",
            path,
            "--id",
            id
        };
        if (reference is not null)
        {
            arguments.Add("--reference");
            arguments.Add(reference);
        }

        (Int32 exit, String output) = Execute(application, secret, arguments.ToArray());
        if (exit is not 0 and not 1)
        {
            throw new InvalidOperationException("Diagnostic verification failed for " + variant + ": " + output);
        }

        String? matchedLine = output.Split('\n').FirstOrDefault(line =>
            line.StartsWith("Matched bits: ", StringComparison.Ordinal));
        if (matchedLine is null ||
            !Int32.TryParse(matchedLine["Matched bits: ".Length..].Split('/')[0], out Int32 matchedBits))
        {
            throw new InvalidDataException("Diagnostic verifier output missing bit count: " + output);
        }

        Boolean detected = exit == 0 && output.Contains("Result: MATCH", StringComparison.Ordinal);
        rows.Add(new Dictionary<String, Object>
        {
            ["Pattern"] = pattern,
            ["Transform"] = variant,
            ["ExpectedWatermark"] = expected,
            ["Detected"] = detected,
            ["MatchedBitsOf64"] = matchedBits,
            ["ExitCode"] = exit,
            ["MarkedPsnrDb"] = psnr is null ? null! : psnr.Value
        });
    }

    private static void CreateSyntheticImage(String path, String pattern)
    {
        using SKBitmap bitmap = new SKBitmap(256, 256);
        Random random = new Random(20261024);
        for (Int32 y = 0; y < bitmap.Height; y++)
        {
            for (Int32 x = 0; x < bitmap.Width; x++)
            {
                Byte red;
                Byte green;
                Byte blue;
                if (pattern == "gradient")
                {
                    red = (Byte)(50 + (x + y / 2) % 130);
                    green = (Byte)(55 + (x / 2 + y) % 120);
                    blue = (Byte)(60 + (x / 3 + y / 3) % 110);
                }
                else
                {
                    red = (Byte)random.Next(48, 196);
                    green = (Byte)random.Next(48, 196);
                    blue = (Byte)random.Next(48, 196);
                }

                bitmap.SetPixel(x, y, new SKColor(red, green, blue));
            }
        }

        Save(bitmap, path, SKEncodedImageFormat.Png, 100);
    }

    private static void CreateVariant(String marked, String output, String variant, Int32 quality)
    {
        using SKBitmap source = SKBitmap.Decode(marked)
            ?? throw new InvalidDataException("Unable to decode diagnostic image.");
        if (variant == "jpeg")
        {
            Save(source, output, SKEncodedImageFormat.Jpeg, quality);
            return;
        }

        if (variant == "resize")
        {
            using SKBitmap resized = source.Resize(new SKImageInfo(192, 192), SKSamplingOptions.Default)
                ?? throw new InvalidOperationException("Unable to resize diagnostic image.");
            Save(resized, output, SKEncodedImageFormat.Png, 100);
            return;
        }

        Int32 left = variant switch
        {
            "crop" => 16,
            "crop-asymmetric" => 8,
            "crop-unaligned" => 13,
            "crop-one-pixel" => 1,
            _ => 0
        };
        Int32 top = variant switch
        {
            "crop" or "crop-asymmetric" => 16,
            "crop-unaligned" => 5,
            "crop-one-pixel" => 1,
            _ => 0
        };
        Int32 right = variant switch
        {
            "crop" or "crop-asymmetric" => 16,
            "crop-unaligned" => 11,
            _ => 0
        };
        Int32 bottom = variant switch
        {
            "crop" => 16,
            "crop-unaligned" => 7,
            _ => 0
        };
        Int32 width = source.Width - left - right;
        Int32 height = source.Height - top - bottom;
        using SKBitmap changed = new SKBitmap(width, height);
        for (Int32 y = 0; y < height; y++)
        {
            for (Int32 x = 0; x < width; x++)
            {
                SKColor pixel = source.GetPixel(x + left, y + top);
                if (variant == "grayscale")
                {
                    Byte value = (Byte)Math.Clamp(Math.Round(
                        .2126 * pixel.Red + .7152 * pixel.Green + .0722 * pixel.Blue), 0, 255);
                    pixel = new SKColor(value, value, value, pixel.Alpha);
                }

                changed.SetPixel(x, y, pixel);
            }
        }

        Save(changed, output, SKEncodedImageFormat.Png, 100);
    }

    private static Double PeakSignalToNoiseRatio(String original, String marked)
    {
        using SKBitmap first = SKBitmap.Decode(original)
            ?? throw new InvalidDataException("Unable to decode diagnostic original.");
        using SKBitmap second = SKBitmap.Decode(marked)
            ?? throw new InvalidDataException("Unable to decode diagnostic marked image.");
        Double sum = 0;
        for (Int32 y = 0; y < first.Height; y++)
        {
            for (Int32 x = 0; x < first.Width; x++)
            {
                SKColor a = first.GetPixel(x, y);
                SKColor b = second.GetPixel(x, y);
                sum += Math.Pow(a.Red - b.Red, 2) +
                    Math.Pow(a.Green - b.Green, 2) +
                    Math.Pow(a.Blue - b.Blue, 2);
            }
        }

        Double mse = sum / (first.Width * first.Height * 3);
        return mse == 0 ? 99 : Math.Round(10 * Math.Log10(255 * 255 / mse), 2);
    }

    private static void Save(SKBitmap bitmap, String path, SKEncodedImageFormat format, Int32 quality)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData encoded = image.Encode(format, quality);
        using FileStream stream = File.Create(path);
        encoded.SaveTo(stream);
    }

    private static (Int32 ExitCode, String Output) Execute(String application, String secret,
        params String[] arguments)
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

        process.StartInfo.Environment["IFP_SECRET"] = secret;
        process.Start();
        // Drain both redirected streams concurrently so a blocked child process
        // cannot prevent the timeout from being enforced.
        Task<String> stdout = process.StandardOutput.ReadToEndAsync();
        Task<String> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("Diagnostic command exceeded 120 seconds.");
        }

        return (process.ExitCode,
            stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
