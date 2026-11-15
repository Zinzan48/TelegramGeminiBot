# Security policy

## Reporting a vulnerability

Please report security problems through GitHub's
[private vulnerability reporting](https://github.com/Zinzan48/TelegramGeminiBot/security/advisories/new),
not a public issue. Include the version or commit, your configuration and the steps to reproduce.

## Threat model

Anyone who can message the bot is untrusted, and their text ends up as input to an AI agent on your machine.

The bot only answers chats listed in `AllowedChatIds`. If the list is empty it answers no one; opening it to
everyone takes `AllowAllChats: true`, and the bot logs a warning when that is set. With a personal agy sign-in,
it refuses to start unless the list holds exactly one private chat.

The model gets no tools. Gemini CLI runs with `gemini-policies/deny-all-tools.toml` (`--policy`), which denies
every built-in and MCP tool. agy runs with `antigravity-policies/deny-all-tools.json` (`--settings`),
`--sandbox` and `--disable-slash-commands`. A message cannot make either CLI read files, run commands, browse
or call MCP servers.

The CLI also never runs inside the bot's own folder, where the token and the chat database live, and each user
is rate limited (`MaxRequestsPerUserPerMinute`).

Out of scope: if an operator sets `AllowAllChats`, removes the policy or points `WorkingDirectory` at sensitive
data, that risk is theirs.

## Operator checklist

- Keep the bot token out of `appsettings.json`. Use `dotnet user-secrets` in development and environment
  variables in production.
- If a token was ever committed or shared, revoke it with @BotFather (`/revoke`).
- Set `CliHome` so the bot does not pick up your personal `~/.gemini` setup (MCP servers, hooks, saved
  sessions). agy stores conversations and "knowledge" in the profile folder, so the Antigravity backend puts
  them in `./agy-home` by default. Leave it that way. With `CliHome: none`, your personal agy memory shapes
  the answers Telegram users get, and their conversations show up in your history.
- agy updates itself. Run `dotnet run --project TelegramGeminiBot -- doctor` after it does.
- Run the bot under a low-privilege OS account, not SYSTEM or root.
