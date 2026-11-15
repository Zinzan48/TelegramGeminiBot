using System.Diagnostics;
using Microsoft.Extensions.Options;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

/// <summary>
/// Runs prompts through Antigravity CLI (<c>agy</c>) in headless stream-json mode. The prompt goes
/// through stdin, so it never appears on the command line and is not limited by its length.
/// </summary>
public sealed class AntigravityCliRunner : IAiCliRunner, IDisposable
{
    private readonly GeminiOptions _options;
    private readonly ILogger<AntigravityCliRunner> _logger;
    private readonly SemaphoreSlim _concurrency;
    private string? _settingsJson;

    public AntigravityCliRunner(IOptions<GeminiOptions> options, ILogger<AntigravityCliRunner> logger)
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

            foreach (var argument in BuildArguments())
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (_options.AntigravityAuth == AntigravityAuth.GeminiApiKey)
            {
                startInfo.Environment["GEMINI_API_KEY"] = _options.GeminiApiKey;
            }

            if (!string.IsNullOrWhiteSpace(_options.CliHome))
            {
                // agy ignores GEMINI_CLI_HOME but keeps its state under the user profile directory.
                startInfo.Environment["USERPROFILE"] = _options.CliHome;
                startInfo.Environment["HOME"] = _options.CliHome;
            }

            _logger.LogInformation("Starting Antigravity CLI (prompt length {Length}).", prompt.Length);

            var result = await CliProcess.RunAsync(
                startInfo,
                AntigravityStreamJson.BuildInputLine(prompt) + "\n",
                TimeSpan.FromSeconds(_options.PromptTimeoutSeconds),
                cancellationToken);

            var parsed = AntigravityStreamJson.ParseResult(result.StandardOutput.Split('\n'));
            if (parsed is { IsSuccess: true } && !string.IsNullOrWhiteSpace(parsed.Response))
            {
                return parsed.Response.Trim();
            }

            var reason = parsed?.Error ?? result.StandardError.Trim();
            if (reason.Contains("authentication required", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Antigravity CLI is not signed in. " + BackendInfo.For(AiBackend.Antigravity).SignInHint);
            }

            throw new InvalidOperationException(
                $"Antigravity CLI failed (exit {result.ExitCode}, status {parsed?.Status ?? "no result"}): {reason}");
        }
        finally
        {
            _concurrency.Release();
        }
    }

    public void Dispose() => _concurrency.Dispose();

    private List<string> BuildArguments()
    {
        _settingsJson ??= AntigravityStreamJson.BuildSettings(
            string.IsNullOrWhiteSpace(_options.PolicyPath) ? null : File.ReadAllText(_options.PolicyPath),
            _options.AntigravityAuth == AntigravityAuth.GeminiApiKey);

        var args = new List<string>
        {
            "--input-format", "stream-json",
            "--output-format", "stream-json",
            "--disable-slash-commands",
            "--sandbox",
            "--settings", _settingsJson
        };

        if (!string.IsNullOrWhiteSpace(_options.Model))
        {
            args.Add("--model");
            args.Add(_options.Model);
        }

        return args;
    }
}
