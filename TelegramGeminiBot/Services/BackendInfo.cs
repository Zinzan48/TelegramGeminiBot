using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

/// <summary>
/// Per-backend facts: default executable and policy, flags the bot relies on, and official
/// install / update / sign-in instructions shown when something is missing.
/// </summary>
public sealed record BackendInfo(
    string DisplayName,
    string DefaultExecutable,
    string DefaultPolicyPath,
    IReadOnlyList<string> RequiredFlags,
    string InstallCommand,
    string? InstallPrerequisite,
    string UpdateCommand,
    string SignInHint,
    string DocsUrl)
{
    public static BackendInfo For(AiBackend backend) => backend switch
    {
        AiBackend.GeminiCli => new(
            DisplayName: "Gemini CLI",
            DefaultExecutable: "gemini",
            DefaultPolicyPath: "gemini-policies/deny-all-tools.toml",
            RequiredFlags: ["--policy", "--output-format", "--model"],
            InstallCommand: "npm install -g @google/gemini-cli",
            InstallPrerequisite: "Node.js 20+ (https://nodejs.org). Check with: node --version",
            UpdateCommand: "npm install -g @google/gemini-cli@latest",
            SignInHint: "Set GeminiApiKey (or the GEMINI_API_KEY environment variable). Personal Google sign-in stopped working on 2026-06-18.",
            DocsUrl: "https://github.com/google-gemini/gemini-cli"),

        AiBackend.Antigravity => new(
            DisplayName: "Antigravity CLI (agy)",
            DefaultExecutable: "agy",
            DefaultPolicyPath: "antigravity-policies/deny-all-tools.json",
            RequiredFlags: ["--settings", "--input-format", "--output-format", "--sandbox", "--disable-slash-commands"],
            InstallCommand: OperatingSystem.IsWindows()
                ? "irm https://antigravity.google/cli/install.ps1 | iex"
                : "curl -fsSL https://antigravity.google/cli/install.sh | bash",
            InstallPrerequisite: OperatingSystem.IsWindows() ? "Run the command in PowerShell, then open a new terminal." : "Open a new terminal afterwards.",
            UpdateCommand: "agy update",
            SignInHint: "Run `agy` once interactively on this machine, as the account that runs the bot, to sign in.",
            DocsUrl: "https://antigravity.google/docs/getting-started"),

        _ => throw new ArgumentOutOfRangeException(nameof(backend), backend, null)
    };
}
