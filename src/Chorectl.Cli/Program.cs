using System.Net.Http.Headers;
using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Spectre.Console;
using Spectre.Console.Cli;

IGitHubAuthenticator authenticator = new GhCliAuthenticator(new ProcessRunner());
string token;
try
{
    token = authenticator.GetToken();
}
catch (GitHubAuthException ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
    return 1;
}

var services = new ServiceCollection();

services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<IGitHubClient>(_ =>
    new GitHubClient(new Octokit.ProductHeaderValue("chorectl")) { Credentials = new Credentials(token) });
services.AddSingleton<IRepositorySource, OctokitRepositorySource>();
services.AddSingleton<IPullRequestMerger, OctokitPullRequestMerger>();
services.AddSingleton<RestClient>();
services.AddSingleton(_ =>
{
    var httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("chorectl", "1.0"));
    return new GraphQlClient(httpClient);
});

var app = new CommandApp(new TypeRegistrar(services));
app.Configure(AppConfiguration.Configure);

return app.Run(args);
