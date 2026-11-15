using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public class MessageFilterTests
{
    private const string BotName = "my_bot";

    private static Message CreateMessage(long chatId, ChatType type, string text, params MessageEntity[] entities) => new()
    {
        Chat = new Chat { Id = chatId, Type = type },
        Text = text,
        Entities = entities.Length == 0 ? null : entities
    };

    [Fact]
    public void EmptyAllowList_WithoutOptIn_RejectsEveryChat()
    {
        var options = new BotOptions();

        Assert.False(MessageFilter.IsChatAllowed(123, options));
        Assert.False(MessageFilter.ShouldProcess(CreateMessage(123, ChatType.Private, "hi"), "hi", BotName, options));
    }

    [Fact]
    public void EmptyAllowList_WithAllowAllChats_AcceptsChat()
    {
        var options = new BotOptions { AllowAllChats = true };

        Assert.True(MessageFilter.IsChatAllowed(123, options));
    }

    [Fact]
    public void AllowList_TakesPrecedenceOverAllowAllChats()
    {
        var options = new BotOptions { AllowedChatIds = [1], AllowAllChats = true };

        Assert.True(MessageFilter.IsChatAllowed(1, options));
        Assert.False(MessageFilter.IsChatAllowed(2, options));
    }

    [Fact]
    public void PrivateChat_InAllowList_IsProcessedWithoutMention()
    {
        var options = new BotOptions { AllowedChatIds = [10] };

        Assert.True(MessageFilter.ShouldProcess(CreateMessage(10, ChatType.Private, "hello"), "hello", BotName, options));
    }

    [Fact]
    public void GroupChat_RequiresMention()
    {
        var options = new BotOptions { AllowedChatIds = [-100] };
        var message = CreateMessage(-100, ChatType.Supergroup, "hello everyone");

        Assert.False(MessageFilter.ShouldProcess(message, message.Text!, BotName, options));
    }

    [Fact]
    public void GroupChat_WithMention_IsProcessed()
    {
        var options = new BotOptions { AllowedChatIds = [-100] };
        const string text = "@My_Bot what is 2+2?";
        var message = CreateMessage(-100, ChatType.Group, text,
            new MessageEntity { Type = MessageEntityType.Mention, Offset = 0, Length = 7 });

        Assert.True(MessageFilter.ShouldProcess(message, text, BotName, options));
    }

    [Fact]
    public void GroupChat_WithBotCommandAddressedToBot_IsProcessed()
    {
        var options = new BotOptions { AllowedChatIds = [-100] };
        const string text = "/ask@my_bot tell me a joke";
        var message = CreateMessage(-100, ChatType.Group, text,
            new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 11 });

        Assert.True(MessageFilter.ShouldProcess(message, text, BotName, options));
    }

    [Fact]
    public void GroupChat_MentionNotRequired_IsProcessed()
    {
        var options = new BotOptions { AllowedChatIds = [-100], GroupMentionRequired = false };

        Assert.True(MessageFilter.ShouldProcess(CreateMessage(-100, ChatType.Group, "hi"), "hi", BotName, options));
    }

    [Fact]
    public void Normalize_StripsLeadingCommandAndMention()
    {
        const string text = "/ask@my_bot tell me a joke";
        var message = CreateMessage(-100, ChatType.Group, text,
            new MessageEntity { Type = MessageEntityType.BotCommand, Offset = 0, Length = 11 });

        Assert.Equal("tell me a joke", MessageFilter.Normalize(text, message, BotName));
    }

    [Fact]
    public void Normalize_StripsMentionAnywhere()
    {
        const string text = "hey @MY_BOT how are you";
        var message = CreateMessage(-100, ChatType.Group, text);

        Assert.Equal("hey  how are you", MessageFilter.Normalize(text, message, BotName));
    }
}
