using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;
using TelegramGeminiBot.Storage;

namespace TelegramGeminiBot;

public sealed class Worker : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<Worker> _logger;
    private readonly BotOptions _botOptions;
    private readonly ChatHistoryOptions _historyOptions;
    private readonly ChatHistoryStore _historyStore;
    private readonly GeminiCliRunner _gemini;
    private readonly ChatQueue _chatQueue;
    private string _botUsername = string.Empty;
    private UpdateType[] _allowedUpdates = [UpdateType.Message];

    public Worker(
        ITelegramBotClient botClient,
        ILogger<Worker> logger,
        IOptions<BotOptions> botOptions,
        IOptions<ChatHistoryOptions> historyOptions,
        ChatHistoryStore historyStore,
        GeminiCliRunner gemini,
        ChatQueue chatQueue)
    {
        _botClient = botClient;
        _logger = logger;
        _botOptions = botOptions.Value;
        _historyOptions = historyOptions.Value;
        _historyStore = historyStore;
        _gemini = gemini;
        _chatQueue = chatQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _historyStore.InitializeAsync(stoppingToken);

        var me = await _botClient.GetMe();
        _botUsername = NormalizeUsername(_botOptions.Username, me.Username);
        _allowedUpdates = BuildAllowedUpdates();

        if (string.IsNullOrWhiteSpace(_botUsername))
        {
            _logger.LogWarning("Bot username is not available; group mention checks will fail.");
        }

        _logger.LogInformation("Bot connected as {Username} ({Id}).", me.Username, me.Id);

        int? offset = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            Update[] updates;
            try
            {
                updates = await _botClient.GetUpdates(offset: offset, timeout: 30, allowedUpdates: _allowedUpdates, cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll updates.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            foreach (var update in updates)
            {
                offset = update.Id + 1;

                try
                {
                    await HandleUpdateAsync(update, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to handle update {UpdateId}.", update.Id);
                }

                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Message is null)
        {
            return;
        }

        var message = update.Message;
        var text = message.Text ?? message.Caption;

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!ShouldProcessMessage(message, text))
        {
            return;
        }

        text = NormalizeMessageText(text, message);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var gate = await _chatQueue.AcquireAsync(message.Chat.Id, cancellationToken);

        var history = await _historyStore.GetRecentMessagesAsync(message.Chat.Id, _historyOptions.MaxMessages, cancellationToken);
        var prompt = PromptBuilder.BuildPrompt(text, history);

        string response;
        try
        {
            response = await _gemini.RunAsync(prompt, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini CLI timed out for chat {ChatId}.", message.Chat.Id);
            await _botClient.SendMessage(message.Chat, "Gemini CLI timed out. Please try again.", cancellationToken: cancellationToken);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini CLI error for chat {ChatId}.", message.Chat.Id);
            await _botClient.SendMessage(message.Chat, "Gemini CLI error. Please try again later.", cancellationToken: cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        await _historyStore.AddMessageAsync(message.Chat.Id, "User", text, now, cancellationToken);
        await _historyStore.AddMessageAsync(message.Chat.Id, "Assistant", response, now, cancellationToken);
        await _historyStore.TrimHistoryAsync(message.Chat.Id, _historyOptions.MaxMessages, cancellationToken);

        await _botClient.SendMessage(message.Chat, response, cancellationToken: cancellationToken);
    }

    private bool ShouldProcessMessage(Message message, string text)
    {
        if (message.Chat.Type == ChatType.Private)
        {
            return true;
        }

        if (!_botOptions.GroupMentionRequired)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_botUsername))
        {
            return false;
        }

        var mention = "@" + _botUsername;

        if (text.Contains(mention, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var entities = message.Entities ?? message.CaptionEntities;
        if (entities is null)
        {
            return false;
        }

        foreach (var entity in entities)
        {
            if (entity.Type == MessageEntityType.Mention)
            {
                var value = SafeSubstring(text, entity.Offset, entity.Length);
                if (string.Equals(value, mention, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (entity.Type == MessageEntityType.BotCommand)
            {
                var value = SafeSubstring(text, entity.Offset, entity.Length);
                if (value.Contains(mention, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private string NormalizeMessageText(string text, Message message)
    {
        var normalized = text;
        var entities = message.Entities ?? message.CaptionEntities;

        if (entities is not null)
        {
            foreach (var entity in entities)
            {
                if (entity.Type == MessageEntityType.BotCommand && entity.Offset == 0)
                {
                    normalized = SafeSubstring(normalized, entity.Length, normalized.Length - entity.Length).Trim();
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_botUsername))
        {
            normalized = normalized.Replace("@" + _botUsername, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return normalized.Trim();
    }

    private UpdateType[] BuildAllowedUpdates()
    {
        if (_botOptions.AllowedUpdates.Length == 0)
        {
            return [UpdateType.Message];
        }

        var updates = new List<UpdateType>();
        foreach (var raw in _botOptions.AllowedUpdates)
        {
            if (Enum.TryParse(raw, true, out UpdateType parsed))
            {
                updates.Add(parsed);
            }
            else
            {
                _logger.LogWarning("Unknown update type: {UpdateType}", raw);
            }
        }

        return updates.Count == 0 ? [UpdateType.Message] : updates.ToArray();
    }

    private static string NormalizeUsername(string configured, string? actual)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimStart('@');
        }

        return string.IsNullOrWhiteSpace(actual) ? string.Empty : actual.TrimStart('@');
    }

    private static string SafeSubstring(string text, int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset >= text.Length)
        {
            return string.Empty;
        }

        var safeLength = Math.Min(length, text.Length - offset);
        return text.Substring(offset, safeLength);
    }
}
