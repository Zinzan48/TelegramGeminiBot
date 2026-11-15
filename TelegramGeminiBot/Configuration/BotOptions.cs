using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public class BotOptions
{
    /// <summary>
    /// Telegram bot token from @BotFather. Never commit it: supply it through
    /// user-secrets (Development) or the <c>BotProfiles__0__Token</c> environment variable.
    /// </summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Bot username without "@". Leave empty to use the name reported by Telegram.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    public string[] AllowedUpdates { get; set; } = ["Message"];

    public bool GroupMentionRequired { get; set; } = true;

    /// <summary>
    /// Chats (private, group or supergroup IDs) the bot is allowed to answer.
    /// When empty, the bot answers nobody unless <see cref="AllowAllChats"/> is true.
    /// </summary>
    public long[] AllowedChatIds { get; set; } = [];

    /// <summary>
    /// Explicit opt-in to answer every Telegram user. Anyone who finds the bot can then
    /// spend your Gemini quota, so keep this false unless you really mean it.
    /// </summary>
    public bool AllowAllChats { get; set; }

    /// <summary>
    /// Maximum prompts a single Telegram user may send per minute. 0 disables the limit.
    /// </summary>
    [Range(0, 1000)]
    public int MaxRequestsPerUserPerMinute { get; set; } = 10;
}
