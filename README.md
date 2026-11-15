# TelegramGeminiBot

[![build](https://github.com/Zinzan48/TelegramGeminiBot/actions/workflows/build.yml/badge.svg)](https://github.com/Zinzan48/TelegramGeminiBot/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A small .NET 10 worker that connects a Telegram bot to [Gemini CLI](https://github.com/google-gemini/gemini-cli).
Each Telegram message is piped to `gemini` in headless mode and the answer is sent back. Recent
messages per chat are kept in SQLite, so the bot remembers the conversation.

[繁體中文說明](README.zh-TW.md)

## Before you deploy: read this

- **Terms of service.** Check that your Gemini CLI sign-in method allows the way you use it. The
  [Gemini CLI terms](https://geminicli.com/docs/resources/tos-privacy) say that reaching its backend
  through third-party software can get an account suspended, and they do not cover serving other
  people with a personal Google login. An API key (`GEMINI_API_KEY`) or Vertex AI is the safer choice
  for a bot other people use.
- **Free tier.** Per the same page, Gemini CLI was replaced by Antigravity CLI for unpaid and
  Google One users on 2026-06-18. You need an API key, a paid Code Assist licence or Vertex AI.
- **Who can talk to the bot.** By default nobody. Add chat IDs to `AllowedChatIds` (see below).

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20+ and Gemini CLI: `npm install -g @google/gemini-cli`, signed in or configured with an API key.
  A version with `--policy` support is required for the default tool lockdown.
- A bot token from [@BotFather](https://t.me/BotFather)

## Quick start

```bash
git clone https://github.com/Zinzan48/TelegramGeminiBot.git
cd TelegramGeminiBot

# Secrets stay outside the repo (loaded in the Development environment)
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:Token" "<your-bot-token>"

dotnet run --project TelegramGeminiBot
```

The first run logs `Ignored message from chat <id>` when you message the bot. Put that ID into
`AllowedChatIds` and restart:

```bash
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:AllowedChatIds:0" "<your-chat-id>"
```

In production, use environment variables instead. `__` separates levels and array items:

```bash
export BotProfiles__0__Token="<your-bot-token>"
export BotProfiles__0__AllowedChatIds__0="123456789"
export DOTNET_ENVIRONMENT=Production
```

## Configuration

All settings live in `TelegramGeminiBot/appsettings.json`. That file is a template, so do not put secrets in it.

### `BotProfiles[]`

| Key | Default | Description |
|---|---|---|
| `Name` | `Main` | Profile name chosen by `ActiveProfiles:Bot`. One active profile per process. |
| `Token` | — | Bot token. Supply via user-secrets or environment variables. |
| `Username` | Telegram value | Bot username without `@`, used for group mentions. |
| `AllowedChatIds` | `[]` | Chats the bot answers. Empty means **none**. |
| `AllowAllChats` | `false` | Answer every chat. Anyone who finds the bot can spend your quota. |
| `GroupMentionRequired` | `true` | In groups, only answer when mentioned or addressed by `/cmd@bot`. |
| `MaxRequestsPerUserPerMinute` | `10` | Per-user rate limit. `0` disables it. |
| `AllowedUpdates` | `["Message"]` | Telegram update types to poll. |

### `GeminiProfiles[]`

| Key | Default | Description |
|---|---|---|
| `ExecutablePath` | `gemini` | Use `gemini.cmd` on Windows. |
| `Model` | CLI default | Passed as `--model` when set. |
| `WorkingDirectory` | `./gemini-workspace` | Where the CLI runs. Must not contain the bot's own files. |
| `CliHome` | — | Sets `GEMINI_CLI_HOME`, so the bot uses its own `.gemini` folder instead of your personal MCP servers, hooks and history. Recommended. |
| `PolicyPath` | `gemini-policies/deny-all-tools.toml` | Passed as `--policy`. Denies all tools. |
| `PromptTimeoutSeconds` | `120` | Kills the CLI after this long. |
| `MaxConcurrentProcesses` | `2` | CLI processes allowed at once across all chats. |

### `ChatHistory`

| Key | Default | Description |
|---|---|---|
| `DatabasePath` | `data/chat-history.db` | SQLite file. Git ignores it. |
| `MaxMessages` | `20` | Messages kept and sent as context, per chat. |

## Security

Telegram users are untrusted input to an AI agent running on your machine. The defaults lock this
down: a fail-closed allow-list, a deny-all tool policy, an isolated working directory and a rate limit.
See [SECURITY.md](SECURITY.md) for the threat model and how to report a vulnerability.

In group chats, every member shares the same conversation history.

## Development

```bash
dotnet build TelegramGeminiBot.slnx
dotnet test TelegramGeminiBot.slnx
```

## License

[MIT](LICENSE)
