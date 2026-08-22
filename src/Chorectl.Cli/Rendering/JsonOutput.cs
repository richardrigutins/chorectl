using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

namespace Chorectl.Cli.Rendering;

/// <summary>
/// Serializes structured output for <c>--json</c> mode, shared across dependabot commands.
/// Written via <see cref="IAnsiConsole.WriteLine(string, Style?)"/> rather than markup, since
/// JSON's square brackets would otherwise be parsed as Spectre markup tags.
/// </summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Write(IAnsiConsole console, object value) =>
        console.WriteLine(JsonSerializer.Serialize(value, Options));
}

/// <summary>Structured <c>--json</c> output for an action command (merge/rebase/approve).</summary>
public sealed record ActionJsonOutput<TResult>(bool DryRun, IReadOnlyList<TResult> Results);
