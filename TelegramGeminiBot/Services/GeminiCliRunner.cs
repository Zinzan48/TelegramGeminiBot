using System.Diagnostics;
using Microsoft.Extensions.Options;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

/// <summary>Runs prompts through Google Gemini CLI (<c>gemini</c>) in headless mode.</summary>
public sealed class GeminiCliRunner : IAiCliRunner, IDisposable
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
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                WorkingDirectory = _options.WorkingDirectory
            };

            foreach (var argument in BuildArguments(promptArgument: _options.UseHeadless ? null : prompt))
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!string.IsNullOrWhiteSpace(_options.CliHome))
            {
                startInfo.Environment["GEMINI_CLI_HOME"] = _options.CliHome;
            }

            if (!string.IsNullOrWhiteSpace(_options.GeminiApiKey))
            {
                startInfo.Environment["GEMINI_API_KEY"] = _options.GeminiApiKey;
            }

            // Never log the prompt itself: it contains users' messages and chat history.
            _logger.LogInformation("Starting Gemini CLI: {Args} (prompt length {Length})",
                string.Join(' ', BuildArguments(promptArgument: null)), prompt.Length);

            var result = await CliProcess.RunAsync(
                startInfo,
                _options.UseHeadless ? prompt : null,
                TimeSpan.FromSeconds(_options.PromptTimeoutSeconds),
                cancellationToken);

            if (result.ExitCode != 0)
            {
                var stderr = result.StandardError.Trim();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr)
                    ? $"gemini CLI exited with code {result.ExitCode}."
                    : stderr);
            }

            var output = result.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException("gemini CLI returned empty output.");
            }

            return output;
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public void Dispose() => _concurrency.Dispose();

    private List<string> BuildArguments(string? promptArgument)
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

        if (promptArgument is not null)
        {
            args.Add("--prompt");
            args.Add(promptArgument);
        }

        return args;
    }
}
