using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public class GeminiOptions
{
    [Required]
    public string ExecutablePath { get; set; } = "gemini";

    /// <summary>
    /// Model passed to <c>--model</c>. Leave empty to use the Gemini CLI default.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    public bool UseHeadless { get; set; } = true;

    [Range(5, 600)]
    public int PromptTimeoutSeconds { get; set; } = 120;

    public string OutputFormat { get; set; } = "text";

    /// <summary>
    /// Directory the Gemini CLI runs in. It must not be the bot's own directory, which holds
    /// configuration and the chat database. Empty means "gemini-workspace" under the content root.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// When set, passed to the CLI as <c>GEMINI_CLI_HOME</c> so the bot uses its own
    /// <c>.gemini</c> folder instead of inheriting your personal MCP servers, hooks and history.
    /// </summary>
    public string CliHome { get; set; } = string.Empty;

    /// <summary>
    /// Policy file or directory passed to <c>--policy</c>. The default denies every tool so
    /// Telegram users cannot make the CLI read files, run commands or call MCP servers.
    /// Set to empty only if your Gemini CLI version does not support <c>--policy</c>.
    /// </summary>
    public string PolicyPath { get; set; } = "gemini-policies/deny-all-tools.toml";

    /// <summary>
    /// Maximum Gemini CLI processes running at the same time across all chats.
    /// </summary>
    [Range(1, 32)]
    public int MaxConcurrentProcesses { get; set; } = 2;
}
