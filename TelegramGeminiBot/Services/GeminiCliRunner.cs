using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

public sealed class GeminiCliRunner : IDisposable
{
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiCliRunner> _logger;
    private readonly SemaphoreSlim _concurrency;

    public GeminiCliRunner(IOptions<GeminiOptions> options, ILogger<GeminiCliRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
        _concurrency = new SemaphoreSlim(_options.MaxConcurrentProcesses, _options.MaxConcurrentProcesses);
    }

    public async Task<string> RunAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("Prompt is required.", nameof(prompt));
        }

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            return await RunProcessAsync(prompt, cancellationToken);
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public void Dispose() => _concurrency.Dispose();

    private async Task<string> RunProcessAsync(string prompt, CancellationToken cancellationToken)
    {
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
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _options.WorkingDirectory
        };

        if (!string.IsNullOrWhiteSpace(_options.CliHome))
        {
            startInfo.Environment["GEMINI_CLI_HOME"] = _options.CliHome;
        }

        foreach (var argument in BuildArguments(includePrompt: !_options.UseHeadless ? prompt : null))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        process.OutputDataReceived += (_, args) => { if (args.Data is not null) stdoutBuilder.AppendLine(args.Data); };
        process.ErrorDataReceived += (_, args) => { if (args.Data is not null) stderrBuilder.AppendLine(args.Data); };

        // Never log the prompt itself: it contains users' messages and chat history.
        _logger.LogInformation("Starting Gemini CLI: {Args} (prompt length {Length})",
            string.Join(' ', BuildArguments(includePrompt: null)), prompt.Length);

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

    private List<string> BuildArguments(string? includePrompt)
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

        if (!string.IsNullOrWhiteSpace(_options.PolicyPath))
        {
            args.Add("--policy");
            args.Add(_options.PolicyPath);
        }

        if (includePrompt is not null)
        {
            args.Add("--prompt");
            args.Add(includePrompt);
        }

        return args;
    }
}
