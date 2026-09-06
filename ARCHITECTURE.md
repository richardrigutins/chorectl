# Architecture

This document covers the technical decisions behind `chorectl` and how a command flows through the system end to end. For user-facing usage, see [README.md](README.md); for the commit/PR workflow, see [CONTRIBUTING.md](CONTRIBUTING.md).

## Technical decisions

### Language & runtime: .NET 10 (C#)

`dotnet publish -r <rid> --self-contained -p:PublishSingleFile=true` produces a standalone, single-file native executable per OS/architecture, with no separate runtime dependency for the end user.

`-p:PublishTrimmed=true` is deliberately **not** used. Trimming breaks Spectre.Console.Cli at runtime: the framework resolves command settings via reflection, which trimming strips, so every command throws immediately on startup. Binaries stay self-contained and single-file but untrimmed as a result (tens of MB each).

### TUI: Spectre.Console

Covers everything the interactive screens need: `MultiSelectionPrompt<T>` gives arrow-key navigation, space-to-toggle, enter-to-confirm, and pre-selected defaults out of the box (`Rendering/Dependabot/SelectionScreens.cs`). `Table` and live-display (`Status`) components cover the overview table (`OverviewTable.cs`) and the merge-progress screen (`ProgressDisplay.cs`). `Spectre.Console.Testing`'s `TestConsole` lets tests script key presses against the real prompts instead of mocking them.

### CLI framework: Spectre.Console.Cli

Typed command/settings classes, built-in help generation, and it composes directly with Spectre.Console's rendering - one dependency instead of separate CLI-parsing and rendering libraries. The command tree, descriptions, and examples live in one place, `Infrastructure/AppConfiguration.cs`, shared between the real entry point (`Program.cs`) and CLI-wiring tests so both exercise the identical setup.

### GitHub access: Octokit.NET (REST) + a thin GraphQL wrapper

- **REST**, via Octokit.NET, for repo discovery, merge, comment, and approve mutations.
- **GraphQL**, via a hand-written `HttpClient` wrapper (`GitHub/GraphQlClient.cs`, `Queries.cs`) posting hard-coded query strings to `https://api.github.com/graphql` with `System.Text.Json` deserialization, rather than the official `Octokit.GraphQL` package (still in beta).
- Fetching Dependabot PRs (with CI/review/merge state) and cross-referencing `vulnerabilityAlerts` (to set `IsSecurityUpdate`) rides along in the same per-repo GraphQL query, batched across repos, rather than one REST call per repo per PR - this keeps GitHub's point-based GraphQL rate limit (5,000 points/hour) well under budget even across dozens of repos. Each repo's `vulnerabilityAlerts` page is capped at 100 alerts per query - a repo with more than that silently under-reports `IsSecurityUpdate` for the overflow, though the user is warned about it rather than it passing unnoticed (surfaced as a console warning, or listed under `--json` output).

### Authentication: delegated to `gh`

`chorectl` requires the GitHub CLI (`gh`) to already be installed and authenticated. `GitHub/GhCliAuthenticator.cs` shells out to `gh --version` (minimum version check), `gh auth status`, and `gh auth token` - there is no separate login flow or credential storage of chorectl's own. The token is fetched **lazily**: `CachingGitHubAuthenticator` wraps the real authenticator in a `Lazy<string>`, so the underlying `gh` calls only happen the first time a command actually needs a GitHub client, and only once per process no matter how many clients (Octokit's `GitHubClient` via `GitHubCredentialStore`, and the GraphQL `HttpClient` via `GitHubAuthenticationHandler`) end up sharing it. This is why `chorectl --help` never touches `gh` at all.

### Packaging & self-update

