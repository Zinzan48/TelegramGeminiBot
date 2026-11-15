using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class GeminiProfile
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string ExecutablePath { get; init; } = "gemini";

    [Required]
    public string Model { get; init; } = "gemini 3.0 pro";

    public bool UseHeadless { get; init; } = true;

    [Range(5, 600)]
    public int PromptTimeoutSeconds { get; init; } = 120;

    public string OutputFormat { get; init; } = "text";

    public string WorkingDirectory { get; init; } = string.Empty;
}
