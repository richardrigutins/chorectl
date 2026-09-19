using Chorectl.Cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Infrastructure;

public class AppConfigurationTests
{
    [Fact]
    public async Task RootHelp_ListsSubcommandsWithDescriptions()
    {
        var result = await RunAsync("--help");

        Assert.Contains("dependabot", result.Output);
        Assert.Contains("Dependabot pull request triage commands", result.Output);
        Assert.Contains("update", result.Output);
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
    public async Task UpdateHelp_ShowsDescriptionAndUsageExample()
    {
        var result = await RunAsync("update", "--help");

        Assert.Contains("Download and install the latest chorectl release in place", result.Output);
        Assert.Contains("chorectl update", result.Output);
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

    // A command whose construction itself fails - standing in for a real one, e.g. a malformed
    // config file blowing up while a command's constructor reads it - lets this assert on
    // AppConfiguration's own exception handling instead of on a specific failure mode. Goes
    // through the real TypeRegistrar/TypeResolver (backed by Microsoft.Extensions.DependencyInjection,
    // like CompositionRoot), not CommandAppTester's default activator: that's what unwraps the
    // constructor's exception onto CommandRuntimeException.InnerException directly, instead of
    // leaving it one level deeper inside a reflection TargetInvocationException.
    [Fact]
    public async Task CommandConstructorFailure_ShowsTheUnderlyingMessage_NotSpectresGenericWrapper()
    {
        var console = new TestConsole();
        var app = new CommandApp(new TypeRegistrar(new ServiceCollection()));
        app.Configure(config =>
        {
            AppConfiguration.Configure(config);
            config.AddCommand<ThrowingCommand>("throwing");
            config.ConfigureConsole(console);
        });

        await app.RunAsync(["throwing"]);

        Assert.Contains("boom", console.Output);
        Assert.DoesNotContain("Could not resolve type", console.Output);
    }

    private sealed class ThrowingCommand : Command<EmptyCommandSettings>
    {
        public ThrowingCommand() => throw new InvalidOperationException("boom");

        protected override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken) => 0;
    }

    private static async Task<CommandAppResult> RunAsync(params string[] args)
    {
        var tester = new CommandAppTester();
        tester.Configure(AppConfiguration.Configure);
        return await tester.RunAsync(args);
    }
}
