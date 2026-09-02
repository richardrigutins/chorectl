using System.Formats.Tar;
using System.IO.Compression;
using Chorectl.Cli.Commands;
using Chorectl.Core.GitHub;
using Chorectl.Core.Update;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands;

public class UpdateCommandTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task RunAsync_WhenAlreadyOnTheLatestVersion_PrintsSoAndDoesNotTouchTheExecutable()
    {
        var console = new TestConsole();
        var releaseSource = new FakeReleaseSource(new ReleaseInfo(CurrentVersion.Value, []));
        var updater = new Updater(releaseSource, new HttpClient(new FakeBinaryHttpMessageHandler([])), isWindows: false);
        var command = new UpdateCommand(updater, console);
        var executablePath = WriteExecutable("original content");

        var exitCode = await command.RunAsync(executablePath, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Already on the latest version", console.Output);
        Assert.Equal("original content", File.ReadAllText(executablePath));
    }

    [Fact]
    public async Task RunAsync_WhenANewerVersionExists_ReplacesTheExecutable_AndReportsTheTransition()
    {
        var console = new TestConsole();
        var releaseSource = new FakeReleaseSource(new ReleaseInfo(
            "v99.0.0",
            [new ReleaseAsset("chorectl-linux-x64.tar.gz", "https://example.test/chorectl-linux-x64.tar.gz")]));
        var updater = new Updater(releaseSource, new HttpClient(new FakeBinaryHttpMessageHandler(BuildTarGz("chorectl", "new content"))), isWindows: false);
        var command = new UpdateCommand(updater, console);
        var executablePath = WriteExecutable("original content");

        var exitCode = await command.RunAsync(executablePath, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Updated", console.Output);
        Assert.Contains("v99.0.0", console.Output);
        Assert.Equal("new content", File.ReadAllText(executablePath));
    }

    private string WriteExecutable(string content)
    {
        var path = Path.Combine(_tempDir, "chorectl");
        File.WriteAllText(path, content);
        return path;
    }

    private static byte[] BuildTarGz(string entryName, string content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        using (var tarWriter = new TarWriter(gzip, leaveOpen: true))
        {
            var entryContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            var entry = new PaxTarEntry(TarEntryType.RegularFile, entryName) { DataStream = entryContent };
            tarWriter.WriteEntry(entry);
        }

        return output.ToArray();
    }
}
