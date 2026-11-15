using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Tests;

public class ProfileSetupTests
{
    private static GeminiOptions AgyAccount() => new() { Backend = AiBackend.Antigravity, AntigravityAuth = AntigravityAuth.Account };

    [Fact]
    public void AgyAccount_WithExactlyOnePrivateChat_IsAllowed()
    {
        Assert.Null(ProfileSetup.ValidateBackendAccess(new BotOptions { AllowedChatIds = [12345] }, AgyAccount()));
    }

    [Theory]
    [InlineData(new long[0])]
    [InlineData(new long[] { 1, 2 })]
    [InlineData(new long[] { -1001234567890 })]
    public void AgyAccount_RejectsAnythingButOneOwnerChat(long[] chatIds)
    {
        var error = ProfileSetup.ValidateBackendAccess(new BotOptions { AllowedChatIds = chatIds }, AgyAccount());

        Assert.NotNull(error);
        Assert.Contains("account sharing", error);
    }

    [Fact]
    public void AgyAccount_RejectsAllowAllChats()
    {
        var error = ProfileSetup.ValidateBackendAccess(new BotOptions { AllowedChatIds = [1], AllowAllChats = true }, AgyAccount());

        Assert.NotNull(error);
    }

    [Fact]
    public void AgyApiKey_CanServeGroupsAndManyChats()
    {
        var gemini = new GeminiOptions { Backend = AiBackend.Antigravity, AntigravityAuth = AntigravityAuth.GeminiApiKey, GeminiApiKey = "k" };

        Assert.Null(ProfileSetup.ValidateBackendAccess(new BotOptions { AllowedChatIds = [1, 2, -100] }, gemini));
    }

    [Fact]
    public void AgyApiKey_WithoutKey_IsRejected()
    {
        var gemini = new GeminiOptions { Backend = AiBackend.Antigravity, AntigravityAuth = AntigravityAuth.GeminiApiKey };

        Assert.NotNull(ProfileSetup.ValidateBackendAccess(new BotOptions { AllowedChatIds = [1] }, gemini));
    }

    [Fact]
    public void GeminiCli_HasNoOwnerOnlyRestriction()
    {
        Assert.Null(ProfileSetup.ValidateBackendAccess(new BotOptions { AllowAllChats = true }, new GeminiOptions()));
    }

    [Fact]
    public void ApplyDefaults_FillsBackendExecutableAndPolicy_AndForcesSingleProcessForAgyAccount()
    {
        var root = Path.Combine(Path.GetTempPath(), "tgb-root");
        var gemini = AgyAccount();
        gemini.MaxConcurrentProcesses = 4;

        ProfileSetup.ApplyGeminiDefaults(gemini, root, root, createDirectories: false);

        Assert.Equal("agy", gemini.ExecutablePath);
        Assert.EndsWith(Path.Combine("antigravity-policies", "deny-all-tools.json"), gemini.PolicyPath);
        Assert.Equal(1, gemini.MaxConcurrentProcesses);
        Assert.Equal(Path.Combine(root, "gemini-workspace"), gemini.WorkingDirectory);
    }

    [Fact]
    public void ApplyDefaults_PolicyNone_DisablesPolicy()
    {
        var root = Path.Combine(Path.GetTempPath(), "tgb-root");
        var gemini = new GeminiOptions { PolicyPath = "none" };

        ProfileSetup.ApplyGeminiDefaults(gemini, root, root, createDirectories: false);

        Assert.Equal(string.Empty, gemini.PolicyPath);
        Assert.Equal("gemini", gemini.ExecutablePath);
    }

    [Fact]
    public void ApplyDefaults_RejectsWorkingDirectoryContainingBotFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "tgb-root");
        var gemini = new GeminiOptions { WorkingDirectory = "." };

        Assert.Throws<InvalidOperationException>(() => ProfileSetup.ApplyGeminiDefaults(gemini, root, root, createDirectories: false));
    }
}
