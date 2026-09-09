using Chorectl.Core.Update;
using Spectre.Console;

namespace Chorectl.Cli.Rendering.Update;

/// <summary>
/// Runs <c>chorectl update</c> under a live-updating Spectre.Console status display, following the
/// same AnsiConsole.Status() pattern as Rendering.Dependabot.ProgressDisplay.RunLivePollAsync.
/// </summary>
public static class UpdateProgressDisplay
{
    public static Task<UpdateResult> RunAsync(IAnsiConsole console, Func<IProgress<UpdateProgress>, Task<UpdateResult>> update) =>
        console.Status().StartAsync(
            Describe(new UpdateProgress(UpdateStage.CheckingForRelease)),
            ctx => update(new SynchronousProgress<UpdateProgress>(p => ctx.Status(Describe(p)))));

    private static string Describe(UpdateProgress progress) => progress.Stage switch
    {
        UpdateStage.CheckingForRelease => "Checking for the latest release...",
        UpdateStage.Downloading => $"Downloading update{Percentage(progress)}...",
        UpdateStage.Extracting => "Extracting update...",
        UpdateStage.ReplacingExecutable => "Replacing executable...",
        _ => "",
    };

    private static string Percentage(UpdateProgress progress) =>
        progress is { BytesDownloaded: not null, TotalBytes: > 0 }
            ? $" ({progress.BytesDownloaded!.Value * 100 / progress.TotalBytes!.Value}%)"
            : "";

    /// <summary>Reports synchronously on the calling thread — unlike <see cref="Progress{T}"/>, which
    /// posts through a captured SynchronizationContext and could reorder updates relative to a status
    /// display driven from the same async flow.</summary>
    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
