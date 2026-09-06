using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Chorectl.Core.Process;
using Spectre.Console;

IGitHubAuthenticator authenticator = new GhCliAuthenticator(new ProcessRunner());

return await CompositionRoot.RunAsync(authenticator, args, AnsiConsole.Console);
