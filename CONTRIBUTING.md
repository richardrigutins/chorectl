# Contributing

## Getting started

Requires the .NET 10 SDK (see [ARCHITECTURE.md](ARCHITECTURE.md) for why). Clone the repo, then:

```sh
dotnet restore
dotnet build
dotnet test
```

`dotnet restore` also installs this repo's Husky.Net git hooks automatically (see below) - no separate setup step needed.

To run the CLI from source using dotnet, run the following from the repo root:

```sh
dotnet run --project src/Chorectl.Cli -- <command> [args]
```

For example:

```sh
dotnet run --project src/Chorectl.Cli -- dependabot list --repo my-repo
```

## Commit messages

This repo follows [Conventional Commits](https://www.conventionalcommits.org/), enforced by `commitlint` on every commit (via a Husky.Net `commit-msg` hook) and again in CI:

```
<type>(<optional scope>): <description>
```

Common types:

| Type | Meaning |
|---|---|
| `feat` | A new feature |
| `fix` | A bug fix |
| `chore` | Tooling, dependencies, or other changes with no release impact |
| `docs` | Documentation only |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `test` | Adding or correcting tests |

Commit type also drives versioning: releases are cut automatically by [semantic-release](https://semantic-release.gitbook.io/) from these commit messages (`fix` → patch, `feat` → minor, `feat!`/`BREAKING CHANGE:` → major, everything else → no release). A malformed commit message doesn't just fail the lint check - it can silently skip a release that should have shipped, so get the type right.

## Mutation testing

[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) runs against `Chorectl.Core`'s Domain layer (`SemverParser`, `Classifier`) - the pure, highest-consequence classification logic. It's too slow for per-PR CI, so it isn't part of `ci.yml` - instead it runs weekly via `.github/workflows/mutation.yml` (also triggerable on demand from the Actions tab), which posts the score as a job summary. Run it locally the same way before a release that touches that logic:

```sh
dotnet tool restore
cd src/Chorectl.Core
dotnet stryker
```

## Branch naming

Branch off `main` using `feature/<short-description>`, e.g. `feature/rate-limit-backoff`.

## Pull requests

- Open the PR against `main` and fill in the [PR template](.github/pull_request_template.md) (description, related issue if any, testing notes, checklist).
- Keep the title under 70 characters, in Conventional Commits format (`feat: ...`, `fix: ...`) - a squash-merge workflow would use it as the resulting commit message, but this repo merges PRs with a merge commit, so every commit inside the PR is linted individually (see above) rather than just the title.
- Before opening a PR, make sure the full local check passes: `dotnet format --verify-no-changes`, `dotnet build`, `dotnet test`. CI (`.github/workflows/ci.yml`) runs the same checks plus `commitlint` on every commit in the PR, and branch protection on `main` requires it to pass before merging - there are no direct pushes to `main`.
- A PR that changes classification logic (`SemverParser`, `Classifier`) should re-run mutation testing locally (see above) if the change could plausibly lower the mutation score, since that check only runs weekly in CI, not per PR.
- Once merged, [semantic-release](https://semantic-release.gitbook.io/) picks up the commits automatically - there's nothing further to do to cut a release.
