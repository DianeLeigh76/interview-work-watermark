using System.Security.Cryptography;
using System.Text.Json;
using InterviewFingerprint.Tests.Records;
using Xunit;

namespace InterviewFingerprint.Tests.Classes;

public sealed class ArchiveCliTests
{
    [Fact]
    public async Task ArchiveRoundTripVerifiesIndependentHashesAndDetectsTampering()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String evidence = CreateSyntheticSubmission(directory.Root);
        String manifest = Path.Combine(directory.Root, "manifest.json");
        CliResult archived = await CliRunner.RunAsync(null,
            "archive", "--in", evidence, "--out", manifest, "--id", "synthetic-archive-one");
        Assert.Equal(0, archived.ExitCode);
        Assert.True(File.Exists(manifest), archived.Output);

        Byte[] manifestBytes = File.ReadAllBytes(manifest);
        String digest = Convert.ToHexString(SHA256.HashData(manifestBytes));
        Assert.Contains("Manifest SHA-256: " + digest, archived.Output);
        using (JsonDocument document = JsonDocument.Parse(manifestBytes))
        {
            JsonElement files = document.RootElement.GetProperty("Files");
            Assert.Equal(2, files.GetArrayLength());
            foreach (JsonElement entry in files.EnumerateArray())
            {
                String relative = entry.GetProperty("Path").GetString()!;
                String path = Path.Combine(evidence, relative.Replace('/', Path.DirectorySeparatorChar));
                Byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)),
                    entry.GetProperty("Sha256").GetString());
                Assert.Equal((Int64)bytes.Length, entry.GetProperty("Bytes").GetInt64());
            }
        }

        CliResult verified = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest, "--sha256", digest);
        Assert.Equal(0, verified.ExitCode);
        Assert.Contains("2/2 match; 0 unrecorded files", verified.Output);

        File.AppendAllText(Path.Combine(evidence, "sample.txt"), "Deliberate change.\n");
        CliResult tampered = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest, "--sha256", digest);
        Assert.Equal(1, tampered.ExitCode);
        Assert.Contains("CHANGED/MISSING \"sample.txt\"", tampered.Output);
    }

    [Fact]
    public async Task ChangedManifestDigestFailsBeforeReportingAnyMatches()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String evidence = CreateSyntheticSubmission(directory.Root);
        String manifest = Path.Combine(directory.Root, "manifest.json");
        CliResult archived = await CliRunner.RunAsync(null,
            "archive", "--in", evidence, "--out", manifest, "--id", "synthetic-archive-two");
        Assert.Equal(0, archived.ExitCode);

        String trustedDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest)));
        File.AppendAllText(manifest, "\n");
        CliResult verified = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest, "--sha256", trustedDigest);
        Assert.Equal(2, verified.ExitCode);
        Assert.Contains("Manifest SHA-256 does not match", verified.Output);
        Assert.DoesNotContain("OK \"", verified.Output);
    }

    [Fact]
    public async Task ArchiveDoesNotReplacePublishedManifest()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String evidence = CreateSyntheticSubmission(directory.Root);
        String manifest = Path.Combine(directory.Root, "manifest.json");
        CliResult first = await CliRunner.RunAsync(null,
            "archive", "--in", evidence, "--out", manifest, "--id", "synthetic-archive-three");
        Assert.Equal(0, first.ExitCode);
        Byte[] original = File.ReadAllBytes(manifest);
        CliResult second = await CliRunner.RunAsync(null,
            "archive", "--in", evidence, "--out", manifest, "--id", "synthetic-archive-three");
        Assert.NotEqual(0, second.ExitCode);
        Assert.Equal(original, File.ReadAllBytes(manifest));
    }

    private static String CreateSyntheticSubmission(String root)
    {
        String evidence = Path.Combine(root, "submission");
        Directory.CreateDirectory(evidence);
        foreach (String filename in new[] { "sample.txt", "sample.pdf" })
        {
            String fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", filename);
            File.Copy(fixture, Path.Combine(evidence, filename));
        }

        return evidence;
    }
}
