# Contributing

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

More contributing guidelines (setup, testing, PR process) will land here once there's an actual command surface to contribute to.
