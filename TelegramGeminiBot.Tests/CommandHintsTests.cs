using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public class CommandHintsTests
{
    [Fact]
    public void Development_UsesDotnetRunAndUserSecrets()
    {
        Assert.Equal("dotnet run --project TelegramGeminiBot -- doctor --install",
            CommandHints.Doctor(install: true, isDevelopment: true, isWindows: true));
        Assert.Equal("dotnet user-secrets --project TelegramGeminiBot set \"BotProfiles:0:Token\" \"<t>\"",
            CommandHints.SetSetting("BotProfiles:0:Token", "<t>", isDevelopment: true));
    }

    [Theory]
    [InlineData(true, @".\TelegramGeminiBot.exe doctor")]
    [InlineData(false, "./TelegramGeminiBot doctor")]
    public void Release_UsesTheExecutable(bool isWindows, string expected)
    {
        Assert.Equal(expected, CommandHints.Doctor(install: false, isDevelopment: false, isWindows: isWindows));
    }

    [Fact]
    public void Release_SettingHint_UsesEnvironmentVariableName()
    {
        var hint = CommandHints.SetSetting("BotProfiles:0:AllowedChatIds:0", "<chat id>", isDevelopment: false);

        Assert.Contains("BotProfiles__0__AllowedChatIds__0", hint);
        Assert.DoesNotContain("user-secrets", hint);
    }
}
