# TelegramGeminiBot（繁體中文）

這是一個 .NET 10 Worker 服務，負責把 Telegram bot 接到 [Gemini CLI](https://github.com/google-gemini/gemini-cli)。
收到的每則 Telegram 訊息會用 headless 模式送進 `gemini`，再把回覆傳回 Telegram。每個聊天室最近的對話存在
SQLite 裡，所以 bot 會記得前後文。

[English](README.md)

## 部署前請先看

- **服務條款**：請先確認你的 Gemini CLI 登入方式允許這種用法。[Gemini CLI 條款](https://geminicli.com/docs/resources/tos-privacy)
  寫明，透過第三方軟體存取它的後端服務可能導致帳號停權；條款也沒有提到可以用個人 Google 帳號替其他人服務。
  如果 bot 會給別人用，改用 API key（`GEMINI_API_KEY`）或 Vertex AI 比較穩妥。
- **免費層**：同一頁寫到，免費與 Google One 使用者的 Gemini CLI 已在 2026-06-18 由 Antigravity CLI 取代。
  要使用本專案，需要 API key、付費的 Code Assist 或 Vertex AI。
- **誰能跟 bot 說話**：預設沒有任何人。請把 chat ID 加進 `AllowedChatIds`（見下方）。

## 需求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js 20 以上，以及 Gemini CLI：`npm install -g @google/gemini-cli`，並完成登入或設定好 API key。
  預設的工具封鎖需要 CLI 支援 `--policy` 參數。
- 向 [@BotFather](https://t.me/BotFather) 申請的 bot token

## 快速開始

```bash
git clone https://github.com/Zinzan48/TelegramGeminiBot.git
cd TelegramGeminiBot

# 秘密值放在 repo 之外（Development 環境才會載入）
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:Token" "<你的 bot token>"

dotnet run --project TelegramGeminiBot
```

第一次跑起來後傳訊息給 bot，log 會出現 `Ignored message from chat <id>`。把這個 ID 加進
`AllowedChatIds`，再重新啟動：

```bash
dotnet user-secrets --project TelegramGeminiBot set "BotProfiles:0:AllowedChatIds:0" "<你的 chat id>"
```

正式環境請改用環境變數，層級和陣列索引都用 `__` 分隔：

```powershell
$env:BotProfiles__0__Token = "<你的 bot token>"
$env:BotProfiles__0__AllowedChatIds__0 = "123456789"
$env:DOTNET_ENVIRONMENT = "Production"
```

## 設定

所有設定欄位都在 `TelegramGeminiBot/appsettings.json`，各欄位說明請見 [英文 README 的 Configuration 表格](README.md#configuration)。
這個檔案只是範本，請不要把秘密值寫進去。幾個重點：

- `AllowedChatIds` 留空代表**不回應任何人**。`AllowAllChats: true` 才會開放給所有人，但任何找到 bot 的人都能用掉你的額度。
- `CliHome` 會設定 `GEMINI_CLI_HOME`，讓 bot 使用自己的 `.gemini` 資料夾，不會繼承你個人的 MCP server、hooks 和對話紀錄。建議設定。
- `PolicyPath` 預設是 `gemini-policies/deny-all-tools.toml`，會封鎖所有工具，模型只能回文字。
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

## 授權

[MIT](LICENSE)
