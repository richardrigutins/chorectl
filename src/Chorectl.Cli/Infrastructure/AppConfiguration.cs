using Chorectl.Cli.Commands;
using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Core.GitHub;
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

            dependabot.AddCommand<RebaseCommand>("rebase")
                .WithDescription("Select PRs needing a rebase and request one from Dependabot.")
                .WithExample("dependabot", "rebase");

            dependabot.AddCommand<ApproveCommand>("approve")
                .WithDescription("Select PRs needing approval and approve them.")
                .WithExample("dependabot", "approve");
        });

        config.AddBranch("config", configBranch =>
        {
            configBranch.SetDescription("View and edit chorectl preferences.");

            configBranch.AddCommand<ConfigGetCommand>("get")
                .WithDescription("Print the resolved config, with defaults applied.")
                .WithExample("config", "get");

            configBranch.AddCommand<ConfigSetCommand>("set")
                .WithDescription("Set a single config value and persist it.")
                .WithExample("config", "set", "merge_method", "rebase");
        });
    }

    // Unknown-command help falls back to root help, not the specific branch the user
    // was in (e.g. `dependabot foo` shows root help, not `dependabot --help`) - Spectre.Console.Cli
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

        // Spectre.Console.Cli wraps any exception thrown while constructing a command's
        // dependencies (e.g. a lazily-fetched GitHub token failing) in a CommandRuntimeException
        // with a generic "Could not resolve type" message, stashing the real exception as
        // InnerException. Unwrap it so auth failures still show their actionable message.
        if (ex is CommandRuntimeException { InnerException: GitHubAuthException authException })
        {
            console.MarkupLine($"[red]Error:[/] {authException.Message.EscapeMarkup()}");
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
