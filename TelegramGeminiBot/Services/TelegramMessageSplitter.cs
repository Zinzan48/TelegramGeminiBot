namespace TelegramGeminiBot.Services;

/// <summary>
/// Splits long replies into chunks that fit Telegram's 4096-character message limit,
/// preferring line breaks, then spaces, and never splitting a surrogate pair.
/// </summary>
public static class TelegramMessageSplitter
{
    public const int TelegramMaxLength = 4096;

    public static IReadOnlyList<string> Split(string text, int maxLength = TelegramMaxLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 2);

        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var chunks = new List<string>();
        var remaining = text.AsSpan();

        while (remaining.Length > maxLength)
        {
            var window = remaining[..maxLength];
            var cut = window.LastIndexOf('\n');
            if (cut < maxLength / 2)
            {
                cut = window.LastIndexOf(' ');
            }

            // Drop exactly the separator we split on, so indentation on the next line survives.
            var skip = 1;
            if (cut < maxLength / 2)
            {
                cut = maxLength;
                skip = 0;
                if (char.IsHighSurrogate(remaining[cut - 1]))
                {
                    cut--;
                }
            }

            var chunk = remaining[..cut].TrimEnd('\r');
            if (!chunk.IsWhiteSpace())
            {
                chunks.Add(chunk.ToString());
            }

            remaining = remaining[(cut + skip)..];
        }

        if (!remaining.IsEmpty)
        {
            chunks.Add(remaining.ToString());
        }

        return chunks;
    }
}
