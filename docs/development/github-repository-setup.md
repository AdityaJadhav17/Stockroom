# GitHub repository setup

This page lists the community files in the repository and the GitHub settings that only the owner can change.

## Community files

GitHub reads the community files from `.github/`.

| File | Purpose |
| --- | --- |
| [README](../../README.md) | Purpose, setup, tests, security behavior, and limitations |
| [MIT license](../../LICENSE) | Reuse terms the owner selected |
| [Code of conduct](../../.github/CODE_OF_CONDUCT.md) | Contributor Covenant text with the owner's private contact |
| [Contributing](../../.github/CONTRIBUTING.md) | How to propose, verify, and submit a change |
| [Security policy](../../.github/SECURITY.md) | Private vulnerability reporting and dependency checks |
| [Support](../../.github/SUPPORT.md) | Setup questions and defect reports |
| [Issue forms](../../.github/ISSUE_TEMPLATE) | Bug, feature, and question forms |
| [Pull request template](../../.github/pull_request_template.md) | Behavior, evidence, and review notes |
| [Dependabot](../../.github/dependabot.yml) | NuGet and GitHub Actions update proposals |
| [CI workflow](../../.github/workflows/ci.yml) | Repository, dependency, build, integration, and browser checks |

The license and code of conduct keep their standard wording.

## Settings checked on 2026-10-04

The developer queried the public GitHub API without credentials. Settings that need an authenticated call stay unverified.

| Setting | Result | Evidence |
| --- | --- | --- |
| Community profile | 100% health | `GET /repos/AdityaJadhav17/Stockroom/community/profile`, checked before M9 moved the files into `.github/` |
| Private vulnerability reporting | Enabled | `GET .../private-vulnerability-reporting` returned `enabled: true` |
| Branch protection on `main` | Not configured | `GET .../branches/main` returned `"protected": false` |
| Branch rules and rulesets | None | `GET .../rules/branches/main` and `GET .../rulesets` returned empty lists |
| Dependency graph and Dependabot alerts | Unverified | Needs an authenticated call or the Settings page |

## Owner actions

1. Add a ruleset for `main` that requires a pull request, requires the `CI Status` check, and blocks force pushes and deletion.
2. After merging M9, open the community profile page and confirm GitHub still finds the files in `.github/`.
3. Confirm that the dependency graph and Dependabot alerts are on, and review the latest Dependabot run.
4. Add repository topics that match the stack, such as `aspnet-core`, `razor-pages`, `sqlite`, and `inventory`.
