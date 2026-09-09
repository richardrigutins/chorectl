using System.Formats.Tar;
using System.IO.Compression;
using Chorectl.Core.GitHub;
using Chorectl.Core.Update;

namespace Chorectl.Core.Tests.Update;

public class UpdaterTests : IDisposable
{
    private const string CurrentVersion = "1.0.0";

    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task UpdateAsync_WhenAlreadyOnTheLatestVersion_DoesNotDownload_AndReportsNotUpdated()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v1.0.0", []));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler([]));
        var updater = new Updater(releaseSource, httpClient, isWindows: false);
        var executablePath = WriteExecutable("chorectl", "old content");

        var result = await updater.UpdateAsync(CurrentVersion, executablePath);

        Assert.False(result.Updated);
        Assert.Equal("v1.0.0", result.LatestVersion);
        Assert.Equal("old content", File.ReadAllText(executablePath));
    }

    [Fact]
    public async Task UpdateAsync_WhenNoAssetMatchesTheCurrentPlatform_Throws()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v2.0.0", []));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler([]));
        var updater = new Updater(releaseSource, httpClient, isWindows: false);
        var executablePath = WriteExecutable("chorectl", "old content");

        await Assert.ThrowsAsync<InvalidOperationException>(() => updater.UpdateAsync(CurrentVersion, executablePath));
    }

    [Fact]
    public async Task UpdateAsync_OnUnix_DownloadsExtractsTarGz_AndAtomicallyReplacesTheExecutable()
    {
        var assetBytes = BuildTarGz("chorectl", "new content");
        var releaseSource = new FakeReleaseSource(new ReleaseInfo(
            "v2.0.0",
            [new ReleaseAsset("chorectl-linux-x64.tar.gz", "https://example.test/chorectl-linux-x64.tar.gz")]));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler(assetBytes));
        var updater = new Updater(releaseSource, httpClient, isWindows: false);
        var executablePath = WriteExecutable("chorectl", "old content");

        var result = await updater.UpdateAsync(CurrentVersion, executablePath);

        Assert.True(result.Updated);
        Assert.Equal("v2.0.0", result.LatestVersion);
        Assert.Equal("new content", File.ReadAllText(executablePath));
        Assert.False(File.Exists(executablePath + ".old"));
    }

    [Fact]
    public async Task UpdateAsync_OnWindows_RenamesTheRunningExecutableAside_ThenPlacesTheNewBinary()
    {
        var assetBytes = BuildZip("chorectl.exe", "new content");
        var releaseSource = new FakeReleaseSource(new ReleaseInfo(
            "v2.0.0",
            [new ReleaseAsset("chorectl-win-x64.zip", "https://example.test/chorectl-win-x64.zip")]));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler(assetBytes));
        var updater = new Updater(releaseSource, httpClient, isWindows: true);
        var executablePath = WriteExecutable("chorectl.exe", "old content");

        var result = await updater.UpdateAsync(CurrentVersion, executablePath);

        Assert.True(result.Updated);
        Assert.Equal("new content", File.ReadAllText(executablePath));
        Assert.Equal("old content", File.ReadAllText(executablePath + ".old"));
    }

    [Fact]
    public async Task UpdateAsync_OnWindows_WhenALeftoverOldFileAlreadyExists_OverwritesIt()
    {
        var assetBytes = BuildZip("chorectl.exe", "new content");
        var releaseSource = new FakeReleaseSource(new ReleaseInfo(
            "v2.0.0",
            [new ReleaseAsset("chorectl-win-x64.zip", "https://example.test/chorectl-win-x64.zip")]));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler(assetBytes));
        var updater = new Updater(releaseSource, httpClient, isWindows: true);
        var executablePath = WriteExecutable("chorectl.exe", "old content");
        File.WriteAllText(executablePath + ".old", "stale leftover");

        await updater.UpdateAsync(CurrentVersion, executablePath);

        Assert.Equal("old content", File.ReadAllText(executablePath + ".old"));
    }

    [Fact]
    public async Task UpdateAsync_WhenAlreadyOnTheLatestVersion_OnlyReportsCheckingForRelease()
    {
        var releaseSource = new FakeReleaseSource(new ReleaseInfo("v1.0.0", []));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler([]));
        var updater = new Updater(releaseSource, httpClient, isWindows: false);
        var executablePath = WriteExecutable("chorectl", "old content");
        var reported = new List<UpdateProgress>();

        await updater.UpdateAsync(CurrentVersion, executablePath, new RecordingProgress(reported));

        Assert.Equal([new UpdateProgress(UpdateStage.CheckingForRelease)], reported);
    }

    [Fact]
    public async Task UpdateAsync_WhenUpdating_ReportsEachStageInOrder_WithDownloadBytesAgainstContentLength()
    {
        var assetBytes = BuildTarGz("chorectl", "new content");
        var releaseSource = new FakeReleaseSource(new ReleaseInfo(
            "v2.0.0",
            [new ReleaseAsset("chorectl-linux-x64.tar.gz", "https://example.test/chorectl-linux-x64.tar.gz")]));
        var httpClient = new HttpClient(new FakeBinaryHttpMessageHandler(assetBytes));
        var updater = new Updater(releaseSource, httpClient, isWindows: false);
        var executablePath = WriteExecutable("chorectl", "old content");
        var reported = new List<UpdateProgress>();

        await updater.UpdateAsync(CurrentVersion, executablePath, new RecordingProgress(reported));

        Assert.Equal(UpdateStage.CheckingForRelease, reported.First().Stage);
        Assert.Equal(UpdateStage.ReplacingExecutable, reported.Last().Stage);
        Assert.Contains(reported, p => p.Stage == UpdateStage.Extracting);

        var downloadUpdates = reported.Where(p => p.Stage == UpdateStage.Downloading).ToList();
        Assert.NotEmpty(downloadUpdates);
        Assert.All(downloadUpdates, p => Assert.Equal(assetBytes.Length, p.TotalBytes));
        Assert.Equal(assetBytes.Length, downloadUpdates.Last().BytesDownloaded);
    }

    private sealed class RecordingProgress(List<UpdateProgress> reported) : IProgress<UpdateProgress>
    {
        public void Report(UpdateProgress value) => reported.Add(value);
    }

    private string WriteExecutable(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static byte[] BuildZip(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return stream.ToArray();
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
