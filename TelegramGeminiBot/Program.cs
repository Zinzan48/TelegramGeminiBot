using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramGeminiBot;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;
using TelegramGeminiBot.Storage;

var isDoctor = args.Length > 0 && string.Equals(args[0], "doctor", StringComparison.OrdinalIgnoreCase);
var doctorInstall = isDoctor && args.Skip(1).Any(a => string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase));
var hostArgs = isDoctor ? args.Skip(1).Where(a => !string.Equals(a, "--install", StringComparison.OrdinalIgnoreCase)).ToArray() : args;

// Use the program's folder, not the current directory, so appsettings.json and the policy files are
// found when the bot is started from elsewhere or as a service.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = hostArgs,
    ContentRootPath = AppContext.BaseDirectory
});
CommandHints.IsDevelopment = builder.Environment.IsDevelopment();

var botProfiles = builder.Configuration.GetSection("BotProfiles").Get<BotProfile[]>() ?? [];
var geminiProfiles = builder.Configuration.GetSection("GeminiProfiles").Get<GeminiProfile[]>() ?? [];

var activeBot = ProfileSetup.ResolveProfile(botProfiles, builder.Configuration.GetValue<string>("ActiveProfiles:Bot"));
var activeGemini = ProfileSetup.ResolveProfile(geminiProfiles, builder.Configuration.GetValue<string>("ActiveProfiles:Gemini"));

ProfileSetup.ApplyGeminiDefaults(activeGemini, builder.Environment.ContentRootPath, AppContext.BaseDirectory, createDirectories: !isDoctor);

if (isDoctor)
{
    return await Doctor.RunAsync(activeBot, activeGemini, doctorInstall, CancellationToken.None);
}

ProfileSetup.ValidateAnnotations(activeBot);
ProfileSetup.ValidateAnnotations(activeGemini);
if (ProfileSetup.ValidateBackendAccess(activeBot, activeGemini) is { } accessError)
{
    throw new InvalidOperationException(accessError);
}

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
    return new ChatHistoryStore(Path.GetFullPath(options.DatabasePath, builder.Environment.ContentRootPath));
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new UserRateLimiter(
    sp.GetRequiredService<IOptions<BotOptions>>().Value.MaxRequestsPerUserPerMinute,
    TimeSpan.FromMinutes(1),
    sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<ChatQueue>();

if (activeGemini.Backend == AiBackend.Antigravity)
{
    builder.Services.AddSingleton<IAiCliRunner, AntigravityCliRunner>();
}
else
{
    builder.Services.AddSingleton<IAiCliRunner, GeminiCliRunner>();
}

// Hosted services start in registration order, so the preflight check runs before Worker polls Telegram.
builder.Services.AddHostedService<PreflightHostedService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
return 0;
