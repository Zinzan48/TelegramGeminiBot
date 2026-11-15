using System.Text.Json.Nodes;
using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public class AntigravityStreamJsonTests
{
    [Theory]
    [InlineData("plain")]
    [InlineData("quote \" and backslash \\")]
    [InlineData("line1\nline2\r\nline3")]
    [InlineData("中文與 emoji 😀")]
    public void BuildInputLine_IsSingleLineJson_ThatRoundTripsThePrompt(string prompt)
    {
        var line = AntigravityStreamJson.BuildInputLine(prompt);

        Assert.DoesNotContain('\n', line);
        var node = JsonNode.Parse(line)!;
        Assert.Equal("user", node["event"]!.GetValue<string>());
        Assert.Equal(prompt, node["message"]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void ParseResult_ReturnsSuccessResponse_IgnoringOtherEvents()
    {
        string[] lines =
        [
            """{"event":"init","conversation_id":"c1","init":{"cwd":"x","tools":["view_file"],"permission_mode":"request-review"}}""",
            """{"event":"step_update","step_update":{"state":"ACTIVE","step_type":"agent_response","text_delta":"Hel"}}""",
            "not json at all",
            """{"event":"result","result":{"conversation_id":"c1","status":"SUCCESS","response":"Hello!","num_turns":1}}"""
        ];

        var result = AntigravityStreamJson.ParseResult(lines);

        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        Assert.Equal("Hello!", result.Response);
    }

    [Fact]
    public void ParseResult_ReturnsError()
    {
        var result = AntigravityStreamJson.ParseResult(
            ["""{"event":"result","result":{"status":"ERROR","error":"authentication required"}}"""]);

        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal("authentication required", result.Error);
    }

    [Fact]
    public void ParseResult_WithoutResultEvent_ReturnsNull()
    {
        Assert.Null(AntigravityStreamJson.ParseResult(["""{"event":"init","init":{}}""", ""]));
    }

    [Fact]
    public void BuildSettings_AddsModelProviderOnlyForApiKey()
    {
        const string policy = """{"permissions":{"deny":["read_file(*)"]}}""";

        var account = JsonNode.Parse(AntigravityStreamJson.BuildSettings(policy, useGeminiApiKey: false))!;
        var apiKey = JsonNode.Parse(AntigravityStreamJson.BuildSettings(policy, useGeminiApiKey: true))!;

        Assert.Null(account["modelProvider"]);
        Assert.Equal("read_file(*)", account["permissions"]!["deny"]![0]!.GetValue<string>());
        Assert.Equal("gemini", apiKey["modelProvider"]!.GetValue<string>());
        Assert.NotNull(apiKey["permissions"]);
    }

    [Fact]
    public void ShippedPolicy_DeniesEveryToolCategory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "antigravity-policies", "deny-all-tools.json");
        var deny = JsonNode.Parse(File.ReadAllText(path))!["permissions"]!["deny"]!.AsArray()
            .Select(n => n!.GetValue<string>()).ToHashSet();

        Assert.Superset(new HashSet<string> { "read_file(*)", "write_file(*)", "command(*)", "read_url(*)", "execute_url(*)", "mcp(*)" }, deny);
    }
}
