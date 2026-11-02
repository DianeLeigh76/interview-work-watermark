using System.Security.Cryptography;
using InterviewFingerprint.Tests.Records;
using SkiaSharp;
using Xunit;

namespace InterviewFingerprint.Tests.Classes;

public sealed class ImageCliTests
{
    private const String TestSecret = "SYNTHETIC-TEST-SECRET-NOT-FOR-REAL-SUBMISSIONS";

    [Fact]
    public async Task MarkedPngCanBeDecodedAndVerifiedByCli()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String input = Path.Combine(directory.Root, "synthetic.png");
        String marked = Path.Combine(directory.Root, "marked.png");
        CreateSyntheticPng(input);

        CliResult created = await CliRunner.RunAsync(TestSecret,
            "mark", "--in", input, "--out", marked, "--id", "synthetic-image-one",
            "--strength", "48");
        Assert.Equal(0, created.ExitCode);
        Assert.True(File.Exists(marked), created.Output);
        Assert.Contains("SHA-256: " +
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(marked))), created.Output);

        using SKBitmap markedImage = SKBitmap.Decode(marked)
            ?? throw new InvalidDataException("Marked output is not a decodable PNG.");
        Assert.Equal(256, markedImage.Width);
        Assert.Equal(256, markedImage.Height);

        CliResult verified = await CliRunner.RunAsync(TestSecret,
            "verify", "--in", marked, "--id", "synthetic-image-one");
        Assert.Equal(0, verified.ExitCode);
        Assert.Contains("Result: MATCH", verified.Output);

        CliResult wrongId = await CliRunner.RunAsync(TestSecret,
            "verify", "--in", marked, "--id", "synthetic-image-two");
        Assert.Equal(1, wrongId.ExitCode);
        Assert.Contains("Result: NOT VERIFIED", wrongId.Output);
    }

    private static void CreateSyntheticPng(String path)
    {
        using SKBitmap bitmap = new SKBitmap(256, 256);
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
        using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, encoded.ToArray());
    }
}
