# TelegramGeminiBot（繁體中文）

> [!IMPORTANT]
> **v0.2.0 有破壞性變更**：設定格式改了，`AllowedChatIds` 留空現在代表不回應任何人。
> 另外，Gemini CLI 從 2026-06-18 起已不接受個人 Google 帳號登入。
> 升級前請先看 [CHANGELOG 的升級指南](CHANGELOG.md#upgrading-from-010)。

這是一個 .NET 10 Worker 服務，負責把 Telegram bot 接到 AI 命令列 agent。可以選
[Gemini CLI](https://github.com/google-gemini/gemini-cli)，或
[Antigravity CLI（`agy`）](https://antigravity.google/docs/getting-started)。每則訊息會用 headless 模式交給 CLI 處理，
回覆傳回 Telegram。每個聊天室最近的對話存在 SQLite 裡，所以 bot 記得前後文。

[English](README.md)

## 選擇後端

| | Gemini CLI | Antigravity CLI（帳號登入） | Antigravity CLI（API key） |
|---|---|---|---|
| `GeminiProfiles[].Backend` | `GeminiCli` | `Antigravity` | `Antigravity` |
| 驗證方式 | 付費 Gemini API key，或 Code Assist Standard／Enterprise | 你的 Google 帳號（存在系統 keyring） | Gemini API key，並設 `AntigravityAuth: GeminiApiKey` |
| 誰能用這個 bot | 你允許的人 | **只有你自己**，限一個私聊 | 你允許的人 |
| 額度 | 你的 API 帳單 | 你個人的 Antigravity 額度（每 5 小時／每週） | 你的 API 帳單 |
| 安裝 | `npm install -g @google/gemini-cli`（需要 Node.js 20 以上） | Windows：`irm https://antigravity.google/cli/install.ps1 \| iex`<br>macOS／Linux：`curl -fsSL https://antigravity.google/cli/install.sh \| bash` | 同左 |

**關於條款**：Gemini CLI 的個人 Google 登入已在 2026-06-18 停止服務
（[官方公告](https://developers.googleblog.com/en/an-important-update-transitioning-gemini-cli-to-antigravity-cli/)）。
Antigravity 的部分，依我們的理解（根據一則[論壇回覆](https://discuss.ai.google.dev/t/terms-clarification-personal-antigravity-cli-integration-with-a-multi-ai-orchestrator/187536)，
不是條款原文），用自己的程式呼叫官方的 `agy` 沒有問題；但讓其他人使用你的帳號，就算帳號共用。
所以在帳號登入模式下，`AllowedChatIds` 必須剛好是一個私聊，否則 bot 不會啟動。要給別人用的話，請改用 API key。
部署前請自行確認最新條款。

測試環境：支援 `--policy` 的 Gemini CLI，以及 agy 1.5.9。agy 會自動更新，更新後請重跑一次 `doctor`。

## 快速開始

```bash
git clone https://github.com/Zinzan48/TelegramGeminiBot.git
cd TelegramGeminiBot

# 1. 檢查環境，缺什麼都會印出安裝指令
dotnet run --project TelegramGeminiBot -- doctor
#    要直接安裝目前設定的 CLI（會先問 y/N）：
dotnet run --project TelegramGeminiBot -- doctor --install

# 2. 秘密值放在 repo 之外（Development 環境才會載入）
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:Token" "<你的 bot token>"
#    用 Gemini CLI，或用 API key 跑 Antigravity：
dotnet user-secrets --project TelegramGeminiBot set "GeminiProfiles:0:GeminiApiKey" "<你的 Gemini API key>"
#    用自己的帳號跑 Antigravity：先執行一次 `agy` 完成登入，再切換 profile：
dotnet user-secrets --project TelegramGeminiBot set "ActiveProfiles:Gemini" "Antigravity"

# 3. 啟動
dotnet run --project TelegramGeminiBot
```

傳訊息給 bot 之後，log 會出現 `Ignored message from chat <id>`。把這個 ID 加進白名單，再重新啟動：

```bash
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:AllowedChatIds:0" "<你的 chat id>"
```

bot 啟動時會先檢查設定的 CLI。沒有安裝或版本太舊，會直接停止，並在 log 印出修正用的指令，不會等到第一則訊息才出錯。

正式環境請改用環境變數，層級和陣列索引都用 `__` 分隔：

```powershell
$env:BotProfiles__0__Token = "<你的 bot token>"
$env:BotProfiles__0__AllowedChatIds__0 = "123456789"
$env:GeminiProfiles__0__GeminiApiKey = "<你的 Gemini API key>"
$env:DOTNET_ENVIRONMENT = "Production"
```

## 設定

所有設定都在 `TelegramGeminiBot/appsettings.json`，各欄位說明請見[英文 README 的 Configuration 表格](README.md#configuration)。
這個檔案只是範本，請不要把秘密值寫進去。幾個重點：

- `AllowedChatIds` 留空代表**不回應任何人**。`AllowAllChats: true` 會開放給所有人，但 agy 帳號登入模式不允許這樣設。
- `CliHome` 讓 bot 使用獨立的 CLI 狀態資料夾，不會共用你個人的 MCP server、hooks 和對話紀錄。建議設定。
  實作上，Gemini CLI 是透過 `GEMINI_CLI_HOME`，agy 則是透過 `USERPROFILE`／`HOME`。
- `PolicyPath` 預設會封鎖所有工具，模型只能回文字。Gemini CLI 透過 `--policy` 傳入，agy 透過 `--settings`。
- `WorkingDirectory` 不可以指向含有 bot 自身檔案的資料夾，啟動時會檢查。

## 安全性

Telegram 使用者對跑在你機器上的 AI agent 來說是不受信任的輸入。預設值已經收緊：白名單採 fail-closed、
所有工具一律拒絕、工作目錄獨立、每個使用者都有限流。威脅模型和回報管道請見 [SECURITY.md](SECURITY.md)。

在群組裡，所有成員共用同一份對話歷史。

## 開發

```bash
dotnet build TelegramGeminiBot.slnx
dotnet test TelegramGeminiBot.slnx
```

版本變更請見 [CHANGELOG.md](CHANGELOG.md)。

## 授權

[MIT](LICENSE)
