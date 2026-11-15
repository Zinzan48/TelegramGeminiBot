using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

public sealed class GeminiCliRunner
{
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiCliRunner> _logger;

    public GeminiCliRunner(IOptions<GeminiOptions> options, ILogger<GeminiCliRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> RunAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        var arguments = BuildArguments(prompt);

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = _options.WorkingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            stdoutBuilder.AppendLine(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            stderrBuilder.AppendLine(args.Data);
        };

        _logger.LogInformation("Starting gemini CLI with args: {args}", string.Join(' ', arguments));

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start gemini CLI process.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (_options.UseHeadless)
        {
            await process.StandardInput.WriteAsync(prompt.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }

        process.StandardInput.Close();

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.PromptTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill gemini CLI process after timeout.");
            }

            throw;
        }

        if (process.ExitCode != 0)
        {
            var stderr = stderrBuilder.ToString().Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                ? $"gemini CLI exited with code {process.ExitCode}."
                : stderr);
        }

        var output = stdoutBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("gemini CLI returned empty output.");
        }

        return output;
    }

    private IReadOnlyList<string> BuildArguments(string prompt)
    {
        var args = new List<string>();

        if (!string.IsNullOrWhiteSpace(_options.Model))
        {
            args.Add("--model");
            args.Add(_options.Model);
        }

        if (!string.IsNullOrWhiteSpace(_options.OutputFormat))
        {
            args.Add("--output-format");
            args.Add(_options.OutputFormat);
        }

        if (!_options.UseHeadless)
        {
            args.Add("--prompt");
            args.Add(prompt);
        }

        return args;
    }
}
