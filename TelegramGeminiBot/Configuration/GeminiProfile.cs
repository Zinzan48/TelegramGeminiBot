using System.ComponentModel.DataAnnotations;

namespace TelegramGeminiBot.Configuration;

public sealed class GeminiProfile : GeminiOptions, INamedProfile
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
