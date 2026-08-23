using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Audit;
using Chorectl.Core.Domain.Dependabot;
using Spectre.Console;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// Plumbing shared by the dependabot action commands (merge/rebase/approve): reporting an empty
/// result set, and executing a selected batch grouped by repo while auditing each outcome. The
/// per-PR action itself, and how each result renders, stays with each command - that's the part
/// that actually differs between merge (poll/retry), rebase, and approve (both fire-and-forget).
/// </summary>
internal static class DependabotActionSupport
{
    public static int ReportNothingToDo<TResult>(IAnsiConsole console, bool json, bool dryRun, string message)
    {
        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<TResult>(dryRun, []));
        }
        else
        {
            console.MarkupLine(message);
        }

        return 0;
    }

    public static async Task<List<TResult>> ExecuteGroupedByRepoAsync<TResult>(
        IAnsiConsole console,
        IAuditLog auditLog,
        IReadOnlyDictionary<string, string> owners,
        IReadOnlyList<DependabotPr> selected,
        bool dryRun,
        bool json,
        Func<string, DependabotPr, CancellationToken, Task<TResult>> actOnOneAsync,
        Func<TResult, DependabotPr> pr,
        Func<TResult, string> describeOutcome,
        Func<TResult, string?> reason,
        Action<IAnsiConsole, TResult> renderResult,
        CancellationToken cancellationToken)
    {
        var results = new List<TResult>();

        foreach (var group in selected.GroupBy(p => p.Repo))
        {
            var owner = owners[group.Key];
            if (!json)
            {
                ProgressDisplay.RenderRepoHeader(console, group.Key);
            }

            foreach (var candidate in group)
            {
                var result = await actOnOneAsync(owner, candidate, cancellationToken);
                results.Add(result);

                // Dry run performs no actual mutation, so nothing is recorded (AC-08.1).
                if (!dryRun)
                {
                    var resultPr = pr(result);
                    await auditLog.RecordAsync(
                        new AuditEntry(DateTimeOffset.UtcNow, resultPr.Repo, resultPr.Number, describeOutcome(result), reason(result)),
                        cancellationToken);
                }

                if (!json)
                {
                    renderResult(console, result);
                }
            }
        }

        return results;
    }
}
