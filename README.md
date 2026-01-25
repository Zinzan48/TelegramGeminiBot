# TelegramGeminiBot

Telegram Bot + Gemini CLI (Google login) bridge using .NET Worker Service.

## Prerequisites
- .NET SDK (net10.0 preview)
- Node.js 20+ and Gemini CLI installed: `npm install -g @google/gemini-cli`
- Gemini CLI login: `gemini login`
- Telegram Bot Token

## Configuration
Edit `TelegramGeminiBot/appsettings.json` (or use environment variables):

```json
{
  "Bot": {
    "Token": "<YOUR_TOKEN>",
    "Username": "your_bot_username",
    "AllowedUpdates": [ "Message" ],
    "GroupMentionRequired": true
  },
  "Gemini": {
    "ExecutablePath": "gemini",
    "Model": "gemini 3.0 pro",
    "UseHeadless": true,
    "PromptTimeoutSeconds": 120,
    "OutputFormat": "text"
  },
  "ChatHistory": {
    "DatabasePath": "data\\chat-history.db",
    "MaxMessages": 20
  }
}
```

Environment variable example (prefix with `Bot__`, `Gemini__`, `ChatHistory__`).

## Run
From repo root:

```bash
dotnet run --project TelegramGeminiBot
```

## Notes
- Group chats: only responds when @bot is mentioned (or bot command like `/ask@bot`).
- Gemini CLI headless mode uses stdin piping. Ensure preview features & model are enabled in Gemini CLI if needed (`/settings` + `/model`).
- If you want to force a specific model, keep `Gemini:Model` set to `gemini 3.0 pro`.
