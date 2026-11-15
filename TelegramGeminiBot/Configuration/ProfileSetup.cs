using System.ComponentModel.DataAnnotations;
using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Configuration;

/// <summary>Resolves the active profiles, fills backend defaults and validates the result.</summary>
public static class ProfileSetup
{
    public static TProfile ResolveProfile<TProfile>(TProfile[] profiles, string? requestedName) where TProfile : INamedProfile
    {
        var kind = typeof(TProfile).Name;
        if (profiles.Length == 0)
        {
            throw new InvalidOperationException($"No {kind} entries found in configuration.");
        }

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return profiles[0];
        }

        if (requestedName.Contains(','))
        {
            throw new InvalidOperationException(
                $"Only one active {kind} is supported, but ActiveProfiles lists '{requestedName}'. Run one bot instance per token.");
        }

        return profiles.FirstOrDefault(p => string.Equals(p.Name, requestedName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Active profile '{requestedName}' not found for {kind}. Available: {string.Join(", ", profiles.Select(p => p.Name))}");
    }

    public static void ValidateAnnotations(object profile)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(profile, new ValidationContext(profile), results, validateAllProperties: true))
        {
            throw new InvalidOperationException(
                $"Invalid {profile.GetType().Name}: {string.Join("; ", results.Select(r => r.ErrorMessage))}");
        }
    }

    /// <summary>
    /// Returns null when the bot/backend combination is allowed, otherwise the reason it is not.
    /// Antigravity with a personal account may only serve its owner: sharing access with anyone else
    /// counts as account sharing under our reading of the Antigravity terms.
    /// </summary>
    public static string? ValidateBackendAccess(BotOptions bot, GeminiOptions gemini)
    {
        if (gemini.Backend == AiBackend.Antigravity && gemini.AntigravityAuth == AntigravityAuth.GeminiApiKey
            && string.IsNullOrWhiteSpace(gemini.GeminiApiKey))
        {
            return "AntigravityAuth is GeminiApiKey but GeminiApiKey is empty. Set it with user-secrets or the GeminiProfiles__0__GeminiApiKey environment variable.";
        }

        if (gemini.Backend != AiBackend.Antigravity || gemini.AntigravityAuth != AntigravityAuth.Account)
        {
            return null;
        }

        const string why = "Antigravity with a personal account sign-in is for its owner only; letting anyone else use it counts as account sharing. " +
                           "Use AntigravityAuth=GeminiApiKey (or the GeminiCli backend with an API key) to serve other people.";

        if (bot.AllowAllChats)
        {
            return "AllowAllChats must be false. " + why;
        }

        if (bot.AllowedChatIds.Length != 1)
        {
            return $"AllowedChatIds must contain exactly one chat ID (yours), but it has {bot.AllowedChatIds.Length}. " + why;
        }

        if (bot.AllowedChatIds[0] <= 0)
        {
            return $"AllowedChatIds[0] = {bot.AllowedChatIds[0]} is a group or channel. Use your private chat ID. " + why;
        }

        return null;
    }

    /// <summary>
    /// Fills backend defaults and resolves paths. Does not touch the file system unless
    /// <paramref name="createDirectories"/> is true (doctor runs with false).
    /// </summary>
    public static void ApplyGeminiDefaults(GeminiOptions gemini, string contentRoot, string appBaseDirectory, bool createDirectories)
    {
        var info = BackendInfo.For(gemini.Backend);

        if (string.IsNullOrWhiteSpace(gemini.ExecutablePath))
        {
            gemini.ExecutablePath = info.DefaultExecutable;
        }

        if (gemini.Backend == AiBackend.Antigravity && gemini.AntigravityAuth == AntigravityAuth.Account)
        {
            // One task at a time: every run spends the same personal 5-hour/weekly quota.
            gemini.MaxConcurrentProcesses = 1;
        }

        // The CLI must never run inside the bot's own folder: that folder holds the bot token
        // (appsettings / secrets) and every chat's history database.
        var workingDirectory = string.IsNullOrWhiteSpace(gemini.WorkingDirectory)
            ? Path.Combine(contentRoot, "gemini-workspace")
            : Path.GetFullPath(gemini.WorkingDirectory, contentRoot);

        if (IsSameOrParent(workingDirectory, contentRoot) || IsSameOrParent(workingDirectory, appBaseDirectory))
        {
            throw new InvalidOperationException(
                $"WorkingDirectory '{workingDirectory}' contains the bot's own files. Point it to a dedicated empty directory.");
        }

        gemini.WorkingDirectory = workingDirectory;

        if (string.Equals(gemini.CliHome, "none", StringComparison.OrdinalIgnoreCase))
        {
            gemini.CliHome = string.Empty;
        }
        else if (string.IsNullOrWhiteSpace(gemini.CliHome) && gemini.Backend == AiBackend.Antigravity)
        {
            // Verified with agy 1.5.9: a separate USERPROFILE/HOME keeps the keyring sign-in working
            // while keeping bot conversations and "knowledge" out of the owner's personal agy state.
            gemini.CliHome = Path.Combine(contentRoot, "agy-home");
        }

        if (!string.IsNullOrWhiteSpace(gemini.CliHome))
        {
            gemini.CliHome = Path.GetFullPath(gemini.CliHome, contentRoot);
        }

        if (string.Equals(gemini.PolicyPath, "none", StringComparison.OrdinalIgnoreCase))
        {
            gemini.PolicyPath = string.Empty;
        }
        else
        {
            var policy = string.IsNullOrWhiteSpace(gemini.PolicyPath) ? info.DefaultPolicyPath : gemini.PolicyPath;
            gemini.PolicyPath = Path.GetFullPath(policy, appBaseDirectory);
        }

        if (createDirectories)
        {
            Directory.CreateDirectory(gemini.WorkingDirectory);
            if (!string.IsNullOrWhiteSpace(gemini.CliHome))
            {
                Directory.CreateDirectory(gemini.CliHome);
            }
        }
    }

    public static bool IsSameOrParent(string candidate, string protectedPath)
    {
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)) + Path.DirectorySeparatorChar;
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedPath)) + Path.DirectorySeparatorChar;
        return b.StartsWith(a, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}
