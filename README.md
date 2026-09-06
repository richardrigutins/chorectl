# chorectl

`chorectl` is a CLI tool for the recurring maintenance chores of owning and managing multiple GitHub repos. The main feature area is Dependabot PR triage: reviewing, rebasing, approving, and merging Dependabot PRs across all of your repos from a single keyboard-driven workflow instead of the GitHub web UI, one PR at a time.

In other words, it's an overly complicated script to merge and approve multiple Dependabot PRs at once, with a nicer TUI and some extra smarts (e.g. pre-selecting patch/minor bumps, skipping PRs that aren't mergeable yet, retrying a merge that temporarily isn't ready, etc.).

> Note: this tool has been built with the aid of generative AI tools.

## Prerequisites

- [GitHub CLI (`gh`)](https://cli.github.com/) 2.5.0 or later, installed and authenticated (`gh auth login`) - `chorectl` uses your existing `gh` credentials and does not have a separate login flow

## Install

Supported platforms: Windows x64, Linux x64, macOS x64, macOS ARM64.

Download the binary matching your OS/architecture from the latest [Release](https://github.com/richardrigutins/chorectl/releases), or use one of the install scripts below, which do that for you:

macOS/Linux:

```sh
curl -fsSL https://raw.githubusercontent.com/richardrigutins/chorectl/main/install.sh | sh
```

Windows (PowerShell):

```powershell
irm https://raw.githubusercontent.com/richardrigutins/chorectl/main/install.ps1 | iex
```

Both scripts install to a per-user location (`~/.local/bin` or `%LOCALAPPDATA%\chorectl`) - no admin/sudo required. Set `CHORECTL_INSTALL_DIR` before running the script to install somewhere else instead.

### Updating

```sh
chorectl update
```

Downloads the matching binary from the latest release and replaces the current one in place - no need to re-run the install script. `chorectl` also checks for a newer version once every 24 hours on startup and prints a one-line notice when one is available; it never updates itself automatically. Skip the check for a single run with `CHORECTL_NO_UPDATE_CHECK=1`, or permanently via [config](#configuration) (`skip_update_check: true`).

### Uninstalling

Delete the binary from wherever it was installed:

- macOS/Linux: `rm ~/.local/bin/chorectl`
- Windows: delete `%LOCALAPPDATA%\chorectl\chorectl.exe`

This doesn't remove your config or audit log (`~/.config/chorectl/`, `~/.local/share/chorectl/`) - delete those too for a full cleanup.

## Commands

All `dependabot` subcommands accept these common flags:

| Flag | Meaning |
|---|---|
| `-r, --repo <NAME>` | Scope to a single repo by name (e.g. `-r my-repo`), skipping discovery and fetch for every other repo |
| `-s, --security` | Filter to PRs that resolve a Dependabot security alert |
| `--json` | Print structured JSON instead of the interactive TUI; implies acting on the full eligible set (see `--yes` below) |
| `-v, --verbose` | Print diagnostic detail about discovery, fetch, and state-check steps |

`merge`, `rebase`, and `approve` additionally accept:

| Flag | Meaning |
|---|---|
| `--dry-run` | Show the selection and summary without merging/rebasing/approving anything |
| `--yes` | Skip the selection screen and act on the default-selected set |

### `chorectl dependabot list`

Prints a status table of every open Dependabot PR across your non-archived, non-fork repos, with CI, review, and merge-conflict indicators plus the detected semver bump.

```sh
chorectl dependabot list
chorectl dependabot list -r my-repo
chorectl dependabot list --security
```

### `chorectl dependabot merge`

Shows a checkbox selection of every PR that's ready to merge (CI passing, review satisfied, no conflicts), with patch and minor bumps pre-checked and major/grouped bumps left for you to review manually. Confirming re-verifies each PR's state immediately before merging it, and retries a merge that briefly isn't mergeable (e.g. a sibling PR just got merged and this one is temporarily behind) before giving up.

PRs that don't meet the merge criteria are not shown.

```sh
chorectl dependabot merge
chorectl dependabot merge --dry-run
chorectl dependabot merge --yes --json   # for cron/automation
```

### `chorectl dependabot rebase`

Shows a checkbox selection of PRs that need a rebase (conflicting or behind the base branch), pre-selected, and posts `@dependabot rebase` on each one you confirm. Doesn't wait for the rebase to finish - it reports "requested" and exits.

PRs that don't need a rebase are not shown, unless `--all` is used.

```sh
chorectl dependabot rebase
chorectl dependabot rebase --all   # widen to every open PR, e.g. to force a CI re-run; only PRs that actually need a rebase stay pre-selected
```

### `chorectl dependabot approve`

Shows a checkbox selection of PRs awaiting review, pre-selected, and submits an approving review on each one you confirm.

PRs that don't need a review are not shown.

```sh
chorectl dependabot approve
chorectl dependabot approve --security
```

### `chorectl config get` / `chorectl config set`

View or edit your preferences (see [Configuration](#configuration) below).

```sh
chorectl config get
chorectl config set merge_method rebase
chorectl config set default_select.major true
```

### `chorectl update`

Downloads and installs the latest release in place. See [Updating](#updating).

### `chorectl --help`

Root help lists every command; `chorectl <command> --help` (or `chorectl dependabot <subcommand> --help`) shows that command's flags and an example.

## Configuration

Preferences live in a YAML file at `~/.config/chorectl/config.yml` (or `$XDG_CONFIG_HOME/chorectl/config.yml` if that variable is set). The file is created on first use of `chorectl config set`; any key you don't set falls back to its default below. Edit it directly, or use `chorectl config get`/`chorectl config set <key> <value>`.

```yaml
exclude_repos: []                 # ["repo-name", ...] - repos to skip entirely
include_forks: false
merge_method: squash              # squash | merge | rebase
merge_poll_interval_seconds: 15
merge_poll_timeout_seconds: 120
max_backoff_seconds: 300          # cap for rate-limit backoff
skip_update_check: false
default_select:
  patch: true
  minor: true
  major: false
  grouped: false
```

| Key | Meaning |
|---|---|
| `exclude_repos` | Comma-separated repo names to exclude from discovery, e.g. `chorectl config set exclude_repos foo,bar` |
| `include_forks` | Include forked repos in discovery |
| `merge_method` | Merge strategy used by `dependabot merge` |
| `merge_poll_interval_seconds` | How often to re-check a PR that isn't mergeable yet |
| `merge_poll_timeout_seconds` | How long to keep retrying before skipping that PR |
| `max_backoff_seconds` | Upper bound for exponential backoff on a GitHub rate limit |
| `skip_update_check` | Disable the startup update check |
| `default_select.*` | Whether each bump tier is pre-checked on the merge-selection screen |

`config set` validates the key and value before writing - an unknown key or an invalid value (e.g. `merge_method bogus`) is rejected with a specific error rather than silently ignored.

## Audit log

Every merge, rebase request, approval, skip, and failure is appended as one JSON line to `~/.local/share/chorectl/audit.log` (or under `$XDG_DATA_HOME` if set), for a record of what the tool did across runs.

## Known Limitations (v1)

- Personal-account repos only - no organization or team scanning
- GitHub.com only - no GitHub Enterprise Server
- Fixed wait time between retries when a merge isn't ready yet, not adaptive to Dependabot's actual processing state
- No auto-update - `chorectl update` must be run manually; updates are never applied silently
- No daemon or scheduled mode - invoke manually or from an external cron job with `--yes --json`

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for setup instructions and the commit/PR workflow, and [ARCHITECTURE.md](ARCHITECTURE.md) for the technical decisions and data flow behind the tool.

## License

[MIT](LICENSE)
