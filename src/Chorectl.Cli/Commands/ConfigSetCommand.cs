using System.ComponentModel;
using Chorectl.Core.Config;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chorectl.Cli.Commands;

/// <summary>
/// <c>chorectl config set &lt;key&gt; &lt;value&gt;</c> - validates and persists a single config
/// value. An unknown key or an invalid value throws <see cref="ArgumentException"/>, surfaced by
/// <see cref="Infrastructure.AppConfiguration"/>'s generic error handler rather than a silent no-op.
/// </summary>
public sealed class ConfigSetCommand(ConfigLoader configLoader, IAnsiConsole console) : Command<ConfigSetCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("The config key to set, e.g. merge_method or default_select.major.")]
        public required string Key { get; init; }

        [CommandArgument(1, "<VALUE>")]
        [Description("The new value for the key.")]
        public required string Value { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken) =>
        Run(settings.Key, settings.Value);

    public int Run(string key, string value)
    {
        configLoader.SetValue(key, value);
        console.MarkupLine($"{key.EscapeMarkup()} set to {value.EscapeMarkup()}.");
        return 0;
    }
}
