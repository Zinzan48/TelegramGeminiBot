using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class BotProfile : BotOptions, INamedProfile
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
