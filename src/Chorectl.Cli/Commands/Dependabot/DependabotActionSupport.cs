using Chorectl.Cli.Rendering;
using Chorectl.Cli.Rendering.Dependabot;
using Chorectl.Core.Audit;
using Chorectl.Core.Domain.Dependabot;
using Chorectl.Core.GitHub;
using Spectre.Console;

namespace Chorectl.Cli.Commands.Dependabot;

/// <summary>
/// Plumbing shared by every dependabot subcommand (list/merge/rebase/approve): discovering repos
/// and fetching candidate PRs, reporting an empty result set, and executing a selected batch
/// grouped by repo while auditing each outcome. The per-PR action itself, and how each result
/// renders, stays with each command - that's the part that actually differs between merge
/// (poll/retry), rebase, and approve (both fire-and-forget).
/// </summary>
internal static class DependabotActionSupport
{
    /// <summary>
    /// Discovers repos (scoped to <see cref="DependabotSettings.Repo"/> when set) and fetches
    /// their open Dependabot PRs, applying the <c>--security</c> filter shared by every
    /// subcommand. Also returns a repo name -&gt; owner lookup, since every action command needs
    /// it to call GitHub mutations (<see cref="ExecuteGroupedByRepoAsync{TResult}"/>'s
    /// <c>owners</c> parameter) even though <c>list</c> doesn't, and the names of any repos whose
    /// <c>vulnerabilityAlerts</c> page was truncated (so <c>IsSecurityUpdate</c> may be
    /// incomplete for them) - printed as a console warning here under non-<c>--json</c> output,
    /// but callers must fold it into their own <c>--json</c> payload themselves, since printing it
    /// there would break structured output.
    /// </summary>
    public static async Task<(IReadOnlyList<DependabotPr> Prs, IReadOnlyDictionary<string, string> Owners, IReadOnlyList<string> ReposWithTruncatedSecurityAlerts)> FetchCandidatesAsync(
        RestClient restClient,
        GraphQlClient graphQlClient,
        IAnsiConsole console,
        DependabotSettings settings,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<RepositoryInfo> repos = [];
        IReadOnlyList<DependabotPr> prs = [];
        IReadOnlyList<string> truncatedRepos = [];

        async Task DiscoverAndFetchAsync(StatusContext? status)
        {
            status?.Status("Discovering repos...");
            repos = await restClient.DiscoverReposAsync(settings.Repo);
            VerboseLog.Write(console, settings.Verbose, settings.Json, $"Discovered {repos.Count} repo(s)");

            status?.Status("Fetching Dependabot PRs...");
            (prs, truncatedRepos) = await graphQlClient.FetchDependabotPrsAsync(repos, cancellationToken);
            VerboseLog.Write(console, settings.Verbose, settings.Json, $"Fetched {prs.Count} Dependabot PR(s)");
        }

        // --json must stay clean of the spinner, same as every other piece of console output.
        if (settings.Json)
        {
            await DiscoverAndFetchAsync(null);
        }
        else
        {
            await console.Status().StartAsync("Discovering repos...", DiscoverAndFetchAsync);
        }

        if (!settings.Json && truncatedRepos.Count > 0)
        {
            // Rare (a repo would need >100 open Dependabot security alerts), but worth surfacing
            // instead of silently under-reporting IsSecurityUpdate for the overflow. --json gets
            // the same information via ReposWithTruncatedSecurityAlerts on the JSON payload instead.
            console.MarkupLine(
                $"[yellow]Warning:[/] {string.Join(", ", truncatedRepos.Select(r => r.EscapeMarkup()))} "
                + $"{(truncatedRepos.Count == 1 ? "has" : "have")} more than 100 open vulnerability alerts - "
                + "some security-update PRs there may not be flagged.");
        }

        if (settings.Security)
        {
            prs = prs.Where(pr => pr.IsSecurityUpdate).ToList();
        }

        return (prs, repos.ToDictionary(r => r.Name, r => r.Owner), truncatedRepos);
    }

    public static int ReportNothingToDo<TResult>(IAnsiConsole console, bool json, bool dryRun, string message, IReadOnlyList<string> truncatedRepos)
    {
        if (json)
        {
            JsonOutput.Write(console, new ActionJsonOutput<TResult>(dryRun, [], truncatedRepos));
        }
        else
        {
            console.MarkupLine(message);
        }

        return 0;
    }

    /// <param name="notAttempted">
    /// Builds the result for a PR that was never attempted because the batch already stopped
    /// (see below). Also used for the PR that was mid-attempt when the backoff budget ran out.
    /// </param>
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
        Func<DependabotPr, TResult> notAttempted,
        CancellationToken cancellationToken)
    {
        var results = new List<TResult>();

        // A rate limit is account-wide - once RateLimitBackoff gives up on one PR (AC-11.3), every
        // other PR would just hit the same wall, so the whole batch stops there instead of burning
        // a full backoff budget per remaining PR.
        var batchStopped = false;

        foreach (var group in selected.GroupBy(p => p.Repo))
        {
            var owner = owners[group.Key];
            if (!json)
            {
                ProgressDisplay.RenderRepoHeader(console, group.Key);
            }

            foreach (var candidate in group)
            {
                TResult result;
                if (batchStopped)
                {
                    result = notAttempted(candidate);
                }
                else
                {
                    try
                    {
                        result = await actOnOneAsync(owner, candidate, cancellationToken);
                    }
                    catch (RateLimitBackoffExhaustedException)
                    {
                        batchStopped = true;
                        result = notAttempted(candidate);
                    }
                }

                results.Add(result);

                // Dry run performs no actual mutation, so nothing is recorded (AC-08.1).
                if (!dryRun)
                {
                    await TryRecordAuditEntryAsync(console, auditLog, json, pr(result), describeOutcome(result), reason(result), cancellationToken);
                }

                if (!json)
                {
                    renderResult(console, result);
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Records one audit entry, degrading to a console warning instead of propagating if the
    /// write itself fails (e.g. a permission error or a full disk under the audit log path). By
    /// the time this runs, the GitHub mutation for this PR - and possibly others already in this
    /// batch - has already happened; letting an audit-log failure abort the rest of the batch
    /// would contradict the "one failing PR never aborts the whole batch" principle (§4.8), just
    /// for a side channel that isn't the PR action itself.
    /// </summary>
    private static async Task TryRecordAuditEntryAsync(
        IAnsiConsole console,
        IAuditLog auditLog,
        bool json,
        DependabotPr pr,
        string action,
        string? reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditLog.RecordAsync(new AuditEntry(DateTimeOffset.UtcNow, pr.Repo, pr.Number, action, reason), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!json)
            {
                console.MarkupLine($"[yellow]Warning:[/] failed to record audit log entry for {pr.Repo.EscapeMarkup()}#{pr.Number} - {ex.Message.EscapeMarkup()}");
            }
        }
    }
}
