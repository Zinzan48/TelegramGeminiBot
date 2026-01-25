using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class ChatHistoryOptions
{
    [Required]
    public string DatabasePath { get; init; } = "data\\chat-history.db";

    [Range(1, 200)]
    public int MaxMessages { get; init; } = 20;
}
