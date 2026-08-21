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
    /// Prompts the user to select PRs to request a rebase for, grouped by repo. Every PR is
    /// pre-selected by default - there's no risk-tier distinction for rebase the way there is for
    /// merge - except one already being rebased by Dependabot, which is left unchecked since
    /// requesting another one would be redundant.
    /// </summary>
    public static IReadOnlyList<DependabotPr> PromptRebase(IAnsiConsole console, IReadOnlyList<DependabotPr> needsRebasePrs) =>
        Prompt(console, "Select PRs to rebase [grey](space to toggle, enter to confirm)[/]", needsRebasePrs, pr => !Classifier.HasRebaseBanner(pr));

    /// <summary>
    /// Prompts the user to select PRs to approve, grouped by repo. Every PR is pre-selected by
    /// default - there's no risk-tier distinction for approval the way there is for merge.
    /// </summary>
    public static IReadOnlyList<DependabotPr> PromptApprove(IAnsiConsole console, IReadOnlyList<DependabotPr> needsApprovalPrs) =>
        Prompt(console, "Select PRs to approve [grey](space to toggle, enter to confirm)[/]", needsApprovalPrs, _ => true);

    private static IReadOnlyList<DependabotPr> Prompt(
        IAnsiConsole console,
        string title,
        IReadOnlyList<DependabotPr> prs,
        Func<DependabotPr, bool> defaultSelected)
    {
        var byLabel = new Dictionary<string, DependabotPr>();
        var prompt = new MultiSelectionPrompt<string>()
            .Title(title)
            .PageSize(15)
            .NotRequired();

        foreach (var repoGroup in prs.GroupBy(pr => pr.Repo).OrderBy(g => g.Key))
        {
            var prsInRepo = repoGroup.ToList();
            var repoLabel = repoGroup.Key.EscapeMarkup();
            var labels = prsInRepo.Select(pr =>
            {
                var label = Describe(pr);
                byLabel[label] = pr;
                return label;
            }).ToList();

            prompt.AddChoiceGroup(repoLabel, labels);

            foreach (var pr in prsInRepo.Where(defaultSelected))
            {
                prompt.Select(Describe(pr));
            }

            if (prsInRepo.TrueForAll(pr => defaultSelected(pr)))
            {
                prompt.Select(repoLabel);
            }
        }

        var selectedLabels = console.Prompt(prompt);

        return selectedLabels
            .Where(byLabel.ContainsKey)
            .Select(label => byLabel[label])
            .ToList();
    }

    private static string Describe(DependabotPr pr)
    {
        var change = pr.DependencyName is not null && pr.FromVersion is not null && pr.ToVersion is not null
            ? $"{pr.DependencyName}  {pr.FromVersion} -> {pr.ToVersion}"
            : pr.Title;

        return $"#{pr.Number}  {change.EscapeMarkup()}  ({pr.SemverLevel.ToString().ToLowerInvariant()})";
    }
}
