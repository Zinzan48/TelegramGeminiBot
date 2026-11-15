using Microsoft.Data.Sqlite;

namespace TelegramGeminiBot.Storage;

public sealed class ChatHistoryStore
{
    private readonly string _connectionString;

    public ChatHistoryStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS chat_messages (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    chat_id INTEGER NOT NULL,
    role TEXT NOT NULL,
    content TEXT NOT NULL,
    created_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_chat_messages_chat_id_created_utc
    ON chat_messages(chat_id, created_utc DESC);";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddMessageAsync(long chatId, string role, string content, DateTimeOffset createdUtc, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO chat_messages (chat_id, role, content, created_utc) VALUES ($chatId, $role, $content, $createdUtc);";
        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$role", role);
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$createdUtc", createdUtc.UtcDateTime.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetRecentMessagesAsync(long chatId, int maxMessages, CancellationToken cancellationToken)
    {
        if (maxMessages <= 0)
        {
            return Array.Empty<ChatMessage>();
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT role, content, created_utc
FROM chat_messages
WHERE chat_id = $chatId
ORDER BY id DESC
LIMIT $limit;";
        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$limit", maxMessages);

        var results = new List<ChatMessage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var role = reader.GetString(0);
            var content = reader.GetString(1);
            var createdUtcRaw = reader.GetString(2);
            var createdUtc = DateTimeOffset.Parse(createdUtcRaw, null, System.Globalization.DateTimeStyles.RoundtripKind);
            results.Add(new ChatMessage(role, content, createdUtc));
        }

        results.Reverse();
        return results;
    }

    public async Task TrimHistoryAsync(long chatId, int keepLast, CancellationToken cancellationToken)
    {
        if (keepLast <= 0)
        {
            return;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
DELETE FROM chat_messages
WHERE chat_id = $chatId
AND id NOT IN (
    SELECT id FROM chat_messages
    WHERE chat_id = $chatId
    ORDER BY id DESC
    LIMIT $keepLast
);";
        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$keepLast", keepLast);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
