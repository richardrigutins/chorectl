using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Chorectl.Core.GitHub;

/// <summary>
/// Retrieves the user's GitHub credential via the gh CLI.
/// </summary>
public sealed partial class GhCliAuthenticator(IProcessRunner processRunner) : IGitHubAuthenticator
{
    // gh auth token was introduced in gh 2.5.0.
    private static readonly Version MinimumGhVersion = new(2, 5, 0);

    /// <summary>
    /// Retrieves the current GitHub token from <c>gh auth token</c>.
    /// </summary>
    /// <exception cref="GitHubAuthException">gh is missing, too old, unauthenticated, or the token couldn't be read.</exception>
    public string GetToken()
    {
        CheckPrerequisites();

        var result = RunGh("auth token");
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new GitHubAuthException(
                "Could not retrieve a token from `gh auth token`. Run `gh auth login` and try again.");
        }

        return result.StandardOutput;
    }

    private void CheckPrerequisites()
    {
        CheckMinimumVersion(RunGhVersion());
        CheckAuthenticated();
    }

    private ProcessResult RunGhVersion()
    {
        try
        {
            return RunGh("--version");
        }
        catch (Win32Exception)
        {
            throw new GitHubAuthException(
                "GitHub CLI (gh) was not found on PATH. Install it from https://cli.github.com and try again.");
        }
    }

    private static void CheckMinimumVersion(ProcessResult versionResult)
    {
        var match = GhVersionPattern().Match(versionResult.StandardOutput);
        if (!match.Success || !Version.TryParse(match.Groups[1].Value, out var version))
        {
            throw new GitHubAuthException(
                $"Could not determine the installed gh version from output: '{versionResult.StandardOutput}'.");
        }

        if (version < MinimumGhVersion)
        {
            throw new GitHubAuthException(
                $"chorectl requires gh version {MinimumGhVersion} or later, found {version}. Update it from https://cli.github.com.");
        }
    }

    private void CheckAuthenticated()
    {
        var result = RunGh("auth status");
        if (result.ExitCode != 0)
        {
            throw new GitHubAuthException(
                "GitHub CLI is not authenticated. Run `gh auth login` and try again.");
        }
    }

    private ProcessResult RunGh(string arguments) => processRunner.Run("gh", arguments);

    [GeneratedRegex(@"gh version (\d+\.\d+\.\d+)")]
    private static partial Regex GhVersionPattern();
}
