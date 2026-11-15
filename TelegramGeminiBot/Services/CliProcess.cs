using System.Diagnostics;
using System.Text;

namespace TelegramGeminiBot.Services;

public sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Starts a CLI process, optionally writes stdin, and collects stdout/stderr with a hard timeout.
/// On timeout or cancellation the whole process tree is killed and <see cref="OperationCanceledException"/> is thrown.
/// </summary>
public static class CliProcess
{
    public static async Task<CliProcessResult> RunAsync(
        ProcessStartInfo startInfo,
        string? standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (stdout) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (stderr) stderr.AppendLine(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start '{startInfo.FileName}'.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), linkedCts.Token);
                await process.StandardInput.FlushAsync(linkedCts.Token);
            }

            process.StandardInput.Close();
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Already exited.
            }

            if (ex is IOException && !linkedCts.IsCancellationRequested)
            {
                throw; // stdin broke because the process died early; surface it.
            }

            throw new OperationCanceledException("CLI process timed out or was cancelled.", ex, linkedCts.Token);
        }

        string output, error;
        lock (stdout) output = stdout.ToString();
        lock (stderr) error = stderr.ToString();
        return new CliProcessResult(process.ExitCode, output, error);
    }
}
