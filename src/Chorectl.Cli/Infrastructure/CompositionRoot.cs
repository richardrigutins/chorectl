using System.Net.Http.Headers;
using Chorectl.Core.Audit;
using Chorectl.Core.Config;
using Chorectl.Core.GitHub;
using Chorectl.Core.Process;
using Chorectl.Core.Update;
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
    private static readonly Octokit.ProductHeaderValue ProductHeader = new("chorectl");

    /// <param name="authenticator">
    /// <see langword="null"/> to use the real <see cref="GhCliAuthenticator"/>, targeting
    /// <see cref="ChorectlConfig.GitHubHost"/> - only tests substitute a fake here.
    /// </param>
    /// <param name="updateCheckCachePath">
    /// Overridable for tests - <see cref="UpdateChecker"/> writes here on every due check (even a
    /// failed one, see its own doc comment), so tests must never let this default to
    /// <see cref="UpdateChecker.DefaultCachePath"/> and pollute the real one.
    /// </param>
    /// <param name="releaseAuthenticator">
    /// Authenticates chorectl's own release check (see <see cref="BuildReleaseSource"/>) -
    /// deliberately separate from <paramref name="authenticator"/>, since that check always
    /// targets github.com regardless of <see cref="ChorectlConfig.GitHubHost"/>. <see
    /// langword="null"/> to use the real <see cref="GhCliAuthenticator"/> pinned to github.com;
    /// only tests substitute a fake here.
    /// </param>
    public static async Task<int> RunAsync(
        IGitHubAuthenticator? authenticator,
        string[] args,
        IAnsiConsole console,
        string? updateCheckCachePath = null,
        IGitHubAuthenticator? releaseAuthenticator = null)
    {
        var configLoader = new ConfigLoader(ConfigLoader.DefaultPath);

        // Lazy, and shared across every registration that needs it, so a malformed config file
        // only surfaces (via Spectre's exception handler, wired up below) when a command that
        // actually needs config resolves its dependencies - never for --help or a bad-config-fixing
        // `config set`, and never before app.Run() has a chance to catch it.
        var lazyConfig = new Lazy<ChorectlConfig>(configLoader.Load);

        // The closure here isn't invoked until GetToken() actually runs, so this stays just as
        // lazy about config as everything else in this method.
        var cachingAuthenticator = new CachingGitHubAuthenticator(
            authenticator ?? new GhCliAuthenticator(new ProcessRunner(), () => lazyConfig.Value.GitHubHost));

        var services = new ServiceCollection();

        services.AddSingleton(console);
        services.AddSingleton(configLoader);
        services.AddSingleton(_ => lazyConfig.Value);

        services.AddSingleton<IGitHubClient>(_ => BuildGitHubClient(new GitHubCredentialStore(cachingAuthenticator), lazyConfig.Value.GitHubHost));
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
                BaseAddress = GitHubHost.GraphQlBaseUri(lazyConfig.Value.GitHubHost),
            };
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("chorectl", "1.0"));
            return new GraphQlClient(httpClient, lazyConfig.Value.MaxBackoffSeconds);
        });

        // chorectl's own release check always targets github.com, independent of the configured
        // target host (ChorectlConfig.GitHubHost) - a GitHub Enterprise Server instance has no
        // relationship to chorectl's own releases, and a GHE-scoped token wouldn't authenticate
        // against github.com anyway. Shared between the startup update check below and a
        // `chorectl update` command's own Updater.
        // Always authenticates separately from the target client, even when the target
        // host already is github.com (one extra `gh auth token` call) - simplest correct option,
        // revisit only if that overhead is measured to matter.
        var releaseSource = BuildReleaseSource(releaseAuthenticator);
        services.AddSingleton<IReleaseSource>(releaseSource);
        services.AddSingleton(_ => new Updater(releaseSource, new HttpClient()));

        var app = new CommandApp(new TypeRegistrar(services));
        app.Configure(cli =>
        {
            AppConfiguration.Configure(cli);
            cli.ConfigureConsole(console);
        });

        // Best-effort, and cheap even when there's nothing to clean up - runs for every invocation,
        // including --help, unlike the update check below.
        Updater.CleanupPreviousVersion(Environment.ProcessPath ?? string.Empty);

        if (ShouldCheckForUpdate(args))
        {
            var updateChecker = new UpdateChecker(releaseSource, updateCheckCachePath ?? UpdateChecker.DefaultCachePath);
            await TryNotifyOfUpdateAsync(updateChecker, lazyConfig, console);
        }

        return await app.RunAsync(args);
    }

    private static GitHubClient BuildGitHubClient(ICredentialStore credentialStore, string? host)
    {
        var baseUri = GitHubHost.RestApiBaseUri(host);
        return baseUri is null
            ? new GitHubClient(ProductHeader, credentialStore)
            : new GitHubClient(ProductHeader, credentialStore, baseUri);
    }

    private static OctokitReleaseSource BuildReleaseSource(IGitHubAuthenticator? releaseAuthenticator)
    {
        var authenticator = new CachingGitHubAuthenticator(
            releaseAuthenticator ?? new GhCliAuthenticator(new ProcessRunner(), () => "github.com"));
        return new OctokitReleaseSource(new GitHubClient(ProductHeader, new GitHubCredentialStore(authenticator)));
    }

    // A cheap, args-only gate that runs before lazyConfig or the authenticator are ever touched,
    // so `--help` and a bare invocation keep never triggering the authenticator (this class's
    // existing contract, see the doc comment above) even though the update check itself needs a
    // real GitHub call. `--json` is suppressed here too, so structured output stays clean, and
    // `update` skips its own redundant check. `-v`/`--version` as the first argument is Spectre's
    // own version-flag trigger (see AppConfiguration.Configure's SetApplicationVersion call) and
    // must print nothing else.
    internal static bool ShouldCheckForUpdate(string[] args) =>
        args.Length > 0
        && !string.Equals(args[0], "update", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(args[0], "-v", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(args[0], "--version", StringComparison.OrdinalIgnoreCase)
        && !args.Contains("--help")
        && !args.Contains("-h")
        && !args.Contains("--json")
        && Environment.GetEnvironmentVariable("CHORECTL_NO_UPDATE_CHECK") is null;

    /// <summary>
    /// Prints a one-line notice if a newer version is available. Never lets a malformed config
    /// file - or any other failure here - propagate up to <c>app.RunAsync</c>: a bad config
    /// surfaces properly once a real command resolves it for itself; this check fails silently by
    /// design, bounded by a short timeout so a slow GitHub never holds up the actual command.
    /// </summary>
    private static async Task TryNotifyOfUpdateAsync(UpdateChecker updateChecker, Lazy<ChorectlConfig> lazyConfig, IAnsiConsole console)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var result = await updateChecker.CheckAsync(CurrentVersion.Value, lazyConfig.Value.SkipUpdateCheck, cts.Token);

            if (result is not null)
            {
                console.MarkupLine(
                    $"[yellow]A new version of chorectl is available: {result.LatestVersion.EscapeMarkup()} - "
                    + "run `chorectl update` to upgrade.[/]");
            }
        }
        catch
        {
            // Fails silently by design, see the doc comment above.
        }
    }
}
