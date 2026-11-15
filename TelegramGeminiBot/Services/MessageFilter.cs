using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramGeminiBot.Configuration;

namespace TelegramGeminiBot.Services;

/// <summary>
/// Decides which Telegram messages the bot answers and strips the bot mention/command from them.
/// </summary>
public static class MessageFilter
{
    public static bool IsChatAllowed(long chatId, BotOptions options)
    {
        if (options.AllowedChatIds.Length > 0)
        {
            return options.AllowedChatIds.Contains(chatId);
        }

        // Fail closed: an empty allow-list answers nobody unless the operator opted in explicitly.
        return options.AllowAllChats;
    }

    public static bool ShouldProcess(Message message, string text, string botUsername, BotOptions options)
    {
        if (!IsChatAllowed(message.Chat.Id, options))
        {
            return false;
        }

        if (message.Chat.Type == ChatType.Private || !options.GroupMentionRequired)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(botUsername))
        {
            return false;
        }

        var mention = "@" + botUsername;
        if (text.Contains(mention, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entities = message.Entities ?? message.CaptionEntities;
        if (entities is null)
        {
            return false;
        }

        foreach (var entity in entities)
        {
            var value = SafeSubstring(text, entity.Offset, entity.Length);
            if (entity.Type == MessageEntityType.Mention && string.Equals(value, mention, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (entity.Type == MessageEntityType.BotCommand && value.Contains(mention, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string Normalize(string text, Message message, string botUsername)
    {
        var normalized = text;
        var entities = message.Entities ?? message.CaptionEntities;

        var leadingCommand = entities?.FirstOrDefault(e => e.Type == MessageEntityType.BotCommand && e.Offset == 0);
        if (leadingCommand is not null)
        {
            normalized = SafeSubstring(normalized, leadingCommand.Length, normalized.Length - leadingCommand.Length);
        }

        if (!string.IsNullOrWhiteSpace(botUsername))
        {
            normalized = normalized.Replace("@" + botUsername, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return normalized.Trim();
    }

    private static string SafeSubstring(string text, int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset >= text.Length)
        {
            return string.Empty;
        }

        return text.Substring(offset, Math.Min(length, text.Length - offset));
    }
}
