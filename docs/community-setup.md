# GitHub community setup

## Files in place

| File | Purpose |
| --- | --- |
| [README](../README.md) | Purpose, scope, status, and project navigation |
| [MIT license](../LICENSE) | Reuse terms selected with the owner's authorization |
| [Code of conduct](../CODE_OF_CONDUCT.md) | Standard Contributor Covenant text with the owner's private contact |
| [Contributing](../CONTRIBUTING.md) | Change, verification, and review process |
| [Security policy](../SECURITY.md) | Private vulnerability contact and current support scope |
| [Support](../SUPPORT.md) | Setup and usage questions |
| [Accessibility](../ACCESSIBILITY.md) | Planned interface checks and barrier reports |
| [Issue forms](../.github/ISSUE_TEMPLATE) | Bug, feature, and setup-question forms |
| [Pull request template](../.github/pull_request_template.md) | Behavior, evidence, and review notes |
| [Dependabot](../.github/dependabot.yml) | NuGet and GitHub Actions update proposals |

The owner approved use of the existing Git email for security and conduct reports. The developer retained standard license and code-of-conduct wording; the prose guide applies to project-authored documentation.

GitHub recommends these community files but also evaluates repository settings and metadata. [Community profile documentation](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories) describes the hosted checklist.

## After publication

1. Add a repository description and choose topics that match the implemented stack.
2. Confirm GitHub recognizes the license, code of conduct, contribution guidance, and issue/PR templates.
3. Enable Issues and private vulnerability reporting. Email remains the private reporting route until that setting exists.
4. Enable the dependency graph and Dependabot alerts where available; inspect the first dependency update run.
5. Confirm the foundation workflow passes on GitHub. Add its check to branch protection after the first run establishes the check name.
6. Add application tests and the demo before describing the MVP as complete.

The local files cannot enable hosted settings. The checkout tracks the owner's Stockroom repository on GitHub; the developer has not verified its community profile or hosted Actions. Review the settings above in the published repository.
