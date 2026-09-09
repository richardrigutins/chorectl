using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Chorectl.Core.GitHub;

namespace Chorectl.Core.Update;

/// <summary>
/// Downloads the release archive matching the current OS/arch and replaces the running executable
/// in place. Asset naming mirrors install.sh/install.ps1: <c>chorectl-{rid}.tar.gz</c> for Unix
/// RIDs, <c>chorectl-win-x64.zip</c> for Windows.
/// </summary>
/// <param name="isWindows">
/// Overridable for tests, which exercise both platforms' file-swap strategy regardless of the
/// host OS actually running them; defaults to <see cref="OperatingSystem.IsWindows"/>.
/// </param>
public sealed class Updater(IReleaseSource releaseSource, HttpClient httpClient, bool? isWindows = null)
{
    private readonly bool isWindows = isWindows ?? OperatingSystem.IsWindows();

    public async Task<UpdateResult> UpdateAsync(string currentVersion, string executablePath, IProgress<UpdateProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report(new UpdateProgress(UpdateStage.CheckingForRelease));
        var release = await releaseSource.GetLatestReleaseAsync();

        if (!VersionComparer.IsNewer(release.TagName, currentVersion))
        {
            return new UpdateResult(Updated: false, currentVersion, release.TagName);
        }

        var assetName = ResolveAssetName();
        var asset = release.Assets.FirstOrDefault(a => a.Name == assetName)
            ?? throw new InvalidOperationException($"Release {release.TagName} has no asset named '{assetName}'.");

        var tempDir = Directory.CreateTempSubdirectory("chorectl-update-");
        try
        {
            var extractedBinaryPath = await DownloadAndExtractAsync(asset, tempDir.FullName, progress, cancellationToken);
            progress?.Report(new UpdateProgress(UpdateStage.ReplacingExecutable));
            ReplaceExecutable(extractedBinaryPath, executablePath);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }

        return new UpdateResult(Updated: true, currentVersion, release.TagName);
    }

    private string ResolveAssetName()
    {
        if (isWindows)
        {
            return "chorectl-win-x64.zip";
        }

        var os = OperatingSystem.IsMacOS() ? "osx" : "linux";
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        return $"chorectl-{os}-{arch}.tar.gz";
    }

    private async Task<string> DownloadAndExtractAsync(ReleaseAsset asset, string tempDir, IProgress<UpdateProgress>? progress, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength;

        var buffer = new MemoryStream();
        await using (var networkStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        {
            var chunk = new byte[81920];
            long bytesRead = 0;
            progress?.Report(new UpdateProgress(UpdateStage.Downloading, bytesRead, totalBytes));

            int read;
            while ((read = await networkStream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
                bytesRead += read;
                progress?.Report(new UpdateProgress(UpdateStage.Downloading, bytesRead, totalBytes));
            }
        }

        buffer.Position = 0;
        progress?.Report(new UpdateProgress(UpdateStage.Extracting));

        if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(buffer, tempDir);
        }
        else
        {
            await using var gzip = new GZipStream(buffer, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, tempDir, overwriteFiles: true, cancellationToken);
        }

        return Path.Combine(tempDir, isWindows ? "chorectl.exe" : "chorectl");
    }

    /// <summary>
    /// On Unix, a plain rename over the running executable's path is atomic - the OS keeps a
    /// running process attached to the file's inode, not its path. On Windows, which won't allow
    /// overwriting a running .exe directly, the current binary is renamed aside first; the leftover
    /// <c>.old</c> file is cleaned up on next launch (see <see cref="CleanupPreviousVersion"/>).
    /// </summary>
    private void ReplaceExecutable(string newBinaryPath, string executablePath)
    {
        if (!isWindows)
        {
            File.Move(newBinaryPath, executablePath, overwrite: true);
            return;
        }

        var oldPath = executablePath + ".old";
        TryDelete(oldPath);
        File.Move(executablePath, oldPath);
        File.Move(newBinaryPath, executablePath);
    }

    /// <summary>
    /// Deletes a leftover <c>{executablePath}.old</c> from a previous Windows update, if any.
    /// Called unconditionally at startup (Program.cs) - a no-op everywhere else, and a best-effort
    /// one on Windows itself: a failure (still locked, e.g. by an antivirus scan) just leaves it
    /// for the next launch to retry, it doesn't block startup.
    /// </summary>
    public static void CleanupPreviousVersion(string executablePath) => TryDelete(executablePath + ".old");

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort, see CleanupPreviousVersion.
        }
    }
}
