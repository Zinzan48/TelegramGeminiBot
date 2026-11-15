# TelegramGeminiBot（繁體中文）

> [!IMPORTANT]
> v0.2.0 改了設定格式，`AllowedChatIds` 留空現在代表誰都不回。Gemini CLI 也從 2026-06-18 起不再接受
> 個人 Google 帳號登入。升級前先看 [CHANGELOG 的升級指南](CHANGELOG.md#upgrading-from-010)。

用 .NET 10 寫的小服務，把 Telegram bot 接到 AI 命令列工具上，可以選
[Gemini CLI](https://github.com/google-gemini/gemini-cli) 或
[Antigravity CLI（`agy`）](https://antigravity.google/docs/getting-started)。收到訊息就丟給 CLI 跑 headless 模式，
答案再傳回 Telegram。每個聊天室最近的對話存在 SQLite，bot 才接得上前文。

[English](README.md)

## 選擇後端

| | Gemini CLI | Antigravity CLI（帳號登入） | Antigravity CLI（API key） |
|---|---|---|---|
| `GeminiProfiles[].Backend` | `GeminiCli` | `Antigravity` | `Antigravity` |
| 驗證方式 | 付費 Gemini API key，或 Code Assist Standard／Enterprise | 你的 Google 帳號（存在系統 keyring） | Gemini API key，並設 `AntigravityAuth: GeminiApiKey` |
| 誰能用這個 bot | 你允許的人 | 只有你自己（限一個私聊） | 你允許的人 |
| 額度 | 你的 API 帳單 | 你個人的 Antigravity 額度（每 5 小時／每週） | 你的 API 帳單 |
| 安裝 | `npm install -g @google/gemini-cli`（需要 Node.js，見[需求](#需求)） | Windows：`irm https://antigravity.google/cli/install.ps1 \| iex`<br>macOS／Linux：`curl -fsSL https://antigravity.google/cli/install.sh \| bash` | 同左 |

### 條款

Gemini CLI 的個人 Google 登入在 2026-06-18 停了
（[官方公告](https://developers.googleblog.com/en/an-important-update-transitioning-gemini-cli-to-antigravity-cli/)）。

Antigravity 這邊，我們的判斷依據是一則[論壇回覆](https://discuss.ai.google.dev/t/terms-clarification-personal-antigravity-cli-integration-with-a-multi-ai-orchestrator/187536)，
不是條款原文：自己寫程式去呼叫官方 `agy` 可以，讓別人用你的帳號就算帳號共用。所以帳號登入模式下，
`AllowedChatIds` 一定要剛好一個私聊，bot 才會啟動。要給別人用就改用 API key。部署前請自己再看一次最新條款。

### 測過什麼

2026-11-15 用 agy 1.5.9 測過：叫它讀一個放了假 token 的檔案，它回 `Permission denied for read_file: Matches user-configured deny rule`；
用預設的 `CliHome` 時登入照常可用，對話也沒有跑進個人的 agy 紀錄。agy 會自己更新，更新後重跑一次 `doctor`。
Gemini CLI 後端這一版沒有做端到端測試。

## 下載 Release 版本

每個 [release](https://github.com/Zinzan48/TelegramGeminiBot/releases) 都有六個 zip，由 GitHub Actions 從打 tag 的那個 commit 編譯：

| 平台 | 內含 .NET（zip 約 32 MB） | 需要 .NET 10 Runtime（zip 約 2.5 MB） |
|---|---|---|
| Windows x64 | `…-win-x64-self-contained.zip` | `…-win-x64-framework-dependent.zip` |
| Linux x64 | `…-linux-x64-self-contained.zip` | `…-linux-x64-framework-dependent.zip` |
| Linux Arm64 | `…-linux-arm64-self-contained.zip` | `…-linux-arm64-framework-dependent.zip` |

framework-dependent 版要先裝 [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)，選「.NET Runtime」那一項就夠了，
不用裝 ASP.NET Core Runtime 或 Desktop Runtime。

解壓後先跑一次 doctor：

```bash
./TelegramGeminiBot doctor        # Windows：.\TelegramGeminiBot.exe doctor
```

Release 版是用 Production 環境執行，不會讀 user-secrets。token 和 chat ID 請用環境變數給
（`BotProfiles__0__Token`、`BotProfiles__0__AllowedChatIds__0`），或直接改解壓資料夾裡的 `appsettings.json`。
那份是你自己的副本，只存在你的電腦上。聊天資料庫、`gemini-workspace` 和 `agy-home` 會建在程式旁邊。

執行檔沒有程式碼簽章，第一次執行時 Windows SmartScreen 可能會擋下來，點「其他資訊」再點「仍要執行」即可。
想確認 zip 真的是這個 repo 編譯出來的，可以這樣驗證：

```bash
gh attestation verify TelegramGeminiBot-0.2.0-win-x64-self-contained.zip --repo Zinzan48/TelegramGeminiBot
sha256sum -c SHA256SUMS.txt --ignore-missing
```

## 需求

從原始碼執行需要：

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)。.NET 10 是目前的 LTS 版本，支援到 2028-11-14，
  專案就是以它為目標。`dotnet --version` 應該顯示 10.0.x；請裝最新的 patch，安全性修正都靠它進來。
- Node.js，只有 Gemini CLI 後端需要。Gemini CLI 接受 20 以上，但 Node 20 在 2026-04-30 就停止更新了，
  請用目前的 LTS（24 或 26）。`doctor` 看到 Node 20 會提醒。
- agy，只有 Antigravity 後端需要。它是獨立的執行檔，不用裝 Node.js。
- 向 [@BotFather](https://t.me/BotFather) 申請的 bot token。

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

bot 一啟動就會檢查設定的 CLI。沒裝或版本太舊會直接停下來，log 裡附上修正用的指令。

正式環境請改用環境變數，層級和陣列索引都用 `__` 分隔：

```powershell
$env:BotProfiles__0__Token = "<你的 bot token>"
$env:BotProfiles__0__AllowedChatIds__0 = "123456789"
$env:GeminiProfiles__0__GeminiApiKey = "<你的 Gemini API key>"
$env:DOTNET_ENVIRONMENT = "Production"
```

## 設定

設定都在 `TelegramGeminiBot/appsettings.json`，每個欄位的說明見[英文 README 的 Configuration 表格](README.md#configuration)。
這個檔案是範本，秘密值不要寫進去。比較需要注意的幾個：

- `AllowedChatIds` 留空就誰都不回。`AllowAllChats: true` 會開放給所有人，agy 帳號登入模式不能這樣設。
- `CliHome` 是 CLI 專用的狀態資料夾，讓 bot 不去用你個人的設定、記憶、MCP server 和對話紀錄。
  agy 後端預設 `./agy-home`，登入資訊存在系統 keyring，換資料夾後照樣可以用。Gemini CLI 後端預設不隔離，
  設成 `none` 則關掉。底層做法是：Gemini CLI 設 `GEMINI_CLI_HOME`，agy 設 `USERPROFILE`／`HOME`。
- `PolicyPath` 預設擋掉所有工具，模型只能回文字。Gemini CLI 用 `--policy` 帶入，agy 用 `--settings`。
- `WorkingDirectory` 不能指到放 bot 自己檔案的資料夾，啟動時會檢查。

## 安全性

Telegram 上任何人打的字，最後都會變成你機器上 AI agent 的輸入。所以預設只回白名單裡的聊天室，CLI 拿不到任何工具，
在獨立的工作目錄執行，每個人也有次數限制。威脅模型和回報方式寫在 [SECURITY.md](SECURITY.md)。

群組裡的成員共用同一份對話紀錄。

## 開發

```bash
dotnet build TelegramGeminiBot.slnx
dotnet test TelegramGeminiBot.slnx
```

Dependabot 的套件更新會先合進 `develop`。發版時開 PR 把 `develop` 合進 `master`，再在合併 commit 上打 tag，
CI 就會開始編譯 Release。安全性更新例外，會直接進 `master`，之後再合回 `develop`。跟著 .NET 一起發版的套件固定在 10.x。

版本變更請見 [CHANGELOG.md](CHANGELOG.md)。

## 授權

[MIT](LICENSE)