- Distributed as GitHub Release assets, one archive per RID (`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`).
- `install.sh` / `install.ps1` handle first install: detect OS/arch, download the matching asset from the latest release, and place it in a per-user, non-elevated location (`~/.local/bin` or `%LOCALAPPDATA%\chorectl`) - no admin/sudo needed.
- `chorectl update` (`Core/Update/Updater.cs`) downloads the matching asset and replaces the running binary: a plain atomic `File.Move` on Unix (the OS keeps a running process attached to the file's inode, not its path), and a rename-aside-then-replace on Windows (`chorectl.exe` → `chorectl.exe.old`, new binary written to the original path), since Windows won't allow overwriting a running `.exe` directly. The leftover `.old` file is cleaned up unconditionally on the *next* launch (`Updater.CleanupPreviousVersion`, called from `CompositionRoot.RunAsync` before anything else runs).
- A throttled, best-effort startup check (`Core/Update/UpdateChecker.cs`) compares the running version against the latest release at most once per 24 hours, using the same authenticated GitHub client (so it rides the higher rate limit), and fails silently on any error so a slow or unreachable GitHub never blocks a real command. Skippable via `CHORECTL_NO_UPDATE_CHECK` or `skip_update_check` in config; always suppressed under `--help`, `--json`, and for the `update` command itself (see `CompositionRoot.ShouldCheckForUpdate`).

### Configuration

YAML at the XDG-style `~/.config/chorectl/config.yml` (`$XDG_CONFIG_HOME` if set), loaded/validated/persisted by `Core/Config/ConfigLoader.cs`. Missing keys - or a missing file entirely - resolve to `ChorectlConfig`'s built-in defaults, so a partially-written file is never a problem. `Load()` validates every value; `SetValue()` (used by `chorectl config set`) validates just the one key being written, so `config set` can still repair a config file with an unrelated bad value in it.

### State/caching

No persistent cache of GitHub state. Every invocation fetches fresh data - this is a low-frequency, interactive tool, not a polling daemon, so staleness risk outweighs the complexity of a cache-invalidation layer. (The update-check timestamp and the audit log are the only things chorectl persists across runs, and neither one caches GitHub *state*.)

### Versioning & release automation: semantic-release

Every merge to `main` is analyzed by [semantic-release](https://semantic-release.gitbook.io/) (`.github/workflows/release.yml`) against Conventional Commits since the last release (see [CONTRIBUTING.md](CONTRIBUTING.md) for the exact bump mapping). When a release is warranted, semantic-release creates the git tag, changelog, and GitHub Release; a second job then builds and uploads the per-RID binaries to that release, with the computed version passed in via `-p:Version=$VERSION`, which overrides the `0.0.0-dev` placeholder set in `Directory.Build.props` for local builds (see `CurrentVersion` and `chorectl --version`). No manually authored *release* version numbers exist anywhere in the repo. This is a Node-based tool but only runs inside the GitHub Actions runner - it adds nothing to the .NET project itself.

This makes Conventional Commits **load-bearing**, not just a style preference: a malformed commit message silently produces no release. `commitlint` runs in CI on every commit in a PR (not just the merge/PR title) to catch this before merge.

### Local dev tooling: Husky.Net

[Husky.Net](https://alirezanet.github.io/Husky.Net/) (the .NET port, unrelated to the JS Husky) wires native git hooks: a `pre-commit` hook runs `dotnet format --verify-no-changes`, and a `commit-msg` hook runs the same `commitlint` check CI runs. This shortens the feedback loop for the common case but doesn't replace CI - hooks can be bypassed with `--no-verify`, so CI stays the actual gate.

## Architecture

### Solution layout

```
Chorectl.slnx
src/
  Chorectl.Cli/         # entry point, command wiring, TUI rendering
    Infrastructure/     # DI composition root, command tree/config, Spectre.Console.Cli <-> DI adapter
    Commands/           # one class per CLI command, grouped by feature area
    Rendering/          # table/selection/progress screens, JSON output
  Chorectl.Core/        # everything Chorectl.Cli depends on; no Spectre dependency
    Domain/             # PR model, semver parsing, classification predicates
    GitHub/             # auth, REST/GraphQL clients, mutation interfaces, exception types
    Config/             # config schema and loader
    Audit/              # audit log
    Update/             # version comparison, self-update, startup update check
tests/
  Chorectl.Core.Tests/  # + Fixtures/: sanitized real API response JSON, committed
  Chorectl.Cli.Tests/
.github/workflows/      # ci.yml, release.yml, mutation.yml (see Technical decisions above)
```

### Data flow: `chorectl dependabot merge`

This is the most involved command; `rebase` and `approve` follow the same shape minus the poll/retry step.

1. Repos are discovered (`RestClient.DiscoverReposAsync`, honoring `--repo`/`-r` and the config's `exclude_repos`/`include_forks`) and their open Dependabot PRs fetched via a batched GraphQL query that also cross-references `vulnerabilityAlerts` to set `IsSecurityUpdate`; `--security`/`-s` filters the result. `ChorectlConfig` and the GitHub token are both resolved lazily, so neither is touched until a command actually needs them.
2. `Classifier.IsReadyToMerge` narrows to the ready subset - CI passing (or no checks), review not required, not `DIRTY`, not draft. `DIRTY` (an actual conflict) is the only `MergeStateStatus` that disqualifies a PR outright; `BEHIND` doesn't, since the branch may merge cleanly without a rebase - the merge attempt itself is the real test, not a state flag predicting it in advance. This predicate, and its siblings `NeedsRebase`/`NeedsApproval`/`DefaultSelected` in the same file, are the highest-consequence logic in the tool (see [Testing strategy](#testing-strategy)) - a wrong result means merging something that shouldn't be merged.
3. Selection: `--yes`/`--json` act on `Classifier.DefaultSelected` (patch/minor pre-checked per config, major/grouped/unknown-semver never auto-selected); otherwise the interactive checkbox screen shows the same defaults pre-checked.
4. Selected PRs are grouped by repo and processed one at a time: each is refetched first to catch anything Dependabot changed since list time, then a `DIRTY` PR is skipped immediately while a `BEHIND`-but-otherwise-ready one is merged directly with no wait. A merge attempt that fails with `MergeNotReadyException` - the only retryable case - enters a poll loop (every `merge_poll_interval_seconds` up to `merge_poll_timeout_seconds`) that refetches, bails out immediately if the PR turns `DIRTY`, and retries the merge once it's no longer `BEHIND` **and** CI is passing; any other failure (permission, or an unrecognized response) skips immediately instead. A rate limit on any mutation backs off transparently (`RateLimitBackoff`, capped at `max_backoff_seconds`); once that budget is exhausted the rest of the batch is marked not-attempted rather than each PR hitting the same wall individually.
5. Results render live (`ProgressDisplay`) or collect into the `--json` payload; a final summary prints, and the exit code is non-zero if anything failed.

### Error handling

A small set of custom exception types (`Chorectl.Core.GitHub`) let commands render the right message without string-matching errors. `GitHubAuthException`, `GitHubRateLimitException`, and `MergeNotReadyException` are covered above in the merge flow; the same types apply identically to `rebase`/`approve`'s own mutations, and `GitHubAuthException` doubles as the startup prerequisite-check failure (missing/unauthenticated `gh`). Two more exist outside that flow:

| Exception | Meaning | Handling |
|---|---|---|
| `RateLimitBackoffExhaustedException` | The backoff budget ran out before the limit cleared | Stops the rest of the batch rather than letting each remaining PR hit the same wall |
| `RepositoryNotFoundException` | `--repo`/`-r` named a repo that doesn't exist or isn't visible | Surfaced directly as a command error |

Everything else (404, 422, an unrecognized response shape) is treated as a likely tool bug, not a per-PR retry condition - it's surfaced with the raw error rather than reported as a misleading generic failure.

## Testing strategy

| Layer | What | Framework |
|---|---|---|
| Domain | Semver parsing, classification predicates, config load/save | xUnit, theory-based |
| GitHub adapter | REST/GraphQL query and mutation wrappers | xUnit + fixture JSON (`tests/Chorectl.Core.Tests/Fixtures/`) |
| TUI screens | Selection prompts, table/progress rendering | `Spectre.Console.Testing` (`TestConsole`) |
| CLI wiring | Command/flag parsing, output formatting | xUnit on `Spectre.Console.Cli` commands |
| Manual smoke | Full flow against a real scratch repo with real Dependabot PRs | manual checklist |

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) runs mutation testing against `Chorectl.Core`'s domain layer specifically (`SemverParser`, `Classifier`) - the pure, deterministic, highest-consequence logic, where a wrong `IsReadyToMerge` or `DefaultSelected` result means merging something that shouldn't be merged. See [CONTRIBUTING.md](CONTRIBUTING.md) for when and how to run it.
