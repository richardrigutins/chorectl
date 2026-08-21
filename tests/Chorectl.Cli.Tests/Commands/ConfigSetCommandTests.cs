using Chorectl.Cli.Commands;
using Chorectl.Core.Config;
using Spectre.Console.Testing;

namespace Chorectl.Cli.Tests.Commands;

public class ConfigSetCommandTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("chorectl-tests-").FullName;

    private string ConfigPath => Path.Combine(_tempDir, "config.yml");

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Run_WithValidKeyAndValue_PersistsAndConfirms()
    {
        var loader = new ConfigLoader(ConfigPath);
        var console = new TestConsole();
        var command = new ConfigSetCommand(loader, console);

        var exitCode = command.Run("merge_method", "rebase");

        Assert.Equal(0, exitCode);
        Assert.Contains("merge_method set to rebase.", console.Output);
        Assert.Equal("rebase", loader.Load().MergeMethod);
    }

    [Fact]
    public void Run_WithUnknownKey_ThrowsArgumentException()
    {
        var loader = new ConfigLoader(ConfigPath);
        var console = new TestConsole();
        var command = new ConfigSetCommand(loader, console);

        Assert.Throws<ArgumentException>(() => command.Run("not_a_real_key", "value"));
    }

    [Fact]
    public void Run_WithInvalidValue_ThrowsArgumentException_AndDoesNotPersist()
    {
        var loader = new ConfigLoader(ConfigPath);
        var console = new TestConsole();
        var command = new ConfigSetCommand(loader, console);

        Assert.Throws<ArgumentException>(() => command.Run("merge_method", "bogus"));
        Assert.Equal("squash", loader.Load().MergeMethod);
    }
}
