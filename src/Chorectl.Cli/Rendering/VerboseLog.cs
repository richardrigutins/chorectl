using Spectre.Console;

namespace Chorectl.Cli.Rendering;

/// <summary>
/// Prints diagnostic lines for <c>--verbose</c>, suppressed entirely when <paramref name="verbose"/>
/// is <see langword="false"/> or when <c>--json</c> is active (structured output must stay clean).
/// </summary>
public static class VerboseLog
{
    public static void Write(IAnsiConsole console, bool verbose, bool json, string message)
    {
        if (verbose && !json)
        {
            console.MarkupLine($"[grey]» {message.EscapeMarkup()}[/]");
        }
    }
}
