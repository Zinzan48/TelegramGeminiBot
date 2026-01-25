using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class BotOptions
{
    [Required]
    public string Token { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string[] AllowedUpdates { get; init; } = ["Message"];

    public bool GroupMentionRequired { get; init; } = true;
}
