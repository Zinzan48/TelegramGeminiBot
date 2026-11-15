namespace TelegramGeminiBot.Services;

/// <summary>
/// Commands shown in fix instructions. A source checkout runs in Development (user-secrets,
/// <c>dotnet run</c>); a release download runs in Production and has no project file.
/// </summary>
public static class CommandHints
{
    /// <summary>Set once at startup from the host environment.</summary>
    public static bool IsDevelopment { get; set; } = true;

    public static string Doctor(bool install = false) => Doctor(install, IsDevelopment, OperatingSystem.IsWindows());

    public static string Doctor(bool install, bool isDevelopment, bool isWindows)
    {
        var command = isDevelopment
            ? "dotnet run --project TelegramGeminiBot -- doctor"
            : (isWindows ? @".\TelegramGeminiBot.exe" : "./TelegramGeminiBot") + " doctor";
        return install ? command + " --install" : command;
    }

    public static string SetSetting(string key, string placeholder) => SetSetting(key, placeholder, IsDevelopment);

    public static string SetSetting(string key, string placeholder, bool isDevelopment) => isDevelopment
        ? $"dotnet user-secrets --project TelegramGeminiBot set \"{key}\" \"{placeholder}\""
        : $"Set the environment variable {key.Replace(":", "__")} to {placeholder}, or put it in appsettings.json next to the program.";
}
