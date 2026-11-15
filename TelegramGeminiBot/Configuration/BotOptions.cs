using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class BotOptions
{
    [Required]
    public string Token { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string[] AllowedUpdates { get; set; } = ["Message"];

    public bool GroupMentionRequired { get; set; } = true;

    /// <summary>
    /// Restrict the bot to only respond in these chat IDs.
    /// If empty, the bot responds to all chats.
    /// </summary>
    public long[] AllowedChatIds { get; set; } = [];
}
