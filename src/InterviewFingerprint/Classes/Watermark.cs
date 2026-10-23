using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

    public static void Mark(Image<Rgba32> image, Byte[] payload, Double strength)
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
                    Rgba32 original = image[x + col, y + row];
                    Double adjustment = changed[row, col] - pixels[row, col];
                    image[x + col, y + row] = new Rgba32(
                        Clamp(original.R + adjustment),
                        Clamp(original.G + adjustment),
                        Clamp(original.B + adjustment),
                        original.A);
                }
            }
        }
    }

    public static (Int32 MatchingBits, Double Agreement, Double Weakest) Verify(Image<Rgba32> image, Byte[] payload)
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
            Int32 x = (index % columns) * Side;
            Int32 y = (index / columns) * Side;
            Double[,] coeff = Transform(ReadLuma(image, x, y), false);
            Int32 slot = index % BitCount;
            if (coeff[2, 3] - coeff[3, 2] >= 0)
            {
                positive[slot]++;
            }

            total[slot]++;
        }

        Int32 matched = 0;
        Double agreement = 0;
        Double weakest = 1;
        for (Int32 bit = 0; bit < BitCount; bit++)
        {
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

        return (matched, agreement / BitCount, weakest);
    }

    private static Double[,] ReadLuma(Image<Rgba32> image, Int32 x, Int32 y)
    {
        Double[,] result = new Double[Side, Side];
        for (Int32 row = 0; row < Side; row++)
        {
            for (Int32 col = 0; col < Side; col++)
            {
                Rgba32 pixel = image[x + col, y + row];
                result[row, col] = .2126 * pixel.R + .7152 * pixel.G + .0722 * pixel.B;
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
