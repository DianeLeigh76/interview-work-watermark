namespace InterviewFingerprint.Tests.Classes;

internal sealed class TemporaryDirectory : IDisposable
{
    internal String Root { get; }

    internal TemporaryDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "InterviewFingerprint-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
