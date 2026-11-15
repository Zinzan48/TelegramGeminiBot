using Microsoft.Extensions.Options;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

/// <summary>
/// Runs before <see cref="Worker"/>. Stops the host and logs install or fix instructions when the
/// configured AI CLI is missing or unusable.
/// </summary>
public sealed class PreflightHostedService(IOptions<GeminiOptions> options, ILogger<PreflightHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var checks = await CliPreflight.CheckBackendAsync(options.Value, cancellationToken);

        foreach (var check in checks)
        {
            switch (check.Status)
            {
                case CheckStatus.Ok:
                    logger.LogInformation("Preflight OK: {Name}: {Detail}", check.Name, check.Detail);
                    break;
                case CheckStatus.Warning:
                    logger.LogWarning("Preflight warning: {Name}: {Detail}{NewLine}{Fix}", check.Name, check.Detail, Environment.NewLine, check.Fix);
                    break;
                default:
                    logger.LogError("Preflight failed: {Name}: {Detail}{NewLine}{Fix}", check.Name, check.Detail, Environment.NewLine, check.Fix);
                    break;
            }
        }

        var errors = checks.Where(c => c.Status == CheckStatus.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Preflight failed: " + string.Join("; ", errors.Select(e => $"{e.Name}: {e.Detail}")) +
                $". Run `{CommandHints.Doctor()}` for step-by-step instructions.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
