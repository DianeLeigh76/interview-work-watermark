using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task ArchiveCheckDetectsAddedAndRemovedFiles()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String evidence = CreateSyntheticSubmission(directory.Root);
        String manifest = Path.Combine(directory.Root, "manifest.json");
        CliResult archived = await CliRunner.RunAsync(null,
            "archive", "--in", evidence, "--out", manifest, "--id", "synthetic-inventory");
        Assert.Equal(0, archived.ExitCode);

        String trustedDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest)));
        String extraFile = Path.Combine(evidence, "unrecorded.txt");
        File.WriteAllText(extraFile, "Synthetic extra file.");
        CliResult added = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest, "--sha256", trustedDigest);
        Assert.Equal(1, added.ExitCode);
        Assert.Contains("EXTRA \"unrecorded.txt\"", added.Output);
        Assert.Contains("1 unrecorded files", added.Output);

        File.Delete(extraFile);
        File.Delete(Path.Combine(evidence, "sample.txt"));
        CliResult removed = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest, "--sha256", trustedDigest);
        Assert.Equal(1, removed.ExitCode);
        Assert.Contains("CHANGED/MISSING \"sample.txt\"", removed.Output);
        Assert.Contains("0 unrecorded files", removed.Output);
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
    public async Task MalformedManifestPathsFailWithoutReportingSuccessfulEvidence()
    {
        using TemporaryDirectory directory = new TemporaryDirectory();
        String evidence = CreateSyntheticSubmission(directory.Root);
        String manifest = Path.Combine(directory.Root, "manifest.json");
        CliResult archived = await CliRunner.RunAsync(null,
            "archive", "--in", evidence, "--out", manifest, "--id", "synthetic-path-validation");
        Assert.Equal(0, archived.ExitCode);
        String originalJson = File.ReadAllText(manifest);

        JsonNode traversal = JsonNode.Parse(originalJson)!;
        traversal["Files"]!.AsArray()[0]!["Path"] = "../escape.txt";
        File.WriteAllText(manifest, traversal.ToJsonString());
        CliResult escaped = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest);
        Assert.Equal(2, escaped.ExitCode);
        Assert.Contains("non-canonical relative path", escaped.Output);
        Assert.DoesNotContain("OK \"", escaped.Output);

        JsonNode duplicate = JsonNode.Parse(originalJson)!;
        JsonArray entries = duplicate["Files"]!.AsArray();
        entries[1]!["Path"] = entries[0]!["Path"]!.GetValue<String>();
        File.WriteAllText(manifest, duplicate.ToJsonString());
        CliResult repeated = await CliRunner.RunAsync(null,
            "check-archive", "--in", evidence, "--manifest", manifest);
        Assert.Equal(2, repeated.ExitCode);
        Assert.Contains("duplicate file paths", repeated.Output);
        Assert.DoesNotContain("OK \"", repeated.Output);
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
