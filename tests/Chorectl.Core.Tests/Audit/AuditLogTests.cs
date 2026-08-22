using System.Text.Json;
using Chorectl.Core.Audit;

namespace Chorectl.Core.Tests.Audit;

public class AuditLogTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    private string LogPath => Path.Combine(_tempDir, "audit.log");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task RecordAsync_WritesOneJsonLineWithTimestampRepoNumberAndAction()
    {
        var timestamp = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var log = new AuditLog(LogPath);

        await log.RecordAsync(new AuditEntry(timestamp, "sample-repo", 42, "merged"));

        var line = (await File.ReadAllLinesAsync(LogPath)).Single();
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("sample-repo", root.GetProperty("repo").GetString());
        Assert.Equal(42, root.GetProperty("prNumber").GetInt32());
        Assert.Equal("merged", root.GetProperty("action").GetString());
        Assert.Equal(timestamp, root.GetProperty("timestamp").GetDateTimeOffset());
    }

    [Fact]
    public async Task RecordAsync_WithReason_IncludesIt()
    {
        var log = new AuditLog(LogPath);

        await log.RecordAsync(new AuditEntry(DateTimeOffset.UtcNow, "sample-repo", 1, "skipped", "conflicting"));

        var line = (await File.ReadAllLinesAsync(LogPath)).Single();
        using var doc = JsonDocument.Parse(line);
        Assert.Equal("conflicting", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task RecordAsync_WithNoReason_OmitsTheField()
    {
        var log = new AuditLog(LogPath);

        await log.RecordAsync(new AuditEntry(DateTimeOffset.UtcNow, "sample-repo", 1, "merged"));

        var line = (await File.ReadAllLinesAsync(LogPath)).Single();
        using var doc = JsonDocument.Parse(line);
        Assert.False(doc.RootElement.TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task RecordAsync_CalledMultipleTimes_AppendsRatherThanOverwriting()
    {
        var log = new AuditLog(LogPath);

        await log.RecordAsync(new AuditEntry(DateTimeOffset.UtcNow, "sample-repo", 1, "merged"));
        await log.RecordAsync(new AuditEntry(DateTimeOffset.UtcNow, "sample-repo", 2, "failed", "unexpected error"));

        var lines = await File.ReadAllLinesAsync(LogPath);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task RecordAsync_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "audit.log");
        var log = new AuditLog(nestedPath);

        await log.RecordAsync(new AuditEntry(DateTimeOffset.UtcNow, "sample-repo", 1, "merged"));

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void DefaultPath_IsUnderLocalShareChorectl()
    {
        Assert.EndsWith(Path.Combine(".local", "share", "chorectl", "audit.log"), AuditLog.DefaultPath);
    }
}
