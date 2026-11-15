using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public enum AiBackend
{
    /// <summary>Google Gemini CLI (<c>gemini</c>). Needs an API key or a Code Assist Standard/Enterprise licence.</summary>
    GeminiCli,

    /// <summary>Google Antigravity CLI (<c>agy</c>).</summary>
    Antigravity
}

public enum AntigravityAuth
{
    /// <summary>Personal account sign-in stored in the OS keyring. Owner-only: the bot may answer just you.</summary>
    Account,

    /// <summary>Gemini API key (<c>modelProvider: gemini</c> + <c>GEMINI_API_KEY</c>).</summary>
    GeminiApiKey
}

public class GeminiOptions
{
    public AiBackend Backend { get; set; } = AiBackend.GeminiCli;

    /// <summary>Only used when <see cref="Backend"/> is <see cref="AiBackend.Antigravity"/>.</summary>
    public AntigravityAuth AntigravityAuth { get; set; } = AntigravityAuth.Account;

    /// <summary>
    /// Optional Gemini API key passed to the CLI as <c>GEMINI_API_KEY</c>. Never commit it:
    /// supply it through user-secrets or the <c>GeminiProfiles__0__GeminiApiKey</c> environment variable.
    /// </summary>
    public string GeminiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// CLI executable name or path. Empty means the backend default (<c>gemini</c> or <c>agy</c>),
    /// looked up on PATH (PATHEXT is honoured on Windows, so <c>gemini</c> finds <c>gemini.cmd</c>).
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>
    /// Model passed to <c>--model</c>. Leave empty to use the CLI default.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Gemini CLI only: false passes the prompt with <c>--prompt</c> instead of stdin.</summary>
    public bool UseHeadless { get; set; } = true;

    [Range(5, 600)]
    public int PromptTimeoutSeconds { get; set; } = 120;

    /// <summary>Gemini CLI only.</summary>
    public string OutputFormat { get; set; } = "text";

    /// <summary>
    /// Directory the CLI runs in. It must not be the bot's own directory, which holds
    /// configuration and the chat database. Empty means "gemini-workspace" under the content root.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Separate state folder for the CLI so the bot does not share your personal configuration,
    /// MCP servers, hooks or history. Gemini CLI receives it as <c>GEMINI_CLI_HOME</c>; agy ignores
    /// that variable, so it receives it as <c>USERPROFILE</c>/<c>HOME</c>. Empty means "agy-home"
    /// under the content root for Antigravity and no isolation for Gemini CLI; <c>none</c> disables it.
    /// </summary>
    public string CliHome { get; set; } = string.Empty;

    /// <summary>
    /// Tool lockdown file. Empty means the backend default (deny every tool):
    /// <c>gemini-policies/deny-all-tools.toml</c> (passed as <c>--policy</c>) or
    /// <c>antigravity-policies/deny-all-tools.json</c> (passed as <c>--settings</c>).
    /// Set to <c>none</c> to disable it.
    /// </summary>
    public string PolicyPath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum CLI processes running at the same time across all chats.
    /// Forced to 1 for Antigravity with account sign-in.
    /// </summary>
    [Range(1, 32)]
    public int MaxConcurrentProcesses { get; set; } = 2;
}
