using System.Net.Http.Headers;
using Chorectl.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Infrastructure;

/// <summary>
/// Wires up the DI container and runs the CLI. The GitHub token is fetched lazily - via
/// <see cref="GitHubCredentialStore"/> for Octokit and <see cref="GitHubAuthenticationHandler"/>
/// for the GraphQL client - so it's only requested when a command actually sends its first
/// GitHub request. Commands that don't touch GitHub - namely <c>--help</c> - never trigger it,
/// and <see cref="CachingGitHubAuthenticator"/> ensures both clients share a single underlying
/// authentication per process.
/// </summary>
public static class CompositionRoot
{
    public static int Run(IGitHubAuthenticator authenticator, string[] args, IAnsiConsole console)
    {
        var cachingAuthenticator = new CachingGitHubAuthenticator(authenticator);

        var services = new ServiceCollection();

        services.AddSingleton(console);
        services.AddSingleton<IGitHubClient>(_ =>
            new GitHubClient(new Octokit.ProductHeaderValue("chorectl"), new GitHubCredentialStore(cachingAuthenticator)));
        services.AddSingleton<IRepositorySource, OctokitRepositorySource>();
        services.AddSingleton<IPullRequestMerger, OctokitPullRequestMerger>();
        services.AddSingleton<RestClient>();
        services.AddSingleton(_ =>
        {
            var httpClient = new HttpClient(new GitHubAuthenticationHandler(cachingAuthenticator) { InnerHandler = new HttpClientHandler() })
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };
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
