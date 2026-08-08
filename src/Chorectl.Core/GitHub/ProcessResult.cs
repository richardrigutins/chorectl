namespace Chorectl.Core.GitHub;

/// <summary>
/// The outcome of running an external process to completion.
/// </summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="StandardOutput">The process's standard output, trimmed.</param>
/// <param name="StandardError">The process's standard error, trimmed.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
