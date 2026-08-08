namespace Chorectl.Core.GitHub;

/// <summary>
/// Runs an external process to completion and captures its result.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with the given <paramref name="arguments"/> and waits for it to exit.
    /// </summary>
    /// <returns>The process's exit code and captured output.</returns>
    ProcessResult Run(string fileName, string arguments);
}
