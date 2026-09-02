using Chorectl.Cli.Commands;
using Chorectl.Core.Config;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands;

public class ConfigGetCommandTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    private string ConfigPath => Path.Combine(_tempDir, "config.yml");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Run_WhenNoConfigFileExists_PrintsDefaults()
    {
        var (command, console) = CreateCommand();

        var exitCode = command.Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("exclude_repos: (none)", console.Output);
        Assert.Contains("include_forks: false", console.Output);
        Assert.Contains("merge_method: squash", console.Output);
        Assert.Contains("merge_poll_interval_seconds: 15", console.Output);
        Assert.Contains("merge_poll_timeout_seconds: 120", console.Output);
        Assert.Contains("max_backoff_seconds: 300", console.Output);
        Assert.Contains("skip_update_check: false", console.Output);
        Assert.Contains("default_select.patch: true", console.Output);
        Assert.Contains("default_select.minor: true", console.Output);
        Assert.Contains("default_select.major: false", console.Output);
        Assert.Contains("default_select.grouped: false", console.Output);
    }

    [Fact]
    public void Run_WithCustomizedConfig_PrintsResolvedValues()
    {
        var (command, console) = CreateCommand();
        new ConfigLoader(ConfigPath).Save(new ChorectlConfig { ExcludeRepos = ["foo", "bar"], MergeMethod = "rebase" });

        command.Run();

        Assert.Contains("exclude_repos: foo, bar", console.Output);
        Assert.Contains("merge_method: rebase", console.Output);
    }

    private (ConfigGetCommand Command, TestConsole Console) CreateCommand()
    {
        var console = new TestConsole();
        var command = new ConfigGetCommand(new ConfigLoader(ConfigPath), console);
        return (command, console);
    }
}
