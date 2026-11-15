using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class BotProfile
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public string Token { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string[] AllowedUpdates { get; init; } = ["Message"];

    public bool GroupMentionRequired { get; init; } = true;

    /// <summary>
    /// Restrict the bot to only respond in these chat IDs.
    /// If empty, the bot responds to all chats.
    /// </summary>
    public long[] AllowedChatIds { get; init; } = [];
}
