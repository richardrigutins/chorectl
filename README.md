# chorectl

A standalone .NET CLI tool for the recurring maintenance chores of owning multiple GitHub repos. The first feature area is Dependabot PR triage: reviewing, rebasing, approving, and merging Dependabot PRs across all of your repos from a single keyboard-driven workflow instead of the GitHub web UI, one PR at a time.

## Status

Work in progress, pre-release.

## Prerequisites

- [GitHub CLI (`gh`)](https://cli.github.com/), installed and authenticated (`gh auth login`) - `chorectl` uses your existing `gh` credentials and does not have a separate login flow

## Install

macOS/Linux:

```sh
curl -fsSL https://raw.githubusercontent.com/richardrigutins/chorectl/main/install.sh | sh
```

Windows (PowerShell):

```powershell
irm https://raw.githubusercontent.com/richardrigutins/chorectl/main/install.ps1 | iex
```

Installs to a per-user location (`~/.local/bin` or `%LOCALAPPDATA%\chorectl`) - no admin/sudo required.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
