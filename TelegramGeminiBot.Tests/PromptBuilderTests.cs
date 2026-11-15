using TelegramGeminiBot.Services;
using TelegramGeminiBot.Storage;

namespace TelegramGeminiBot.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void WithoutHistory_ContainsOnlyCurrentTurn()
    {
        var prompt = PromptBuilder.BuildPrompt("hi", []);

        Assert.DoesNotContain("Conversation so far", prompt);
        Assert.Contains("User: hi", prompt);
        Assert.EndsWith("Assistant:" + Environment.NewLine, prompt);
    }

    [Fact]
    public void WithHistory_ListsTurnsInOrderBeforeCurrentTurn()
    {
        var now = DateTimeOffset.UtcNow;
        var prompt = PromptBuilder.BuildPrompt("next", [new("User", "first", now), new("Assistant", "reply", now)]);

        var first = prompt.IndexOf("User: first", StringComparison.Ordinal);
        var reply = prompt.IndexOf("Assistant: reply", StringComparison.Ordinal);
        var current = prompt.IndexOf("User: next", StringComparison.Ordinal);

        Assert.True(first >= 0 && first < reply && reply < current);
    }
}
