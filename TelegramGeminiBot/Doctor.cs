using System.Diagnostics;
using System.Text;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;

namespace TelegramGeminiBot;

/// <summary>
/// <c>dotnet run --project TelegramGeminiBot -- doctor [--install]</c>: checks everything the bot needs
/// without starting it, prints a fix for each problem, and can run the official CLI installer.
/// </summary>
public static class Doctor
{
    public static async Task<int> RunAsync(BotProfile bot, GeminiProfile gemini, bool install, CancellationToken cancellationToken)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var info = BackendInfo.For(gemini.Backend);
        var checks = new List<PreflightCheck>
        {
            new(".NET runtime", CheckStatus.Ok, Environment.Version.ToString()),
            string.IsNullOrWhiteSpace(bot.Token)
                ? new("Telegram bot token", CheckStatus.Error, "Not configured.",
                    "dotnet user-secrets --project TelegramGeminiBot set \"BotProfiles:0:Token\" \"<token from @BotFather>\"")
                : new("Telegram bot token", CheckStatus.Ok, "Configured (value hidden)."),
            CheckChatAccess(bot)
        };

        var accessError = ProfileSetup.ValidateBackendAccess(bot, gemini);
        checks.Add(accessError is null
            ? new("Backend access rules", CheckStatus.Ok, $"{info.DisplayName}" +
                (gemini.Backend == AiBackend.Antigravity ? $", auth {gemini.AntigravityAuth}" : string.Empty))
            : new("Backend access rules", CheckStatus.Error, accessError));

        if (gemini.Backend == AiBackend.GeminiCli)
        {
            checks.Add(await CheckNodeAsync(cancellationToken));
        }

        var cliChecks = await CliPreflight.CheckBackendAsync(gemini, cancellationToken);
        checks.AddRange(cliChecks);

        Print(checks);

        var cliMissing = cliChecks.Any(c => c.Name.EndsWith("installed", StringComparison.Ordinal) && c.Status == CheckStatus.Error);
        if (install)
        {
            if (!cliMissing)
            {
                Console.WriteLine($"{info.DisplayName} is already installed; nothing to install.");
            }
            else
            {
                await TryInstallAsync(gemini.Backend, cancellationToken);
            }
        }
        else if (cliMissing)
        {
            Console.WriteLine("Tip: run `dotnet run --project TelegramGeminiBot -- doctor --install` to install it interactively.");
        }

