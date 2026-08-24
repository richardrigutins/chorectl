using Chorectl.Core.Domain.Dependabot;
using Spectre.Console;

namespace Chorectl.Cli.Rendering.Dependabot;

/// <summary>
/// Checkbox-list selection screens for the dependabot commands.
/// </summary>
public static class SelectionScreens
{
    /// <summary>
    /// Prompts the user to select PRs to merge, grouped by repo, with patch/minor bumps
    /// pre-selected and major/grouped bumps left unchecked.
    /// </summary>
    public static IReadOnlyList<DependabotPr> PromptMerge(IAnsiConsole console, IReadOnlyList<DependabotPr> readyPrs) =>
        Prompt(console, "Select PRs to merge [grey](space to toggle, enter to confirm)[/]", readyPrs, Classifier.DefaultSelected);

    /// <summary>
    /// Prompts the user to select PRs to request a rebase for, grouped by repo. PRs that need a
    /// rebase are pre-selected by default - except one already being rebased by Dependabot, which
    /// is left unchecked since requesting another one would be redundant. With <c>--all</c>, the
    /// candidate list widens to every open PR, but only ones that actually need a rebase are
    /// pre-selected - the rest require explicit opt-in.
    /// </summary>
    public static IReadOnlyList<DependabotPr> PromptRebase(IAnsiConsole console, IReadOnlyList<DependabotPr> candidatePrs) =>
        Prompt(console, "Select PRs to rebase [grey](space to toggle, enter to confirm)[/]", candidatePrs,
            pr => Classifier.NeedsRebase(pr) && !Classifier.HasRebaseBanner(pr));

    /// <summary>
    /// Prompts the user to select PRs to approve, grouped by repo. Every PR is pre-selected by
    /// default - there's no risk-tier distinction for approval the way there is for merge.
    /// </summary>
    public static IReadOnlyList<DependabotPr> PromptApprove(IAnsiConsole console, IReadOnlyList<DependabotPr> needsApprovalPrs) =>
        Prompt(console, "Select PRs to approve [grey](space to toggle, enter to confirm)[/]", needsApprovalPrs, _ => true);

    /// <summary>
    /// A single row in the selection prompt: either a real PR or a repo group header. Using this
    /// (rather than the PR's rendered display text) as the prompt's underlying choice identity
    /// means two PRs that render identically - e.g. the same dependency bumped the same way in two
    /// different repos, a common occurrence when triaging across many repos at once - stay
    /// distinguishable. <see cref="DependabotPr"/> is a record, so equality already includes
    /// <see cref="DependabotPr.Repo"/> and <see cref="DependabotPr.Number"/>.
    /// </summary>
    private readonly record struct Choice(DependabotPr? Pr, string? Header)
    {
        public static Choice ForPr(DependabotPr pr) => new(pr, null);

        public static Choice ForHeader(string header) => new(null, header);
    }

    private static IReadOnlyList<DependabotPr> Prompt(
        IAnsiConsole console,
        string title,
        IReadOnlyList<DependabotPr> prs,
        Func<DependabotPr, bool> defaultSelected)
    {
        var prompt = new MultiSelectionPrompt<Choice>()
            .Title(title)
            .PageSize(15)
            .NotRequired()
            .UseConverter(choice => choice.Pr is { } pr ? Describe(pr) : choice.Header!.EscapeMarkup());

        foreach (var repoGroup in prs.GroupBy(pr => pr.Repo).OrderBy(g => g.Key))
        {
            var prsInRepo = repoGroup.ToList();
            var header = Choice.ForHeader(repoGroup.Key);
            var choices = prsInRepo.Select(Choice.ForPr).ToList();

            prompt.AddChoiceGroup(header, choices);

            foreach (var choice in choices.Where(c => defaultSelected(c.Pr!)))
            {
                prompt.Select(choice);
            }

            if (prsInRepo.TrueForAll(pr => defaultSelected(pr)))
            {
                prompt.Select(header);
            }
        }

        var selected = console.Prompt(prompt);

        return selected.Where(c => c.Pr is not null).Select(c => c.Pr!).ToList();
    }

    private static string Describe(DependabotPr pr)
    {
        var change = pr.DependencyName is not null && pr.FromVersion is not null && pr.ToVersion is not null
            ? $"{pr.DependencyName}  {pr.FromVersion} -> {pr.ToVersion}"
            : pr.Title;

        return $"#{pr.Number}  {change.EscapeMarkup()}  ({BumpStyle.Markup(pr.SemverLevel)}){Badges.Markup(pr)}";
    }
}
