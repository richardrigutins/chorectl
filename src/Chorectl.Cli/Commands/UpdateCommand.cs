using Chorectl.Cli.Rendering.Update;
using Chorectl.Core.Update;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands;

/// <summary>
/// <c>chorectl update</c> - downloads and installs the latest release in place.
/// </summary>
public sealed class UpdateCommand(Updater updater, IAnsiConsole console) : AsyncCommand<EmptyCommandSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken) =>
        RunAsync(cancellationToken);

    public Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the running executable's path.");

        return RunAsync(executablePath, cancellationToken);
    }

    /// <summary>Takes the target executable path explicitly, so tests never touch the real running binary.</summary>
    public async Task<int> RunAsync(string executablePath, CancellationToken cancellationToken)
    {
        var result = await UpdateProgressDisplay.RunAsync(console, progress =>
            updater.UpdateAsync(CurrentVersion.Value, executablePath, progress, cancellationToken));

        if (!result.Updated)
        {
            console.MarkupLine($"Already on the latest version ({Format(result.CurrentVersion).EscapeMarkup()}).");
            return 0;
        }

        console.MarkupLine($"Updated {Format(result.CurrentVersion).EscapeMarkup()} -> {Format(result.LatestVersion).EscapeMarkup()}");
        return 0;
    }

    private static string Format(string version) => "v" + version.TrimStart('v', 'V');
}
