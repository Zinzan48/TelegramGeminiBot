using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramGeminiBot;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;
using TelegramGeminiBot.Storage;

var builder = Host.CreateApplicationBuilder(args);

// Load profiles
var botProfiles = builder.Configuration.GetSection("BotProfiles").Get<BotProfile[]>() ?? [];
var geminiProfiles = builder.Configuration.GetSection("GeminiProfiles").Get<GeminiProfile[]>() ?? [];

// Support multiple active bot profiles (comma-separated)
var activeBotProfileNames = builder.Configuration.GetValue<string>("ActiveProfiles:Bot") ?? string.Empty;
var activeGeminiProfileName = builder.Configuration.GetValue<string>("ActiveProfiles:Gemini");

var activeBots = ResolveBotProfiles(botProfiles, activeBotProfileNames);
var activeGemini = ResolveProfile(geminiProfiles, activeGeminiProfileName);

// Merge multiple bot profiles into BotOptions
builder.Services.Configure<BotOptions>(opts =>
{
    // Use first profile's token and username as primary
    var primary = activeBots[0];
    opts.Token = primary.Token;
    opts.Username = primary.Username;
    
    // Merge AllowedUpdates from all profiles
    opts.AllowedUpdates = activeBots
        .SelectMany(p => p.AllowedUpdates)
        .Distinct()
        .ToArray();
    
    // GroupMentionRequired: true only if ALL profiles require it
    opts.GroupMentionRequired = activeBots.All(p => p.GroupMentionRequired);
    
    // Merge AllowedChatIds from all profiles
    opts.AllowedChatIds = activeBots
        .SelectMany(p => p.AllowedChatIds)
        .Distinct()
        .ToArray();
});

builder.Services.Configure<GeminiOptions>(opts =>
{
    opts.ExecutablePath = activeGemini.ExecutablePath;
    opts.Model = activeGemini.Model;
    opts.UseHeadless = activeGemini.UseHeadless;
    opts.PromptTimeoutSeconds = activeGemini.PromptTimeoutSeconds;
    opts.OutputFormat = activeGemini.OutputFormat;
    opts.WorkingDirectory = activeGemini.WorkingDirectory;
});

builder.Services.AddOptions<ChatHistoryOptions>()
    .Bind(builder.Configuration.GetSection(OptionsRegistration.ChatHistorySection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    return new TelegramBotClient(options.Token);
});

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<ChatHistoryOptions>>().Value;
    return new ChatHistoryStore(options.DatabasePath);
});

builder.Services.AddSingleton<ChatQueue>();
builder.Services.AddSingleton<GeminiCliRunner>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static TProfile ResolveProfile<TProfile>(IEnumerable<TProfile> profiles, string? requestedName) where TProfile : class
{
    if (!profiles.Any())
    {
        throw new InvalidOperationException($"No {typeof(TProfile).Name} entries found in configuration.");
    }

    if (!string.IsNullOrWhiteSpace(requestedName))
    {
        var match = profiles.FirstOrDefault(p =>
            string.Equals((p as dynamic).Name, requestedName, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match;
        }

        throw new InvalidOperationException($"Active profile '{requestedName}' not found for {typeof(TProfile).Name}. Available: {string.Join(", ", profiles.Select(p => (p as dynamic).Name))}");
    }

    return profiles.First();
}

static List<BotProfile> ResolveBotProfiles(BotProfile[] profiles, string profileNames)
{
    if (profiles.Length == 0)
    {
        throw new InvalidOperationException("No BotProfile entries found in configuration.");
    }

    if (string.IsNullOrWhiteSpace(profileNames))
    {
        return [profiles.First()];
    }

    var names = profileNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var result = new List<BotProfile>();

    foreach (var name in names)
    {
        var match = profiles.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        
        if (match is null)
        {
            throw new InvalidOperationException(
                $"Active profile '{name}' not found. Available: {string.Join(", ", profiles.Select(p => p.Name))}");
        }
        
        result.Add(match);
    }

    return result.Count == 0 ? [profiles.First()] : result;
}
