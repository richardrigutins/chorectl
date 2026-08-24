using System.Text.Json;
using System.Text.Json.Serialization;
using Chorectl.Core.Domain.Dependabot;
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

/// <summary>
/// Structured <c>--json</c> output for an action command (merge/rebase/approve).
/// <paramref name="ReposWithTruncatedSecurityAlerts"/> mirrors the human-readable warning printed
/// under non-<c>--json</c> output (see <see cref="Chorectl.Cli.Commands.Dependabot.DependabotActionSupport.FetchCandidatesAsync"/>) -
/// <c>--json</c> suppresses that console warning to keep output structured, so this is the only
/// signal automation gets that <c>IsSecurityUpdate</c> may be incomplete for the listed repos.
/// </summary>
public sealed record ActionJsonOutput<TResult>(bool DryRun, IReadOnlyList<TResult> Results, IReadOnlyList<string> ReposWithTruncatedSecurityAlerts);

/// <summary>
/// Structured <c>--json</c> output for <c>chorectl dependabot list</c>.
/// <paramref name="ReposWithTruncatedSecurityAlerts"/> mirrors <see cref="ActionJsonOutput{TResult}"/>'s
/// field of the same name.
/// </summary>
public sealed record ListJsonOutput(IReadOnlyList<DependabotPr> Prs, IReadOnlyList<string> ReposWithTruncatedSecurityAlerts);
