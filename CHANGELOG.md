# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/). While the version is 0.x, the configuration format may still change.

## [0.2.1] - 2026-11-15

Dependency updates only. Configuration and behavior are the same as 0.2.0.

### Changed

- Telegram.Bot 22.8.1 → 22.10.4. This is the only update that ships in the release binaries. It was checked by the
  build and the unit tests; the tests do not call the real Telegram API.
- Test tooling: Microsoft.NET.Test.Sdk 17.14.1 → 18.12.0 (versions up to 18.11.0 are affected by an advisory),
  coverlet.collector 6.0.4 → 10.1.0, xunit.runner.visualstudio 3.1.4 → 4.0.1.
- Dependabot now opens version updates against `develop`, and they reach `master` together in patch releases like
  this one. Major updates of `Microsoft.Extensions.*` and `Microsoft.Data.Sqlite` are ignored, so the project
  stays on .NET 10 LTS.

## [0.2.0] - 2026-11-15

> Upgrading from 0.1.0? Read the [upgrade guide](#upgrading-from-010) first. A 0.1.0 configuration
> still starts, but with the new defaults the bot answers nobody.

On 2026-06-18 Google stopped serving Gemini CLI to personal Google sign-ins (free, Google AI Pro and Ultra).
Gemini CLI now needs a paid Gemini API key or a Gemini Code Assist Standard/Enterprise licence
([announcement](https://developers.googleblog.com/en/an-important-update-transitioning-gemini-cli-to-antigravity-cli/)).
This release adds Antigravity CLI (`agy`) as a second backend and fixes security problems that matter now that
the repository is public.

The project targets .NET 10, the current LTS release (supported until 2028-11-14).

### Breaking

- The `Bot` and `Gemini` sections were replaced by `BotProfiles[]` and `GeminiProfiles[]`, selected with
  `ActiveProfiles`.
- An empty `AllowedChatIds` now means the bot answers nobody. Set `AllowAllChats: true` to answer everyone.
- `ActiveProfiles:Bot` takes a single name. The old comma-separated list used only the first token anyway.
- Gemini CLI runs with `--policy gemini-policies/deny-all-tools.toml`, so it can no longer read files or run
  commands for Telegram users. Older Gemini CLI versions without `--policy` will not start.
- The CLI may not run inside the bot's own folder. The default working directory is `./gemini-workspace`.

### Added

- Antigravity CLI backend (`Backend: "Antigravity"`). Prompts go through stdin in stream-json mode, and tools
  are blocked with `--settings antigravity-policies/deny-all-tools.json`, `--sandbox` and
  `--disable-slash-commands`.
  - With `AntigravityAuth: "Account"` (personal sign-in) the bot serves only its owner: exactly one private
    chat ID, no `AllowAllChats`, one request at a time. We read the Antigravity terms as treating any other
    user as account sharing.
  - `AntigravityAuth: "GeminiApiKey"` sets `modelProvider: gemini` and `GEMINI_API_KEY` and has no such limit.
- A startup check confirms the configured CLI is installed, supports the flags the bot uses and has its policy
  file. If not, the bot stops and logs the official install or update command.
- `dotnet run --project TelegramGeminiBot -- doctor` lists every prerequisite with a fix, and
  `doctor --install` runs the official installer after a y/N prompt.
- `CliHome` keeps the bot out of your personal CLI state (memory, MCP servers, hooks, history). Gemini CLI gets
  it as `GEMINI_CLI_HOME` and agy as `USERPROFILE`/`HOME`. For agy it defaults to `./agy-home`; `none` turns
  it off.
- `GeminiApiKey` setting, to be kept in user-secrets or environment variables.
- Per-user rate limit, `MaxRequestsPerUserPerMinute` (default 10).
- Prebuilt releases. Pushing a `v*` tag makes GitHub Actions build six zips (win-x64, linux-x64 and linux-arm64,
  each self-contained and framework-dependent) and attach them to the release with `SHA256SUMS.txt` and a build
  provenance attestation.
- MIT license, `SECURITY.md`, English and Traditional Chinese READMEs, GitHub Actions CI, Dependabot and 61
  unit tests.

Checked against agy 1.5.9: a request to read a file came back as "Permission denied for read_file", and the
keyring sign-in kept working with the separate `CliHome`.

### Changed

- `appsettings.json`, `DatabasePath`, the default workspace and `agy-home` are found relative to the program's
  folder instead of the current directory, so the bot also works when started from elsewhere or as a service.
  When you run from source, the chat database therefore moves to `TelegramGeminiBot/bin/<configuration>/net10.0/data/`.
- Fix instructions printed by `doctor` and the startup check match how you run the bot: `dotnet run` and
  user-secrets from source, the executable and environment variables in a release build.

### Security

- The committed chat-history database was removed and secrets moved out of `appsettings.json`.
- Logs no longer contain message or prompt text.
- Microsoft.* packages updated to 10.0.15, the latest .NET 10 LTS patch. Microsoft.Data.Sqlite now brings in
  SQLitePCLRaw 2.1.13, outside the range of GHSA-2m69-gcr7-jv3q.
- `doctor` warns about Node.js 20, which reached end of life on 2026-04-30.

### Fixed

- Replies over Telegram's 4096-character limit are sent as several messages. Before, sending failed.
- A slow answer in one chat no longer holds up the others.
- A race in the per-chat queue could run two replies for the same chat at once.
- History could come back out of order, because a question and its answer shared a timestamp.

### Deprecated

- Gemini CLI with a personal Google sign-in, which Google no longer serves. Use an API key, a Code Assist
  licence or the Antigravity backend.

### Upgrading from 0.1.0

1. Run `dotnet run --project TelegramGeminiBot -- doctor` to see what is missing.
2. Move the token out of `appsettings.json`:
   `dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:Token" "<token>"`
   (in production, set `BotProfiles__0__Token`).
3. Move your settings to the new sections:

   | 0.1.0 | 0.2.0 |
   |---|---|
   | `Bot:Token`, `Bot:Username`, `Bot:GroupMentionRequired`, `Bot:AllowedUpdates` | same keys under `BotProfiles[0]` |
   | `Gemini:ExecutablePath`, `Gemini:Model`, `Gemini:PromptTimeoutSeconds`, … | same keys under `GeminiProfiles[0]` |
   | (none) | `GeminiProfiles[0].Backend`: `GeminiCli` or `Antigravity` |

4. Start the bot and send it a message. It logs `Ignored message from chat <id>`; add that ID:
   `dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:AllowedChatIds:0" "<id>"`
5. Pick a backend that still works. For Gemini CLI, set `GeminiApiKey` (paid key) or keep a Code Assist
   Standard/Enterprise sign-in. For Antigravity, install agy, run `agy` once to sign in, then set
   `ActiveProfiles:Gemini` to `Antigravity`.
6. If your Gemini CLI is too old for `--policy`, update it with `npm install -g @google/gemini-cli@latest`.
   `PolicyPath: "none"` also gets it running, but then Telegram users can make it use tools on your machine.

## [0.1.0] - 2026-01-25

First version: forwards Telegram messages to Gemini CLI (personal Google sign-in) and keeps per-chat history in
SQLite. It stopped working on 2026-06-18, when Google ended that sign-in.

[0.2.1]: https://github.com/Zinzan48/TelegramGeminiBot/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/Zinzan48/TelegramGeminiBot/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/Zinzan48/TelegramGeminiBot/releases/tag/v0.1.0
