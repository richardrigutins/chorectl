using Chorectl.Cli.Infrastructure;
using Chorectl.Core.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Octokit;
using Spectre.Console.Cli;

var services = new ServiceCollection();

services.AddSingleton<IProcessRunner, ProcessRunner>();
services.AddSingleton<GitHubAuth>();
services.AddSingleton<IGitHubClient>(provider =>
{
    var token = provider.GetRequiredService<GitHubAuth>().GetToken();
    return new GitHubClient(new ProductHeaderValue("chorectl")) { Credentials = new Credentials(token) };
});
services.AddSingleton<IRepositorySource, OctokitRepositorySource>();
services.AddSingleton<RestClient>();

var app = new CommandApp(new TypeRegistrar(services));
return app.Run(args);
