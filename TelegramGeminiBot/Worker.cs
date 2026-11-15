using System.Collections.Concurrent;
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
    private readonly IAiCliRunner _gemini;
    private readonly ChatQueue _chatQueue;
    private readonly UserRateLimiter _rateLimiter;
    private readonly ConcurrentDictionary<Task, byte> _inFlight = new();
    private string _botUsername = string.Empty;
    private UpdateType[] _allowedUpdates = [UpdateType.Message];

    public Worker(
        ITelegramBotClient botClient,
        ILogger<Worker> logger,
        IOptions<BotOptions> botOptions,
        IOptions<ChatHistoryOptions> historyOptions,
        ChatHistoryStore historyStore,
        IAiCliRunner gemini,
        ChatQueue chatQueue,
        UserRateLimiter rateLimiter)
    {
        _botClient = botClient;
        _logger = logger;
        _botOptions = botOptions.Value;
        _historyOptions = historyOptions.Value;
        _historyStore = historyStore;
        _gemini = gemini;
        _chatQueue = chatQueue;
        _rateLimiter = rateLimiter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _historyStore.InitializeAsync(stoppingToken);

        var me = await _botClient.GetMe(stoppingToken);
        _botUsername = NormalizeUsername(_botOptions.Username, me.Username);
        _allowedUpdates = BuildAllowedUpdates();

        if (string.IsNullOrWhiteSpace(_botUsername))
        {
            _logger.LogWarning("Bot username is not available; group mention checks will fail.");
        }

        _logger.LogInformation("Bot connected as @{Username} (ID: {Id}).", me.Username, me.Id);
        _logger.LogInformation("Configuration: GroupMentionRequired={MentionRequired}, AllowedChatIds=[{ChatIds}], AllowAllChats={AllowAll}, AllowedUpdates=[{Updates}]",
            _botOptions.GroupMentionRequired,
            string.Join(", ", _botOptions.AllowedChatIds),
            _botOptions.AllowAllChats,
            string.Join(", ", _allowedUpdates));

        if (_botOptions.AllowedChatIds.Length == 0 && !_botOptions.AllowAllChats)
        {
            _logger.LogWarning(
                "AllowedChatIds is empty and AllowAllChats is false: the bot will ignore every chat. " +
                "Add your chat ID to AllowedChatIds (rejected chat IDs are logged at Information level).");
        }
        else if (_botOptions.AllowAllChats && _botOptions.AllowedChatIds.Length == 0)
        {
            _logger.LogWarning("AllowAllChats is true: any Telegram user can use this bot and your Gemini quota.");
        }

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

                // Run in the background so one slow chat doesn't hold up the rest. ChatQueue keeps
                // each chat in order; the runner caps how many CLI processes run at once.
                var task = HandleUpdateSafeAsync(update, stoppingToken);
                _inFlight.TryAdd(task, 0);
                _ = task.ContinueWith(t => _inFlight.TryRemove(t, out _), TaskScheduler.Default);
            }
        }

        await Task.WhenAll(_inFlight.Keys);
    }

    private async Task HandleUpdateSafeAsync(Update update, CancellationToken cancellationToken)
    {
        try
        {
            await HandleUpdateAsync(update, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle update {UpdateId}.", update.Id);
        }
    }

    private async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message)
        {
            return;
        }

        var text = message.Text ?? message.Caption;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Message text is user data, so only its length goes to the log.
        _logger.LogDebug("Received message (length {Length}) from user {UserId} in {ChatType} {ChatId}.",
            text.Length, message.From?.Id, message.Chat.Type, message.Chat.Id);

        if (!MessageFilter.IsChatAllowed(message.Chat.Id, _botOptions))
        {
            _logger.LogInformation("Ignored message from chat {ChatId} ({ChatType}): not in AllowedChatIds.",
                message.Chat.Id, message.Chat.Type);
            return;
        }

        if (!MessageFilter.ShouldProcess(message, text, _botUsername, _botOptions))
        {
            return;
        }

        text = MessageFilter.Normalize(text, message, _botUsername);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var userId = message.From?.Id ?? message.Chat.Id;
        if (!_rateLimiter.TryAcquire(userId))
        {
            _logger.LogInformation("Rate limit hit for user {UserId} in chat {ChatId}.", userId, message.Chat.Id);
            await _botClient.SendMessage(message.Chat, "Too many requests. Please wait a minute and try again.", cancellationToken: cancellationToken);
            return;
        }

        // History is kept per chat, so everyone in a group shares one history.
        var chatId = message.Chat.Id;

        using var gate = await _chatQueue.AcquireAsync(chatId, cancellationToken);

        var history = await _historyStore.GetRecentMessagesAsync(chatId, _historyOptions.MaxMessages, cancellationToken);
        var prompt = PromptBuilder.BuildPrompt(text, history);

        string response;
        try
        {
            response = await _gemini.RunAsync(prompt, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI CLI timed out for chat {ChatId}.", chatId);
            await _botClient.SendMessage(message.Chat, "The AI took too long to answer. Please try again.", cancellationToken: cancellationToken);
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The exception (which may include sign-in instructions) is for the operator's log only.
            _logger.LogError(ex, "AI CLI error for chat {ChatId}.", chatId);
            await _botClient.SendMessage(message.Chat, "Something went wrong while asking the AI. Please try again later.", cancellationToken: cancellationToken);
            return;
        }

        // Save the exchange only after Telegram accepted the reply.
        foreach (var chunk in TelegramMessageSplitter.Split(response))
        {
            await _botClient.SendMessage(message.Chat, chunk, cancellationToken: cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        await _historyStore.AddMessageAsync(chatId, "User", text, now, cancellationToken);
        await _historyStore.AddMessageAsync(chatId, "Assistant", response, now, cancellationToken);
        await _historyStore.TrimHistoryAsync(chatId, _historyOptions.MaxMessages, cancellationToken);
    }

    private UpdateType[] BuildAllowedUpdates()
    {
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
}
