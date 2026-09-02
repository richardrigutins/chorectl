using Chorectl.Core.Config;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands;

/// <summary>
/// <c>chorectl config get</c> - prints the resolved config (file values merged onto defaults).
/// </summary>
public sealed class ConfigGetCommand(ConfigLoader configLoader, IAnsiConsole console) : Command<EmptyCommandSettings>
{
    protected override int Execute(CommandContext context, EmptyCommandSettings settings, CancellationToken cancellationToken) => Run();

    public int Run()
    {
        var config = configLoader.Load();

        foreach (var (key, value) in Flatten(config))
        {
            console.MarkupLine($"{key}: {value.EscapeMarkup()}");
        }

        return 0;
    }

    private static IEnumerable<(string Key, string Value)> Flatten(ChorectlConfig config)
    {
        yield return ("exclude_repos", config.ExcludeRepos.Count == 0 ? "(none)" : string.Join(", ", config.ExcludeRepos));
        yield return ("include_forks", Format(config.IncludeForks));
        yield return ("merge_method", config.MergeMethod);
        yield return ("merge_poll_interval_seconds", config.MergePollIntervalSeconds.ToString());
        yield return ("merge_poll_timeout_seconds", config.MergePollTimeoutSeconds.ToString());
        yield return ("max_backoff_seconds", config.MaxBackoffSeconds.ToString());
        yield return ("skip_update_check", Format(config.SkipUpdateCheck));
        yield return ("default_select.patch", Format(config.DefaultSelect.Patch));
        yield return ("default_select.minor", Format(config.DefaultSelect.Minor));
        yield return ("default_select.major", Format(config.DefaultSelect.Major));
        yield return ("default_select.grouped", Format(config.DefaultSelect.Grouped));
    }

    private static string Format(bool value) => value ? "true" : "false";
}
