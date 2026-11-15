# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/) (0.x: the configuration format may still change).

## [0.2.0] - 2026-11-15

> **Upgrading from 0.1.0? Read the [upgrade guide](#upgrading-from-010) first.**
> With the new defaults, a 0.1.0 configuration starts but **answers nobody**.

### Why this release

On 2026-06-18 Google stopped serving Gemini CLI to personal Google sign-ins (free, Google AI Pro and Ultra).
Gemini CLI now works only with a paid Gemini API key or a Gemini Code Assist Standard/Enterprise licence
([announcement](https://developers.googleblog.com/en/an-important-update-transitioning-gemini-cli-to-antigravity-cli/)).
This release adds Antigravity CLI (`agy`) as a second backend and closes several security gaps that matter
once the project is public.

### Breaking

- **Configuration format.** The `Bot` and `Gemini` sections were replaced by `BotProfiles[]` and
  `GeminiProfiles[]`, selected with `ActiveProfiles`.
- **Fail-closed chat access.** An empty `AllowedChatIds` now means the bot answers **nobody**.
  To answer everyone you must set `AllowAllChats: true` explicitly.
- **One bot per process.** `ActiveProfiles:Bot` takes a single name. The old comma-separated merge used only
  the first token anyway.
- **Tools are denied.** Gemini CLI runs with `--policy gemini-policies/deny-all-tools.toml`, so it can no longer
  read files or run commands for Telegram users. This needs a Gemini CLI version that supports `--policy`.
- **Working directory.** The CLI may no longer run inside the bot's own folder. The default is `./gemini-workspace`.

### Added

- **Antigravity CLI backend** (`Backend: "Antigravity"`). Prompts go through stdin in stream-json mode, and
  tools are denied through `--settings antigravity-policies/deny-all-tools.json`, `--sandbox` and
  `--disable-slash-commands`.
  - `AntigravityAuth: "Account"` (personal sign-in) is **owner-only**: exactly one private chat ID, no
    `AllowAllChats`, one request at a time. Under our reading of the Antigravity terms, letting anyone else use
    your account counts as account sharing.
  - `AntigravityAuth: "GeminiApiKey"` uses `modelProvider: gemini` and `GEMINI_API_KEY`, with no owner-only limit.
- **Startup preflight.** The bot checks that the configured CLI is installed, supports the flags it needs and has
  its policy file. If anything is missing, it stops and logs the official install or update command.
- **`doctor` command.** `dotnet run --project TelegramGeminiBot -- doctor` lists every prerequisite with a fix.
  `doctor --install` runs the official installer after a y/N prompt.
- `CliHome` keeps the bot away from your personal CLI state (MCP servers, hooks, history). It is passed as
  `GEMINI_CLI_HOME` to Gemini CLI and as `USERPROFILE`/`HOME` to agy.
- `GeminiApiKey` setting (keep it in user-secrets or environment variables).
- Per-user rate limit `MaxRequestsPerUserPerMinute` (default 10).
- MIT license, `SECURITY.md`, English and Traditional Chinese README, GitHub Actions CI, Dependabot, 56 unit tests.

### Security

- Removed the committed chat-history database and moved secrets out of `appsettings.json`.
- Message and prompt content are no longer written to logs.
- Pinned `SQLitePCLRaw.bundle_e_sqlite3` 2.1.14 (GHSA-2m69-gcr7-jv3q).

### Fixed

- Replies longer than Telegram's 4096-character limit are split into several messages instead of failing.
- One slow answer no longer blocks every other chat.
- A race in the per-chat queue could let two replies for the same chat run at once.
- Chat history order was ambiguous because each question and its answer were stored with the same timestamp.

### Deprecated

- Gemini CLI with a personal Google sign-in. Google no longer serves it. Use an API key, a Code Assist
  licence, or the Antigravity backend.

### Upgrading from 0.1.0

1. **Run the doctor** to see what is missing:
   `dotnet run --project TelegramGeminiBot -- doctor`
2. **Move the token out of `appsettings.json`**:
   `dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:Token" "<token>"`
   In production, use `BotProfiles__0__Token`.
3. **Convert the configuration.**

   | 0.1.0 | 0.2.0 |
   |---|---|
   | `Bot:Token`, `Bot:Username`, `Bot:GroupMentionRequired`, `Bot:AllowedUpdates` | same keys under `BotProfiles[0]` |
   | `Gemini:ExecutablePath`, `Gemini:Model`, `Gemini:PromptTimeoutSeconds`, … | same keys under `GeminiProfiles[0]` |
   | (none) | `GeminiProfiles[0].Backend`: `GeminiCli` or `Antigravity` |

4. **Allow your chats.** Start the bot and message it. Copy the ID from the `Ignored message from chat <id>`
   log line, then:
   `dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:AllowedChatIds:0" "<id>"`
5. **Pick a backend that still works.**
   - Gemini CLI: set `GeminiApiKey` (paid key), or keep a Code Assist Standard/Enterprise sign-in.
   - Antigravity: install agy, run `agy` once to sign in, then set `ActiveProfiles:Gemini` to `Antigravity`.
6. **Old Gemini CLI without `--policy`?** Update it with `npm install -g @google/gemini-cli@latest`.
   Setting `PolicyPath: "none"` also works but lets Telegram users use tools on your machine. Not recommended.

## [0.1.0] - 2026-01-25

Initial version: a Telegram bot that forwards messages to Gemini CLI (personal Google sign-in) and keeps per-chat
history in SQLite. **No longer works**, because Google stopped serving that sign-in on 2026-06-18.

[0.2.0]: https://github.com/Zinzan48/TelegramGeminiBot/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Zinzan48/TelegramGeminiBot/releases/tag/v0.1.0
