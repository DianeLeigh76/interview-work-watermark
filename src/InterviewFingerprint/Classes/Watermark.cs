using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace InterviewFingerprint.Classes;

internal static class Watermark
{
    private const Int32 Side = 8;
    private const Int32 BitCount = 64;
    private static readonly Double[,] Cos = BuildCos();

    public static Byte[] Payload(String key, String submissionId)
    {
        if (key.Length < 16)
        {
            throw new ArgumentException("Use a secret key of at least 16 characters.");
        }

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),
            Encoding.UTF8.GetBytes("InterviewFingerprint:v1:" + submissionId))[..8];
    }

    public static void Mark(SKBitmap image, Byte[] payload, Double strength)
    {
        if (image.Width < Side || image.Height < Side)
        {
            throw new ArgumentException("Image too small.");
        }

        Int32 count = (image.Width / Side) * (image.Height / Side);
        if (count < BitCount * 5)
        {
            throw new ArgumentException("Image too small. Need at least 320 full 8x8 blocks.");
        }

        EnsureOpaque(image);
        Int32 columns = image.Width / Side;
        for (Int32 index = 0; index < count; index++)
        {
            Int32 x = (index % columns) * Side;
            Int32 y = (index / columns) * Side;
            Double[,] pixels = ReadLuma(image, x, y);
            Double[,] coefficients = Transform(pixels, false);
            Int32 bit = (payload[(index % BitCount) / 8] >> (7 - ((index % BitCount) % 8))) & 1;
            Double difference = coefficients[2, 3] - coefficients[3, 2];
            Double wanted = bit == 1 ? strength : -strength;
            Double delta = wanted - difference;
            coefficients[2, 3] += delta / 2;
            coefficients[3, 2] -= delta / 2;
            Double[,] changed = Transform(coefficients, true);

            for (Int32 row = 0; row < Side; row++)
            {
                for (Int32 col = 0; col < Side; col++)
                {
                    SKColor original = image.GetPixel(x + col, y + row);
                    Double adjustment = changed[row, col] - pixels[row, col];
                    image.SetPixel(x + col, y + row, new SKColor(
                        Clamp(original.Red + adjustment),
                        Clamp(original.Green + adjustment),
                        Clamp(original.Blue + adjustment),
                        original.Alpha));
                }
            }
        }
    }

    public static (Int32 MatchingBits, Double Agreement, Double Weakest) Verify(SKBitmap image, Byte[] payload)
    {
        EnsureOpaque(image);
        var result = VerifyAligned(image, payload, image.Width / Side, 0, 0);
        return (result.MatchingBits, result.Agreement, result.Weakest);
    }

    internal static Boolean IsVerified((Int32 MatchingBits, Double Agreement, Double Weakest) result)
    {
        return result.MatchingBits == BitCount && result.Agreement >= .75 && result.Weakest >= .55;
    }

    internal static Boolean IsCropVerified(
        (Int32 MatchingBits, Double Agreement, Double Weakest) result, Int32 observedBits)
    {
        return observedBits >= 48 && result.MatchingBits == observedBits &&
            result.Agreement >= .75 && result.Weakest >= .55;
    }

    /// <summary>
    /// Searches for 8-pixel-aligned cropping using the marked reference's original block grid.
    /// A matching offset is a candidate, not a unique determination of where cropping occurred.
    /// </summary>
    internal static Boolean TryVerifyCropped(SKBitmap image, Byte[] payload, SKBitmap reference,
        out (Int32 MatchingBits, Double Agreement, Double Weakest) result,
        out Int32 offsetX, out Int32 offsetY, out Int32 observedBits)
    {
        result = default;
        observedBits = 0;
        offsetX = 0;
        offsetY = 0;
        Int32 widthDifference = reference.Width - image.Width;
        Int32 heightDifference = reference.Height - image.Height;
        if (widthDifference < 0 || heightDifference < 0 ||
            (widthDifference == 0 && heightDifference == 0) ||
            widthDifference > 64 || heightDifference > 64)
        {
            return false;
        }

        EnsureOpaque(image);
        Int32 originalColumns = reference.Width / Side;
        for (Int32 top = 0; top <= heightDifference; top += Side)
        {
            for (Int32 left = 0; left <= widthDifference; left += Side)
            {
                var candidate = VerifyAligned(image, payload, originalColumns, left / Side, top / Side);
                var votes = (candidate.MatchingBits, candidate.Agreement, candidate.Weakest);
                if (IsCropVerified(votes, candidate.ObservedBits))
                {
                    result = votes;
                    observedBits = candidate.ObservedBits;
                    offsetX = left;
                    offsetY = top;
                    return true;
                }
            }
        }

        return false;
    }

    private static (Int32 MatchingBits, Double Agreement, Double Weakest, Int32 ObservedBits) VerifyAligned(
        SKBitmap image, Byte[] payload, Int32 originalColumns, Int32 offsetBlocksX, Int32 offsetBlocksY)
    {
        Int32 columns = image.Width / Side;
        Int32 count = columns * (image.Height / Side);
        if (count < BitCount * 5)
        {
            throw new ArgumentException("Image too small to verify.");
        }

        Int32[] positive = new Int32[BitCount];
        Int32[] total = new Int32[BitCount];

        for (Int32 index = 0; index < count; index++)
        {
            Int32 blockX = index % columns;
            Int32 blockY = index / columns;
            Int32 slot = ((blockY + offsetBlocksY) * originalColumns +
                blockX + offsetBlocksX) % BitCount;
            if (CoefficientDifference(image, blockX * Side, blockY * Side) >= 0)
            {
                positive[slot]++;
            }

            total[slot]++;
        }

        Int32 matched = 0;
        Int32 observed = 0;
        Double agreement = 0;
        Double weakest = 1;
        for (Int32 bit = 0; bit < BitCount; bit++)
        {
            if (total[bit] == 0)
            {
                continue;
            }

            observed++;
            Boolean expected = ((payload[bit / 8] >> (7 - bit % 8)) & 1) == 1;
            Boolean actual = positive[bit] * 2 >= total[bit];
            if (expected == actual)
            {
                matched++;
            }

            Double vote = expected
                ? (Double)positive[bit] / total[bit]
                : (Double)(total[bit] - positive[bit]) / total[bit];
            agreement += vote;
            weakest = Math.Min(weakest, vote);
        }

        return (matched, agreement / observed, weakest, observed);
    }

    private static void EnsureOpaque(SKBitmap image)
    {
        for (Int32 y = 0; y < image.Height; y++)
        {
            for (Int32 x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).Alpha != Byte.MaxValue)
                {
                    throw new ArgumentException(
                        "Images containing transparency must be flattened against an opaque background before marking or verification.");
                }
            }
        }
    }

    /// <summary>
    /// Computes only DCT[2,3] - DCT[3,2], avoiding a full 8x8 DCT during verification.
    /// </summary>
    private static Double CoefficientDifference(SKBitmap image, Int32 x, Int32 y)
    {
        Double difference = 0;
        for (Int32 row = 0; row < Side; row++)
        {
            for (Int32 col = 0; col < Side; col++)
            {
                SKColor pixel = image.GetPixel(x + col, y + row);
                Double luma = .2126 * pixel.Red + .7152 * pixel.Green + .0722 * pixel.Blue;
                difference += luma * (Cos[2, row] * Cos[3, col] - Cos[3, row] * Cos[2, col]);
            }
        }

        return Scale(2) * Scale(3) * difference;
    }

    private static Double[,] ReadLuma(SKBitmap image, Int32 x, Int32 y)
    {
        Double[,] result = new Double[Side, Side];
        for (Int32 row = 0; row < Side; row++)
        {
            for (Int32 col = 0; col < Side; col++)
            {
                SKColor pixel = image.GetPixel(x + col, y + row);
                result[row, col] = .2126 * pixel.Red + .7152 * pixel.Green + .0722 * pixel.Blue;
            }
        }

        return result;
    }

    private static Double[,] Transform(Double[,] values, Boolean inverse)
    {
        Double[,] output = new Double[Side, Side];

        for (Int32 u = 0; u < Side; u++)
        {
            for (Int32 v = 0; v < Side; v++)
            {
                Double sum = 0;
                for (Int32 x = 0; x < Side; x++)
                {
                    for (Int32 y = 0; y < Side; y++)
                    {
                        if (inverse)
                        {
                            sum += Scale(x) * Scale(y) * values[x, y] * Cos[x, u] * Cos[y, v];
                        }
                        else
                        {
                            sum += values[x, y] * Cos[u, x] * Cos[v, y];
                        }
                    }
                }

                output[u, v] = inverse ? sum : Scale(u) * Scale(v) * sum;
            }
        }

        return output;
    }

    private static Double Scale(Int32 index)
    {
        return index == 0 ? 1.0 / Math.Sqrt(8) : .5;
    }

    private static Double[,] BuildCos()
    {
        Double[,] result = new Double[Side, Side];
        for (Int32 u = 0; u < Side; u++)
        {
            for (Int32 x = 0; x < Side; x++)
            {
                result[u, x] = Math.Cos((2 * x + 1) * u * Math.PI / 16);
            }
        }

        return result;
    }

    private static Byte Clamp(Double value)
    {
        return (Byte)Math.Clamp(Math.Round(value), 0, 255);
    }
}
