using System.Text.Json;
using System.Text.Json.Nodes;

namespace TelegramGeminiBot.Services;

public sealed record AntigravityResult(string Status, string? Response, string? Error)
{
    public bool IsSuccess => string.Equals(Status, "SUCCESS", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Encodes prompts for and parses results from <c>agy --input-format stream-json --output-format stream-json</c>.
/// See https://antigravity.google/docs/cli/headless.
/// </summary>
public static class AntigravityStreamJson
{
    /// <summary>One NDJSON line: <c>{"event":"user","message":{"content":"..."}}</c>.</summary>
    public static string BuildInputLine(string prompt)
    {
        var node = new JsonObject
        {
            ["event"] = "user",
            ["message"] = new JsonObject { ["content"] = prompt }
        };
        return node.ToJsonString();
    }

    /// <summary>
    /// Returns the last <c>result</c> event in the stream, or null when the stream has none.
    /// Non-JSON lines and other event types (init, step_update) are ignored.
    /// </summary>
    public static AntigravityResult? ParseResult(IEnumerable<string> lines)
    {
        AntigravityResult? last = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] != '{')
            {
                continue;
            }

            JsonNode? node;
            try
            {
                node = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if (node?["event"]?.GetValue<string>() != "result" || node["result"] is not JsonObject result)
            {
                continue;
            }

            last = new AntigravityResult(
                Status: result["status"]?.GetValue<string>() ?? "UNKNOWN",
                Response: result["response"]?.GetValue<string>(),
                Error: result["error"]?.GetValue<string>());
        }

        return last;
    }

    /// <summary>
    /// Builds the inline <c>--settings</c> JSON: the deny-all policy file plus, for API-key auth,
    /// <c>"modelProvider": "gemini"</c> (required together with GEMINI_API_KEY).
    /// </summary>
    public static string BuildSettings(string? policyJson, bool useGeminiApiKey)
    {
        var settings = string.IsNullOrWhiteSpace(policyJson)
            ? new JsonObject()
            : JsonNode.Parse(policyJson) as JsonObject ?? throw new InvalidOperationException("Antigravity policy file must contain a JSON object.");

        if (useGeminiApiKey)
        {
            settings["modelProvider"] = "gemini";
        }

        return settings.ToJsonString();
    }
}
