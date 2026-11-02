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
        process.StartInfo.ArgumentList.Add(typeof(InterviewFingerprint.Program).Assembly.Location);
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
