using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Spectre.Console;

IGitHubAuthenticator authenticator = new GhCliAuthenticator(new ProcessRunner());

return CompositionRoot.Run(authenticator, args, AnsiConsole.Console);
