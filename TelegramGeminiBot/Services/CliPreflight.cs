using System.Diagnostics;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

public enum CheckStatus { Ok, Warning, Error }

/// <param name="Fix">Copy-paste instructions shown when the check is not OK.</param>
public sealed record PreflightCheck(string Name, CheckStatus Status, string Detail, string? Fix = null);

/// <summary>
/// Verifies that the configured AI CLI is installed, recent enough and configured to authenticate,
/// and explains how to install or fix it when it is not.
/// </summary>
public static class CliPreflight
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Finds an executable by absolute/relative path or on PATH. On Windows, names without an
    /// extension are tried with each PATHEXT extension (so "gemini" finds "gemini.cmd").
    /// </summary>
    public static string? ResolveExecutable(string nameOrPath, string? pathVariable, string? pathExtVariable, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            return null;
        }

        var extensions = isWindows && !Path.HasExtension(nameOrPath)
            ? (pathExtVariable ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [];

        IEnumerable<string> Candidates(string basePath) => extensions.Length == 0
            ? [basePath]
            : extensions.Select(ext => basePath + ext.ToLowerInvariant()).Prepend(basePath);

        if (Path.IsPathRooted(nameOrPath) || nameOrPath.Contains(Path.DirectorySeparatorChar) || nameOrPath.Contains(Path.AltDirectorySeparatorChar))
        {
            return Candidates(Path.GetFullPath(nameOrPath)).FirstOrDefault(IsFile);
        }

        foreach (var directory in (pathVariable ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed;
            try
            {
                trimmed = Path.GetFullPath(directory.Trim().Trim('"'));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            var match = Candidates(Path.Combine(trimmed, nameOrPath)).FirstOrDefault(IsFile);
            if (match is not null)
            {
                return match;
            }
        }

        return null;

        // On Windows, a bare extension-less file (e.g. npm's POSIX shim "gemini") is not runnable.
        bool IsFile(string path) => File.Exists(path) && (!isWindows || Path.HasExtension(path));
    }

    public static IReadOnlyList<string> MissingFlags(string helpText, IEnumerable<string> requiredFlags) =>
        requiredFlags.Where(flag => !helpText.Contains(flag, StringComparison.Ordinal)).ToList();

    public static string FormatInstallFix(BackendInfo info)
    {
        var lines = new List<string>();
        if (info.InstallPrerequisite is not null)
        {
            lines.Add("Prerequisite: " + info.InstallPrerequisite);
        }

        lines.Add("Install: " + info.InstallCommand);
        lines.Add("Docs: " + info.DocsUrl);
        lines.Add("Or run: dotnet run --project TelegramGeminiBot -- doctor --install");
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Runs every check for the configured backend. Never throws for a failed check.</summary>
    public static async Task<IReadOnlyList<PreflightCheck>> CheckBackendAsync(GeminiOptions options, CancellationToken cancellationToken)
    {
        var info = BackendInfo.For(options.Backend);
        var checks = new List<PreflightCheck>();

        var resolved = ResolveExecutable(
            options.ExecutablePath,
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATHEXT"),
            OperatingSystem.IsWindows());

        if (resolved is null)
        {
            checks.Add(new PreflightCheck($"{info.DisplayName} installed", CheckStatus.Error,
                $"'{options.ExecutablePath}' was not found on PATH.", FormatInstallFix(info)));
            return checks;
        }

        options.ExecutablePath = resolved;

        var version = await ProbeAsync(resolved, "--version", options, cancellationToken);
        checks.Add(version is null
            ? new PreflightCheck($"{info.DisplayName} installed", CheckStatus.Error,
                $"Found {resolved}, but '--version' failed or timed out.", FormatInstallFix(info))
            : new PreflightCheck($"{info.DisplayName} installed", CheckStatus.Ok, $"{resolved} (version {FirstLine(version)})"));

        if (version is null)
        {
            return checks;
        }

        var help = await ProbeAsync(resolved, "--help", options, cancellationToken) ?? string.Empty;
        var missing = MissingFlags(help, info.RequiredFlags);
        checks.Add(missing.Count == 0
            ? new PreflightCheck("Required CLI flags", CheckStatus.Ok, string.Join(", ", info.RequiredFlags))
            : new PreflightCheck("Required CLI flags", CheckStatus.Error,
                $"This version does not support: {string.Join(", ", missing)}.", "Update: " + info.UpdateCommand));

        checks.Add(CheckPolicy(options));
        checks.Add(CheckAuth(options, info));
        return checks;
    }

    private static PreflightCheck CheckPolicy(GeminiOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PolicyPath))
        {
            return new PreflightCheck("Tool lockdown policy", CheckStatus.Warning,
                "PolicyPath is 'none': Telegram users can make the CLI use tools (read files, run commands).",
                "Remove PolicyPath from your configuration to use the default deny-all policy.");
        }

        return File.Exists(options.PolicyPath) || Directory.Exists(options.PolicyPath)
            ? new PreflightCheck("Tool lockdown policy", CheckStatus.Ok, options.PolicyPath)
            : new PreflightCheck("Tool lockdown policy", CheckStatus.Error, $"'{options.PolicyPath}' does not exist.",
                "Restore the file from the repository or remove PolicyPath to use the default.");
    }

    private static PreflightCheck CheckAuth(GeminiOptions options, BackendInfo info)
    {
        var hasKey = !string.IsNullOrWhiteSpace(options.GeminiApiKey)
                     || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GEMINI_API_KEY"));

        return options.Backend switch
        {
            AiBackend.GeminiCli when hasKey =>
                new PreflightCheck("Authentication", CheckStatus.Ok, "Gemini API key is configured."),
            AiBackend.GeminiCli =>
                new PreflightCheck("Authentication", CheckStatus.Warning,
                    "No Gemini API key found. This only works with a Code Assist Standard/Enterprise sign-in.", info.SignInHint),
            AiBackend.Antigravity when options.AntigravityAuth == AntigravityAuth.GeminiApiKey =>
                !string.IsNullOrWhiteSpace(options.GeminiApiKey)
                    ? new PreflightCheck("Authentication", CheckStatus.Ok, "Gemini API key is configured (modelProvider: gemini).")
                    : new PreflightCheck("Authentication", CheckStatus.Error, "AntigravityAuth is GeminiApiKey but GeminiApiKey is empty.",
                        "dotnet user-secrets --project TelegramGeminiBot set \"GeminiProfiles:0:GeminiApiKey\" \"<key>\""),
            _ =>
                new PreflightCheck("Authentication", CheckStatus.Warning,
                    "Account sign-in cannot be verified without spending quota; a missing sign-in is reported on the first message.",
                    info.SignInHint)
        };
    }

    private static async Task<string?> ProbeAsync(string executable, string argument, GeminiOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo { FileName = executable };
            startInfo.ArgumentList.Add(argument);
            if (Directory.Exists(options.WorkingDirectory))
            {
                startInfo.WorkingDirectory = options.WorkingDirectory;
            }

            var result = await CliProcess.RunAsync(startInfo, standardInput: null, ProbeTimeout, cancellationToken);
            var text = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
            // Some CLIs print usage to stderr with a non-zero exit code; accept any output for --help.
            return result.ExitCode == 0 || (argument == "--help" && text.Length > 0) ? text : null;
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string FirstLine(string text) => text.Split('\n', 2)[0].Trim();
}
