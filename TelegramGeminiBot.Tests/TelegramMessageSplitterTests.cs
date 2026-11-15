using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public class TelegramMessageSplitterTests
{
    [Fact]
    public void ShortText_IsSingleChunk()
    {
        Assert.Equal(["hello"], TelegramMessageSplitter.Split("hello"));
    }

    [Fact]
    public void EmptyText_HasNoChunks()
    {
        Assert.Empty(TelegramMessageSplitter.Split(string.Empty));
    }

    [Fact]
    public void ReplyLongerThanTelegramLimit_IsSplitIntoChunksWithinLimit()
    {
        var lines = Enumerable.Range(0, 400).Select(i => $"line {i:D4} " + new string('x', 20));
        var text = string.Join('\n', lines);

        var chunks = TelegramMessageSplitter.Split(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.InRange(c.Length, 1, TelegramMessageSplitter.TelegramMaxLength));
        Assert.Equal(text, string.Join('\n', chunks));
    }

    [Fact]
    public void SplitPrefersLineBreak_AndKeepsNextLineIndentation()
    {
        var text = new string('a', 8) + "\n  indented";

        var chunks = TelegramMessageSplitter.Split(text, maxLength: 12);

        Assert.Equal([new string('a', 8), "  indented"], chunks);
    }

    [Fact]
    public void TextWithoutSeparators_IsHardSplit()
    {
        var text = new string('a', 25);

        var chunks = TelegramMessageSplitter.Split(text, maxLength: 10);

        Assert.Equal(["aaaaaaaaaa", "aaaaaaaaaa", "aaaaa"], chunks);
    }

    [Fact]
    public void HardSplit_NeverBreaksSurrogatePair()
    {
        var text = new string('a', 9) + "😀" + "bbb";

        var chunks = TelegramMessageSplitter.Split(text, maxLength: 10);

        Assert.Equal(text, string.Concat(chunks));
        Assert.All(chunks, c => Assert.False(char.IsHighSurrogate(c[^1])));
    }
}
