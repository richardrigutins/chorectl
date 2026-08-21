using Chorectl.Cli.Infrastructure;
using Spectre.Console.Cli.Testing;

namespace Chorectl.Cli.Tests.Infrastructure;

public class AppConfigurationTests
{
    [Fact]
    public async Task RootHelp_ListsSubcommandsWithDescriptions()
    {
        var result = await RunAsync("--help");

        Assert.Contains("dependabot", result.Output);
        Assert.Contains("Dependabot pull request triage commands", result.Output);
    }

    [Fact]
    public async Task DependabotHelp_ListsListAndMergeWithDescriptions()
    {
        var result = await RunAsync("dependabot", "--help");

        Assert.Contains("list", result.Output);
        Assert.Contains("List every open Dependabot PR across your repos", result.Output);
        Assert.Contains("merge", result.Output);
        Assert.Contains("Select and merge ready Dependabot PRs", result.Output);
    }

    [Fact]
    public async Task ListHelp_ShowsDescriptionAndUsageExample()
    {
        var result = await RunAsync("dependabot", "list", "--help");

        Assert.Contains("List every open Dependabot PR across your repos", result.Output);
        Assert.Contains("dependabot list", result.Output);
    }

    [Fact]
    public async Task MergeHelp_ShowsDescriptionAndUsageExample()
    {
        var result = await RunAsync("dependabot", "merge", "--help");

        Assert.Contains("Select and merge ready Dependabot PRs", result.Output);
        Assert.Contains("dependabot merge", result.Output);
    }

    [Fact]
    public async Task ConfigHelp_ListsGetAndSetWithDescriptions()
    {
        var result = await RunAsync("config", "--help");

        Assert.Contains("get", result.Output);
        Assert.Contains("Print the resolved config, with defaults applied", result.Output);
        Assert.Contains("set", result.Output);
        Assert.Contains("Set a single config value and persist it", result.Output);
    }

    [Fact]
    public async Task UnknownTopLevelCommand_FallsBackToHelpInsteadOfRawError()
    {
        var result = await RunAsync("frobnicate");

        Assert.Contains("USAGE", result.Output);
        Assert.Contains("dependabot", result.Output);
        Assert.DoesNotContain("Unknown command", result.Output);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task UnknownSubcommand_FallsBackToHelpInsteadOfRawError()
    {
        var result = await RunAsync("dependabot", "frobnicate");

        Assert.Contains("USAGE", result.Output);
        Assert.DoesNotContain("Unknown command", result.Output);
        Assert.NotEqual(0, result.ExitCode);
    }

    private static async Task<CommandAppResult> RunAsync(params string[] args)
    {
        var tester = new CommandAppTester();
        tester.Configure(AppConfiguration.Configure);
        return await tester.RunAsync(args);
    }
}
