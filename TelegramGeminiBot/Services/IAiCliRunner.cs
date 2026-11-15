namespace TelegramGeminiBot.Services;

/// <summary>Runs one prompt through an AI CLI and returns its text answer.</summary>
public interface IAiCliRunner
{
    Task<string> RunAsync(string prompt, CancellationToken cancellationToken);
}
