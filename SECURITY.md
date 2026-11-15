# Security Policy

## Reporting a vulnerability

Please do not open a public issue for security problems. Use GitHub's
[private vulnerability reporting](https://github.com/Zinzan48/TelegramGeminiBot/security/advisories/new)
instead. Include the affected version or commit, the configuration you used
and the steps to reproduce.

## Threat model

Everyone who can message the bot is untrusted. The bot defends against them with:

- **Chat allow-list, fail closed.** `AllowedChatIds` empty means the bot answers
  nobody. `AllowAllChats: true` is an explicit, logged opt-in.
- **No tools for the model.** Gemini CLI runs with
  `gemini-policies/deny-all-tools.toml` (`--policy`), which denies every built-in and MCP tool.
  Antigravity CLI runs with `antigravity-policies/deny-all-tools.json` (`--settings`), `--sandbox`
  and `--disable-slash-commands`. In both cases, a message cannot make the CLI read files, run
  commands, browse or call MCP servers.
- **Owner-only Antigravity account mode.** With a personal agy sign-in, the bot refuses to start
  unless it serves exactly one private chat.
- **Isolated working directory.** The bot refuses to run the CLI inside its own
  folder, where the token and chat database live.
- **Per-user rate limit** (`MaxRequestsPerUserPerMinute`).

Out of scope: an operator who sets `AllowAllChats`, removes the policy, or points
`WorkingDirectory` at sensitive data has chosen to accept that risk.

## Operator checklist

- Keep the bot token out of `appsettings.json`. Use `dotnet user-secrets`
  for development and environment variables in production.
- If a token was ever committed or shared, revoke it with @BotFather (`/revoke`).
- Set `CliHome` so the bot does not inherit your personal `~/.gemini`
  configuration (MCP servers, hooks, saved sessions). agy keeps conversations and
  "knowledge" under the profile folder; without `CliHome`, bot conversations end up in your
  personal agy history.
- Run `dotnet run --project TelegramGeminiBot -- doctor` after updating the CLI. agy updates itself.
- Run the bot as a low-privilege OS account, not as SYSTEM or root.
