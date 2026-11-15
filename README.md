# TelegramGeminiBot

[![build](https://github.com/Zinzan48/TelegramGeminiBot/actions/workflows/build.yml/badge.svg)](https://github.com/Zinzan48/TelegramGeminiBot/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> [!IMPORTANT]
> **v0.2.0 has breaking changes.** The configuration format changed, and an empty `AllowedChatIds` now
> means the bot answers nobody. Gemini CLI also stopped accepting personal Google sign-ins on 2026-06-18.
> Before upgrading, read the [upgrade guide in CHANGELOG.md](CHANGELOG.md#upgrading-from-010).

A small .NET 10 worker that connects a Telegram bot to an AI command-line agent:
[Gemini CLI](https://github.com/google-gemini/gemini-cli) or
[Antigravity CLI (`agy`)](https://antigravity.google/docs/getting-started). Each message is sent to the CLI
in headless mode, the answer goes back to Telegram, and the most recent messages in each chat are kept in SQLite
so the bot remembers the conversation.

[繁體中文說明](README.zh-TW.md)

## Choose a backend

| | Gemini CLI | Antigravity CLI, account sign-in | Antigravity CLI, API key |
|---|---|---|---|
| `GeminiProfiles[].Backend` | `GeminiCli` | `Antigravity` | `Antigravity` |
| Authentication | Paid Gemini API key, or Code Assist Standard/Enterprise | Your Google account (OS keyring) | Gemini API key + `AntigravityAuth: GeminiApiKey` |
| Who may use the bot | Anyone you allow | **Only you**: one private chat | Anyone you allow |
| Quota | Your API billing | Your personal 5-hour/weekly Antigravity quota | Your API billing |
| Install | `npm install -g @google/gemini-cli` (Node.js 20+) | Windows: `irm https://antigravity.google/cli/install.ps1 \| iex`<br>macOS/Linux: `curl -fsSL https://antigravity.google/cli/install.sh \| bash` | same as account sign-in |

**About the terms.** Personal Google sign-in for Gemini CLI ended on 2026-06-18
([announcement](https://developers.googleblog.com/en/an-important-update-transitioning-gemini-cli-to-antigravity-cli/)).
For Antigravity, as we read it (based on a [forum answer](https://discuss.ai.google.dev/t/terms-clarification-personal-antigravity-cli-integration-with-a-multi-ai-orchestrator/187536),
not the terms text), driving the official `agy` from your own scripts is fine, but letting anyone else use your
account counts as account sharing. That is why the bot refuses to start in account mode unless `AllowedChatIds`
holds exactly one private chat. To serve other people, use an API key. Check the current terms yourself
before you deploy.

Tested with Gemini CLI `--policy` support and agy 1.5.9. agy updates itself, so run `doctor` after updates.

## Quick start

```bash
git clone https://github.com/Zinzan48/TelegramGeminiBot.git
cd TelegramGeminiBot

# 1. Check prerequisites. Prints install commands for anything missing.
dotnet run --project TelegramGeminiBot -- doctor
#    Install the configured CLI interactively (asks y/N first):
dotnet run --project TelegramGeminiBot -- doctor --install

# 2. Store secrets outside the repo (loaded in the Development environment)
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:Token" "<your-bot-token>"
#    Gemini CLI, or Antigravity with an API key:
dotnet user-secrets --project TelegramGeminiBot set "GeminiProfiles:0:GeminiApiKey" "<your-gemini-api-key>"
#    Or use Antigravity with your account: run `agy` once to sign in, then select that profile:
dotnet user-secrets --project TelegramGeminiBot set "ActiveProfiles:Gemini" "Antigravity"

# 3. Run
dotnet run --project TelegramGeminiBot
```

When you message the bot, it logs `Ignored message from chat <id>`. Put that ID into the allow-list and restart:

```bash
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:AllowedChatIds:0" "<your-chat-id>"
```

At startup, the bot checks the configured CLI. If the CLI is missing or too old, the bot stops and logs the
exact command to fix it, so it never fails silently on the first message.

In production, use environment variables. `__` separates sections and array indexes:

```bash
export BotProfiles__0__Token="<your-bot-token>"
export BotProfiles__0__AllowedChatIds__0="123456789"
export GeminiProfiles__0__GeminiApiKey="<your-gemini-api-key>"
export DOTNET_ENVIRONMENT=Production
```

## Configuration

All settings live in `TelegramGeminiBot/appsettings.json`. That file is a template, so do not put secrets in it.

### `BotProfiles[]`

| Key | Default | Description |
|---|---|---|
| `Name` | `Main` | Profile chosen by `ActiveProfiles:Bot`. One active profile per process. |
| `Token` | — | Bot token. Use user-secrets or environment variables. |
| `Username` | value from Telegram | Bot username without `@`, used for group mentions. |
| `AllowedChatIds` | `[]` | Chats the bot answers. Empty means **none**. |
| `AllowAllChats` | `false` | Answer every chat. Anyone who finds the bot can spend your quota. Not allowed with agy account sign-in. |
| `GroupMentionRequired` | `true` | In groups, answer only when mentioned or addressed by `/cmd@bot`. |
| `MaxRequestsPerUserPerMinute` | `10` | Per-user rate limit. `0` disables it. |
| `AllowedUpdates` | `["Message"]` | Telegram update types to poll. |

### `GeminiProfiles[]`

| Key | Default | Description |
|---|---|---|
| `Name` | `Default` / `Antigravity` | Profile chosen by `ActiveProfiles:Gemini`. |
| `Backend` | `GeminiCli` | `GeminiCli` or `Antigravity`. |
| `AntigravityAuth` | `Account` | `Account` (owner-only) or `GeminiApiKey`. Used only by the Antigravity backend. |
| `GeminiApiKey` | — | Passed as `GEMINI_API_KEY`. Use user-secrets or environment variables. |
| `ExecutablePath` | `gemini` / `agy` | Looked up on PATH. On Windows `gemini` also finds `gemini.cmd`. |
| `Model` | CLI default | Passed as `--model`. |
| `WorkingDirectory` | `./gemini-workspace` | Where the CLI runs. Must not contain the bot's own files. |
| `CliHome` | — | Separate state folder so the bot does not share your personal CLI configuration, MCP servers or history. Recommended. |
| `PolicyPath` | per backend | Deny-all tool policy (`--policy` for Gemini CLI, `--settings` for agy). `none` disables it (not recommended). |
| `PromptTimeoutSeconds` | `120` | The CLI is killed after this long. |
| `MaxConcurrentProcesses` | `2` | CLI processes at once across all chats. Always 1 for agy account sign-in. |

### `ChatHistory`

| Key | Default | Description |
|---|---|---|
| `DatabasePath` | `data/chat-history.db` | SQLite file. Ignored by git. |
| `MaxMessages` | `20` | Messages kept and sent as context per chat. |

## Security

Telegram users are untrusted input to an AI agent running on your machine. The defaults lock this down: a
fail-closed allow-list, deny-all tool policies, an isolated working directory and a rate limit.
See [SECURITY.md](SECURITY.md) for the threat model and how to report a vulnerability.

All members of a group share that group's conversation history.

## Development

```bash
dotnet build TelegramGeminiBot.slnx
dotnet test TelegramGeminiBot.slnx
```

See [CHANGELOG.md](CHANGELOG.md) for release notes.

## License

[MIT](LICENSE)
