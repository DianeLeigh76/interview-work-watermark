using System.Security.Cryptography;
using System.Text;
using InterviewFingerprint.Classes;
using SkiaSharp;
using Xunit;

namespace InterviewFingerprint.Tests.Classes;

public sealed class WatermarkTests
{
    private const String TestSecret = "SYNTHETIC-TEST-SECRET-NOT-FOR-REAL-SUBMISSIONS";

    [Fact]
    public void PayloadUsesKeyedSubmissionSpecificBytes()
    {
        String submissionId = "synthetic-submission-one";
        Byte[] expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(TestSecret),
            Encoding.UTF8.GetBytes("InterviewFingerprint:v1:" + submissionId))[..8];

        Assert.Equal(expected, Watermark.Payload(TestSecret, submissionId));
        Assert.NotEqual(Convert.ToHexString(expected),
            Convert.ToHexString(Watermark.Payload(TestSecret, "synthetic-submission-two")));
    }

    [Fact]
    public void SyntheticImageRoundTripVerifiesAndWrongIdDoesNot()
    {
        using SKBitmap image = CreateSyntheticImage();
        Byte[] payload = Watermark.Payload(TestSecret, "synthetic-submission-one");
        Assert.False(Watermark.IsVerified(Watermark.Verify(image, payload)));

        Watermark.Mark(image, payload, 48.0);
        var recovered = Watermark.Verify(image, payload);
        Assert.Equal(64, recovered.MatchingBits);
        Assert.True(Watermark.IsVerified(recovered),
            $"Marked-image agreement was {recovered.Agreement:P2}, weakest vote {recovered.Weakest:P2}.");

        Byte[] wrongPayload = Watermark.Payload(TestSecret, "synthetic-submission-two");
        Assert.False(Watermark.IsVerified(Watermark.Verify(image, wrongPayload)));
    }

    [Theory]
    [InlineData(64, 0.75, 0.55, true)]
    [InlineData(64, 0.749, 0.55, false)]
    [InlineData(64, 0.90, 0.549, false)]
    [InlineData(63, 1.00, 1.00, false)]
    public void VerificationRequiresAllBitsAndBothAgreementThresholds(
        Int32 matchingBits, Double averageAgreement, Double weakestAgreement, Boolean expected)
    {
        Assert.Equal(expected, Watermark.IsVerified(
            (matchingBits, averageAgreement, weakestAgreement)));
    }

    [Fact]
    public void MarkRejectsTransparentImage()
    {
        using SKBitmap image = CreateSyntheticImage();
        image.SetPixel(0, 0, new SKColor(0, 0, 0, 0));

        Assert.Throws<ArgumentException>(() =>
            Watermark.Mark(image, Watermark.Payload(TestSecret, "transparent-test"), 48.0));
    }

    private static SKBitmap CreateSyntheticImage()
    {
        SKBitmap bitmap = new SKBitmap(256, 256);
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

        return bitmap;
    }
}
