using System.Text;
using TelegramGeminiBot.Storage;

namespace TelegramGeminiBot.Services;

public static class PromptBuilder
{
    public static string BuildPrompt(string userInput, IReadOnlyList<ChatMessage> history)
    {
        var builder = new StringBuilder();

        if (history.Count > 0)
        {
            builder.AppendLine("Conversation so far:");
            foreach (var item in history)
            {
                builder.AppendLine($"{item.Role}: {item.Content}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("User: " + userInput);
        builder.AppendLine("Assistant:");

        return builder.ToString();
    }
}
