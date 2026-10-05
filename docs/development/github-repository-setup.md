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

## Settings checked on 2026-10-05

The developer checked the public GitHub API without credentials. On 2026-10-05 the owner confirmed that the dependency graph, Dependabot alerts, secret scanning, and push protection are in place and working. The table distinguishes those confirmations from API results.

| Setting | Result | Evidence |
| --- | --- | --- |
| Community profile | 100% health | The public API returned 100 after M9 and the diagram updates merged. GitHub finds the community files in `.github/`. |
| Private vulnerability reporting | Enabled | `GET .../private-vulnerability-reporting` returned `enabled: true` |
| Branch protection on `main` | Not configured | `GET .../branches/main` returned `"protected": false` |
| Branch rules and rulesets | None | The public API returned empty branch rules and repository rulesets. |
| Hosted CI | Seven jobs passed at `12ffc87` | Run [37249415888](https://github.com/AdityaJadhav17/Stockroom/actions/runs/37249415888). |
| Dependency graph and Dependabot alerts | Enabled, owner-confirmed | Owner confirmation on 2026-10-05; the developer did not perform an authenticated API check. |
| Secret scanning and push protection | Enabled, owner-confirmed | Owner confirmation on 2026-10-05; the developer did not perform an authenticated API check. |

## Owner actions

1. Add a ruleset for `main` that requires a pull request, requires the `CI Status` check, and blocks force pushes and deletion.
2. After merging the release documentation, record passing CI on the commit selected for the release tag.
3. Check that the configured security controls remain enabled after future settings changes.

Optional: add repository topics that match the stack, such as `aspnet-core`, `razor-pages`, `sqlite`, and `inventory`. Actions approval defaults for fork pull requests and default token permissions remain unverified.
