namespace TelegramGeminiBot.Storage;

public sealed record ChatMessage(string Role, string Content, DateTimeOffset CreatedUtc);
