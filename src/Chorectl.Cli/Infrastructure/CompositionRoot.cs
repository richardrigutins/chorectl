using System.Net.Http.Headers;
using Chorectl.Core.Audit;
using Chorectl.Core.Config;
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
        var configLoader = new ConfigLoader(ConfigLoader.DefaultPath);

        // Lazy, and shared across every registration that needs it, so a malformed config file
        // only surfaces (via Spectre's exception handler, wired up below) when a command that
        // actually needs config resolves its dependencies - never for --help or a bad-config-fixing
        // `config set`, and never before app.Run() has a chance to catch it.
        var lazyConfig = new Lazy<ChorectlConfig>(configLoader.Load);

        var services = new ServiceCollection();

        services.AddSingleton(console);
        services.AddSingleton(configLoader);
        services.AddSingleton(_ => lazyConfig.Value);
        services.AddSingleton<IGitHubClient>(_ =>
            new GitHubClient(new Octokit.ProductHeaderValue("chorectl"), new GitHubCredentialStore(cachingAuthenticator)));
        services.AddSingleton<IRepositorySource, OctokitRepositorySource>();
        services.AddSingleton<IPullRequestMerger>(provider =>
            new OctokitPullRequestMerger(provider.GetRequiredService<IGitHubClient>(), lazyConfig.Value.MergeMethod));
        services.AddSingleton<IPullRequestCommenter, OctokitPullRequestCommenter>();
        services.AddSingleton<IPullRequestApprover, OctokitPullRequestApprover>();
        services.AddSingleton<IAuditLog>(new AuditLog(AuditLog.DefaultPath));
        services.AddSingleton(provider => new RestClient(
            provider.GetRequiredService<IRepositorySource>(),
            new HashSet<string>(lazyConfig.Value.ExcludeRepos),
            lazyConfig.Value.IncludeForks));
        services.AddSingleton(_ =>
        {
            var httpClient = new HttpClient(new GitHubAuthenticationHandler(cachingAuthenticator) { InnerHandler = new HttpClientHandler() })
            {
                BaseAddress = new Uri("https://api.github.com/"),
            };
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("chorectl", "1.0"));
            return new GraphQlClient(httpClient, lazyConfig.Value.MaxBackoffSeconds);
        });

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(cli =>
        {
            AppConfiguration.Configure(cli);
            cli.ConfigureConsole(console);
        });

        return app.Run(args);
    }
}
