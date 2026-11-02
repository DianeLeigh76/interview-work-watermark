using System.Diagnostics;
using InterviewFingerprint.Tests.Records;

namespace InterviewFingerprint.Tests.Classes;

internal static class CliRunner
{
    internal static async Task<CliResult> RunAsync(String? secret, params String[] arguments)
    {
        using Process process = new Process();
        process.StartInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        // A project reference copies the CLI DLL, but may not copy its runtimeconfig
        // or deps file. Explicitly supply the test executable's runtime metadata
        // so the child process works from standard dotnet test output directories.
        String application = typeof(InterviewFingerprint.Program).Assembly.Location;
        String runtimeConfiguration = Path.ChangeExtension(application, ".runtimeconfig.json");
        String dependencyManifest = Path.ChangeExtension(application, ".deps.json");
        if (!File.Exists(runtimeConfiguration) || !File.Exists(dependencyManifest))
        {
            String testAssembly = typeof(CliRunner).Assembly.Location;
            runtimeConfiguration = Path.ChangeExtension(testAssembly, ".runtimeconfig.json");
            dependencyManifest = Path.ChangeExtension(testAssembly, ".deps.json");
        }

        if (!File.Exists(runtimeConfiguration) || !File.Exists(dependencyManifest))
        {
            throw new FileNotFoundException("Cannot find .NET runtime metadata for the CLI integration test.");
        }

        process.StartInfo.ArgumentList.Add("exec");
        process.StartInfo.ArgumentList.Add("--runtimeconfig");
        process.StartInfo.ArgumentList.Add(runtimeConfiguration);
        process.StartInfo.ArgumentList.Add("--depsfile");
        process.StartInfo.ArgumentList.Add(dependencyManifest);
        process.StartInfo.ArgumentList.Add(application);
        foreach (String argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (secret is null)
        {
            process.StartInfo.Environment.Remove("IFP_SECRET");
        }
        else
        {
            process.StartInfo.Environment["IFP_SECRET"] = secret;
        }

        process.Start();
        Task<String> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<String> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            throw new TimeoutException("InterviewFingerprint CLI exceeded 120 seconds.");
        }

        return new CliResult(process.ExitCode, await standardOutput + await standardError);
    }
}