        var errors = checks.Count(c => c.Status == CheckStatus.Error);
        Console.WriteLine(errors == 0 ? "All required checks passed." : $"{errors} problem(s) must be fixed before the bot can run.");
        return errors == 0 ? 0 : 1;
    }

    private static PreflightCheck CheckChatAccess(BotOptions bot)
    {
        if (bot.AllowedChatIds.Length > 0)
        {
            return new("Chat allow-list", CheckStatus.Ok, $"{bot.AllowedChatIds.Length} chat(s): {string.Join(", ", bot.AllowedChatIds)}");
        }

        return bot.AllowAllChats
            ? new("Chat allow-list", CheckStatus.Warning, "AllowAllChats is true: anyone who finds the bot can use it.")
            : new("Chat allow-list", CheckStatus.Warning, "AllowedChatIds is empty: the bot will ignore every chat.",
                "Start the bot, message it, copy the chat ID from the 'Ignored message from chat <id>' log line, then:" + Environment.NewLine +
                "dotnet user-secrets --project TelegramGeminiBot set \"BotProfiles:0:AllowedChatIds:0\" \"<chat id>\"");
    }

    private static async Task<PreflightCheck> CheckNodeAsync(CancellationToken cancellationToken)
    {
        const string fix = "Install a current Node.js LTS (24 or 26) from https://nodejs.org, then open a new terminal.";
        var node = CliPreflight.ResolveExecutable("node", Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATHEXT"), OperatingSystem.IsWindows());
        if (node is null)
        {
            return new("Node.js 20+", CheckStatus.Error, "node was not found on PATH.", fix);
        }

        try
        {
            var startInfo = new ProcessStartInfo { FileName = node };
            startInfo.ArgumentList.Add("--version");
            var result = await CliProcess.RunAsync(startInfo, null, TimeSpan.FromSeconds(10), cancellationToken);
            var text = result.StandardOutput.Trim();
            if (!Version.TryParse(text.TrimStart('v'), out var version) || version.Major < 20)
            {
                return new("Node.js 20+", CheckStatus.Error, $"Found {text}; Gemini CLI needs 20 or later.", fix);
            }

            // Gemini CLI still accepts Node 20, but Node 20 reached end of life on 2026-04-30.
            return version.Major < 22
                ? new("Node.js 20+", CheckStatus.Warning, $"{text} works but no longer gets security updates.", fix)
                : new("Node.js 20+", CheckStatus.Ok, text);
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new("Node.js 20+", CheckStatus.Error, "node --version failed.", fix);
        }
    }

    private static void Print(IEnumerable<PreflightCheck> checks)
    {
        Console.WriteLine();
        Console.WriteLine("TelegramGeminiBot doctor");
        Console.WriteLine(new string('-', 40));
        foreach (var check in checks)
        {
            var mark = check.Status switch { CheckStatus.Ok => "✓", CheckStatus.Warning => "!", _ => "✗" };
            Console.WriteLine($"{mark} {check.Name}: {check.Detail}");
            if (check.Status != CheckStatus.Ok && check.Fix is not null)
            {
                foreach (var line in check.Fix.Split(Environment.NewLine))
                {
                    Console.WriteLine("    " + line);
                }
            }
        }

        Console.WriteLine(new string('-', 40));
    }

    private static async Task TryInstallAsync(AiBackend backend, CancellationToken cancellationToken)
    {
        var info = BackendInfo.For(backend);
        var startInfo = BuildInstallerStartInfo(backend, out var blocker);
        if (startInfo is null)
        {
            Console.WriteLine($"Cannot install automatically: {blocker}");
            Console.WriteLine(CliPreflight.FormatInstallFix(info));
            return;
        }

        if (Console.IsInputRedirected || !Environment.UserInteractive)
        {
            Console.WriteLine("--install needs an interactive terminal. Run this yourself instead:");
            Console.WriteLine("    " + info.InstallCommand);
            return;
        }

        Console.WriteLine($"This will run the official {info.DisplayName} installer:");
        Console.WriteLine("    " + info.InstallCommand);
        Console.Write("Proceed? [y/N] ");
        var answer = Console.ReadLine();
        if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Cancelled.");
            return;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the installer.");
        await process.WaitForExitAsync(cancellationToken);

        Console.WriteLine(process.ExitCode == 0
            ? $"Installer finished. Open a new terminal and run doctor again. Then: {info.SignInHint}"
            : $"Installer exited with code {process.ExitCode}. Run it manually: {info.InstallCommand}");
    }

    private static ProcessStartInfo? BuildInstallerStartInfo(AiBackend backend, out string? blocker)
    {
        blocker = null;
        var path = Environment.GetEnvironmentVariable("PATH");
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        var isWindows = OperatingSystem.IsWindows();

        if (backend == AiBackend.GeminiCli)
        {
            var npm = CliPreflight.ResolveExecutable("npm", path, pathExt, isWindows);
            if (npm is null)
            {
                blocker = "npm was not found. Install Node.js 20+ from https://nodejs.org first.";
                return null;
            }

            var npmStart = new ProcessStartInfo { FileName = npm, UseShellExecute = false };
            foreach (var arg in new[] { "install", "-g", "@google/gemini-cli" })
            {
                npmStart.ArgumentList.Add(arg);
            }

            return npmStart;
        }

        var shell = isWindows
            ? CliPreflight.ResolveExecutable("powershell", path, pathExt, isWindows) ?? CliPreflight.ResolveExecutable("pwsh", path, pathExt, isWindows)
            : CliPreflight.ResolveExecutable("bash", path, pathExt, isWindows);
        if (shell is null)
        {
            blocker = isWindows ? "PowerShell was not found." : "bash was not found.";
            return null;
        }

        var start = new ProcessStartInfo { FileName = shell, UseShellExecute = false };
        if (isWindows)
        {
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", BackendInfo.For(backend).InstallCommand })
            {
                start.ArgumentList.Add(arg);
            }
        }
        else
        {
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add(BackendInfo.For(backend).InstallCommand);
        }

        return start;
    }
}
