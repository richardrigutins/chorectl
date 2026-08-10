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
    public static IReadOnlyList<DependabotPr> PromptMerge(IAnsiConsole console, IReadOnlyList<DependabotPr> readyPrs)
    {
        var byLabel = new Dictionary<string, DependabotPr>();
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select PRs to merge [grey](space to toggle, enter to confirm)[/]")
            .PageSize(15)
            .NotRequired();

        foreach (var repoGroup in readyPrs.GroupBy(pr => pr.Repo).OrderBy(g => g.Key))
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

            foreach (var pr in prsInRepo.Where(Classifier.DefaultSelected))
            {
                prompt.Select(Describe(pr));
            }

            if (prsInRepo.TrueForAll(Classifier.DefaultSelected))
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
