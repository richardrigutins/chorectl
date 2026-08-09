using System.Net.Http.Headers;
using Chorectl.Cli.Commands.Dependabot;
using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Spectre.Console;
using Spectre.Console.Cli;

var services = new ServiceCollection();

services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<GitHubAuth>();
services.AddSingleton<IGitHubClient>(provider =>
{
    var token = provider.GetRequiredService<GitHubAuth>().GetToken();
    return new GitHubClient(new Octokit.ProductHeaderValue("chorectl")) { Credentials = new Credentials(token) };
});
services.AddSingleton<IRepositorySource, OctokitRepositorySource>();
services.AddSingleton<IPullRequestMerger, OctokitPullRequestMerger>();
services.AddSingleton<RestClient>();
services.AddSingleton(provider =>
{
    var token = provider.GetRequiredService<GitHubAuth>().GetToken();
    var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("chorectl", "1.0"));
    return new GraphQlClient(httpClient);
});

var app = new CommandApp(new TypeRegistrar(services));
app.Configure(config =>
{
    config.AddBranch("dependabot", dependabot =>
    {
        dependabot.AddCommand<ListCommand>("list");
        dependabot.AddCommand<MergeCommand>("merge");
    });
});

return app.Run(args);
