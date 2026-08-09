using Chorectl.Cli.Commands.Dependabot;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Infrastructure;

/// <summary>
/// The command tree, descriptions, examples, and error handling shared between the real
/// entry point (<c>Program.cs</c>) and CLI-wiring tests, so both exercise the exact same
/// Spectre.Console.Cli setup.
/// </summary>
public static class AppConfiguration
{
    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("chorectl");
        config.SetExceptionHandler((ex, _) => HandleException(ex, config.Settings.Console ?? AnsiConsole.Console));

        config.AddBranch("dependabot", dependabot =>
        {
            dependabot.SetDescription("Dependabot pull request triage commands.");

            dependabot.AddCommand<ListCommand>("list")
                .WithDescription("List every open Dependabot PR across your repos.")
                .WithExample("dependabot", "list");

            dependabot.AddCommand<MergeCommand>("merge")
                .WithDescription("Select and merge ready Dependabot PRs.")
                .WithExample("dependabot", "merge");
        });
    }

    // ponytail: unknown-command help falls back to root help, not the specific branch the user
    // was in (e.g. `dependabot foo` shows root help, not `dependabot --help`) — Spectre.Console.Cli
    // doesn't expose which branch a CommandParseException failed under. Revisit if that's confusing
    // in practice.
    private static int HandleException(Exception ex, IAnsiConsole console)
    {
        if (ex is CommandParseException && ex.Message.StartsWith("Unknown command", StringComparison.Ordinal))
        {
            var helpApp = new CommandApp();
            helpApp.Configure(c =>
            {
                Configure(c);
                c.ConfigureConsole(console);
            });
            helpApp.Run(["--help"]);
            return 1;
        }

        if (ex is CommandAppException { Pretty: not null } appException)
        {
            console.Write(appException.Pretty);
        }
        else
        {
            console.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
        }

        return 1;
    }
}
