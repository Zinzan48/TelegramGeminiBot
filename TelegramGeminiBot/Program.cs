using Microsoft.Extensions.Options;
using Telegram.Bot;
using TelegramGeminiBot;
using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;
using TelegramGeminiBot.Storage;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(OptionsRegistration.BotSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<GeminiOptions>()
    .Bind(builder.Configuration.GetSection(OptionsRegistration.GeminiSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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
