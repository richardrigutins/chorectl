# chorectl

A standalone .NET CLI tool for the recurring maintenance chores of owning multiple GitHub repos. The first feature area is Dependabot PR triage: reviewing, rebasing, approving, and merging Dependabot PRs across all of your repos from a single keyboard-driven workflow instead of the GitHub web UI, one PR at a time.

## Status

Work in progress, pre-release. No published builds yet - see the [implementation plan](.planning) for current progress.

## Prerequisites

- [.NET 10 SDK or runtime](https://dotnet.microsoft.com/download) (self-contained builds will remove this requirement once packaging lands)
- [GitHub CLI (`gh`)](https://cli.github.com/), installed and authenticated (`gh auth login`) - `chorectl` uses your existing `gh` credentials and does not have a separate login flow

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
