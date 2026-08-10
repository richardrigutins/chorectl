using System.Net.Http.Headers;
using Chorectl.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Infrastructure;

/// <summary>
/// Wires up the DI container and runs the CLI. The GitHub token is fetched lazily, on first use
/// by a command's dependencies, so commands that don't touch GitHub - namely <c>--help</c> - don't
/// require <c>gh</c> to be installed or authenticated.
/// </summary>
public static class CompositionRoot
{
    public static int Run(IGitHubAuthenticator authenticator, string[] args, IAnsiConsole console)
    {
        var token = new Lazy<string>(authenticator.GetToken);

        var services = new ServiceCollection();

        services.AddSingleton(console);
        services.AddSingleton<IGitHubClient>(_ =>
            new GitHubClient(new Octokit.ProductHeaderValue("chorectl")) { Credentials = new Credentials(token.Value) });
        services.AddSingleton<IRepositorySource, OctokitRepositorySource>();
        services.AddSingleton<IPullRequestMerger, OctokitPullRequestMerger>();
        services.AddSingleton<RestClient>();
        services.AddSingleton(_ =>
        {
            var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("chorectl", "1.0"));
            return new GraphQlClient(httpClient);
        });

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(config =>
        {
            AppConfiguration.Configure(config);
            config.ConfigureConsole(console);
        });

        return app.Run(args);
    }
}
