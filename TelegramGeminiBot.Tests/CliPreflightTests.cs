using TelegramGeminiBot.Configuration;
using TelegramGeminiBot.Services;

namespace TelegramGeminiBot.Tests;

public sealed class CliPreflightTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tgb-path-" + Guid.NewGuid().ToString("N"));

    public CliPreflightTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Touch(string name)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    [Fact]
    public void Windows_FindsCmdShimThroughPathExt()
    {
        Touch("gemini");          // npm's POSIX shim: not runnable on Windows
        var cmd = Touch("gemini.cmd");

        var resolved = CliPreflight.ResolveExecutable("gemini", "C:\\nope" + Path.PathSeparator + _dir, ".EXE;.CMD", isWindows: true);

        Assert.Equal(cmd, resolved, ignoreCase: true);
    }

    [Fact]
    public void Unix_FindsExtensionlessExecutable()
    {
        var agy = Touch("agy");

        Assert.Equal(agy, CliPreflight.ResolveExecutable("agy", _dir, null, isWindows: false));
    }

    [Fact]
    public void ExplicitPath_IsUsedAsIs()
    {
        var exe = Touch("agy.exe");

        Assert.Equal(exe, CliPreflight.ResolveExecutable(exe, pathVariable: "", pathExtVariable: null, isWindows: true), ignoreCase: true);
    }

    [Fact]
    public void Missing_ReturnsNull()
    {
        Assert.Null(CliPreflight.ResolveExecutable("definitely-not-installed", _dir, ".EXE", isWindows: true));
    }

    [Fact]
    public void MissingFlags_ListsOnlyAbsentFlags()
    {
        const string help = "  --settings   Settings\n  --input-format  Input format\n";

        Assert.Equal(["--sandbox"], CliPreflight.MissingFlags(help, ["--settings", "--input-format", "--sandbox"]));
    }

    [Theory]
    [InlineData(AiBackend.GeminiCli, "npm install -g @google/gemini-cli")]
    [InlineData(AiBackend.Antigravity, "antigravity.google/cli/install")]
    public void InstallFix_ContainsOfficialInstallCommandAndDoctorHint(AiBackend backend, string expected)
    {
        var fix = CliPreflight.FormatInstallFix(BackendInfo.For(backend));

        Assert.Contains(expected, fix);
        Assert.Contains("doctor --install", fix);
    }

    [Fact]
    public async Task CheckBackend_WhenCliIsMissing_ReportsErrorWithInstallCommand()
    {
        var options = new GeminiOptions { Backend = AiBackend.Antigravity, ExecutablePath = Path.Combine(_dir, "missing-agy.exe") };

        var checks = await CliPreflight.CheckBackendAsync(options, CancellationToken.None);

        var check = Assert.Single(checks);
        Assert.Equal(CheckStatus.Error, check.Status);
        Assert.Contains("antigravity.google/cli/install", check.Fix);
    }
}
