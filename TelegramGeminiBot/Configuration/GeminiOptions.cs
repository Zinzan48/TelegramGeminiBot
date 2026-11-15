using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class GeminiOptions
{
    [Required]
    public string ExecutablePath { get; set; } = "gemini";

    [Required]
    public string Model { get; set; } = "gemini 3.0 pro";

    public bool UseHeadless { get; set; } = true;

    [Range(5, 600)]
    public int PromptTimeoutSeconds { get; set; } = 120;

    public string OutputFormat { get; set; } = "text";

    /// <summary>
    /// Working directory for the Gemini CLI process to avoid defaulting to System32 when running as SYSTEM.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;
}
