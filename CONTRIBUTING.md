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

More contributing guidelines (setup, testing, PR process) will land here once there's an actual command surface to contribute to.
