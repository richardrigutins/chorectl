using System.Diagnostics;
using System.Text;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Default <see cref="IProcessRunner"/> that runs a real OS process.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    public ProcessResult Run(string fileName, string arguments)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");

        // Reading stdout to completion before even starting to read stderr (or vice versa) can
        // deadlock: if the child fills the OS pipe buffer on the stream nobody's draining yet, its
        // write blocks, which means it never exits and closes the stream being read - which never
        // returns either. Draining both concurrently via the async line-received events (the
        // pattern Process itself documents for this) avoids that regardless of output volume.
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout.ToString().Trim(), stderr.ToString().Trim());
    }
}
