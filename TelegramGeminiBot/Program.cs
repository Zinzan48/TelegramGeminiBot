using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramGeminiBot;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;
using TelegramGeminiBot.Storage;

var builder = Host.CreateApplicationBuilder(args);

var botProfiles = builder.Configuration.GetSection("BotProfiles").Get<BotProfile[]>() ?? [];
var geminiProfiles = builder.Configuration.GetSection("GeminiProfiles").Get<GeminiProfile[]>() ?? [];

var activeBot = ResolveProfile(botProfiles, builder.Configuration.GetValue<string>("ActiveProfiles:Bot"));
var activeGemini = ResolveProfile(geminiProfiles, builder.Configuration.GetValue<string>("ActiveProfiles:Gemini"));

ValidateProfile(activeBot);
ValidateProfile(activeGemini);
PrepareGeminiPaths(activeGemini, builder.Environment.ContentRootPath);

builder.Services.AddSingleton<IOptions<BotOptions>>(Options.Create<BotOptions>(activeBot));
builder.Services.AddSingleton<IOptions<GeminiOptions>>(Options.Create<GeminiOptions>(activeGemini));

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

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new UserRateLimiter(
    sp.GetRequiredService<IOptions<BotOptions>>().Value.MaxRequestsPerUserPerMinute,
    TimeSpan.FromMinutes(1),
    sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<ChatQueue>();
builder.Services.AddSingleton<GeminiCliRunner>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

static TProfile ResolveProfile<TProfile>(TProfile[] profiles, string? requestedName) where TProfile : INamedProfile
{
    var kind = typeof(TProfile).Name;
    if (profiles.Length == 0)
    {
        throw new InvalidOperationException($"No {kind} entries found in configuration.");
    }

    if (string.IsNullOrWhiteSpace(requestedName))
    {
        return profiles[0];
    }

    if (requestedName.Contains(','))
    {
        throw new InvalidOperationException(
            $"Only one active {kind} is supported, but ActiveProfiles lists '{requestedName}'. Run one bot instance per token.");
    }

    return profiles.FirstOrDefault(p => string.Equals(p.Name, requestedName.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Active profile '{requestedName}' not found for {kind}. Available: {string.Join(", ", profiles.Select(p => p.Name))}");
}

static void ValidateProfile(object profile)
{
    var results = new List<ValidationResult>();
    if (!Validator.TryValidateObject(profile, new ValidationContext(profile), results, validateAllProperties: true))
    {
        throw new InvalidOperationException(
            $"Invalid {profile.GetType().Name}: {string.Join("; ", results.Select(r => r.ErrorMessage))}");
    }
}

static void PrepareGeminiPaths(GeminiOptions gemini, string contentRoot)
{
    // The CLI must never run inside the bot's own folder: that folder holds the bot token
    // (appsettings / secrets) and every chat's history database.
    var workingDirectory = string.IsNullOrWhiteSpace(gemini.WorkingDirectory)
        ? Path.Combine(contentRoot, "gemini-workspace")
        : Path.GetFullPath(gemini.WorkingDirectory, contentRoot);

    if (IsSameOrParent(workingDirectory, contentRoot) || IsSameOrParent(workingDirectory, AppContext.BaseDirectory))
    {
        throw new InvalidOperationException(
            $"Gemini WorkingDirectory '{workingDirectory}' contains the bot's own files. Point it to a dedicated empty directory.");
    }

    Directory.CreateDirectory(workingDirectory);
    gemini.WorkingDirectory = workingDirectory;

    if (!string.IsNullOrWhiteSpace(gemini.CliHome))
    {
        gemini.CliHome = Path.GetFullPath(gemini.CliHome, contentRoot);
        Directory.CreateDirectory(gemini.CliHome);
    }

    if (!string.IsNullOrWhiteSpace(gemini.PolicyPath))
    {
        var policyPath = Path.GetFullPath(gemini.PolicyPath, AppContext.BaseDirectory);
        if (!File.Exists(policyPath) && !Directory.Exists(policyPath))
        {
            throw new InvalidOperationException($"Gemini PolicyPath '{policyPath}' does not exist.");
        }

        gemini.PolicyPath = policyPath;
    }
}

static bool IsSameOrParent(string candidate, string protectedPath)
{
    var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate)) + Path.DirectorySeparatorChar;
    var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(protectedPath)) + Path.DirectorySeparatorChar;
    return b.StartsWith(a, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
