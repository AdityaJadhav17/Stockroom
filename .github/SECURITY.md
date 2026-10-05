# Security policy

## Supported versions

Stockroom is a portfolio application for a local demonstration. It has no production deployment. The maintainer fixes reports against the `main` branch; no release branches receive fixes.

## Report a vulnerability

Use the **Report a vulnerability** button on the repository's Security tab. GitHub private vulnerability reporting is enabled for this repository. You can also email [Aditya Jadhav](mailto:aditya.jadhav7910@gmail.com).

Describe the affected commit or page, the steps to reproduce the problem, and the impact you observed. Keep passwords, session tokens, and personal data out of the report, and send exploit details only through a private channel. The maintainer will confirm the report, investigate it, and agree on disclosure with you. This project has no guaranteed response time.

## Security behavior and review

The [security review](../docs/security/security-review.md) records the M7 audit, its findings, and their status. The [release security review](../docs/security/release-security-review.md) records the v1.0.0 candidate review and the remediation of its findings. [ADR 0007](../docs/decisions/0007-m7-security-hardening.md) describes the session, header, and login-throttling controls.

## Remediation targets

Stockroom is a personal portfolio project maintained by one person. The targets below are best-effort goals, not service commitments. They apply to vulnerabilities in direct and transitive NuGet packages, the .NET runtime and SDK, and the GitHub Actions used by CI, and to reports about Stockroom's own code.

The maintainer starts from the severity in the GitHub Advisory Database or Microsoft's security release notes and may lower it by one level when the vulnerable code cannot run in Stockroom, such as a test-only package.

| Severity | Triage: confirm whether Stockroom is affected | Fix or temporary mitigation |
| --- | --- | --- |
| Critical | Within 3 days | Within 7 days |
| High | Within 7 days | Within 14 days |
| Medium | Within 14 days | Within 30 days |
| Low | Within 30 days | Within 90 days, or with the next scheduled update |

- **NuGet packages:** update the version in `Directory.Packages.props` and restore to regenerate the lock files. For a transitive package, update the direct package that brings it in, or reference the fixed version directly until that package updates.
- **.NET runtime and SDK:** Microsoft ships security fixes in monthly patch releases. Update the SDK version in `global.json`, which also sets the runtime that CI installs, within 14 days of a security release, or sooner if the table requires it.
- **GitHub Actions:** apply Dependabot's weekly update pull requests, and update an action named in an advisory within the target for its severity.

### Tracking, mitigation, and verification

- **Tracking:** Dependabot alerts and the NuGet audit in CI report vulnerable packages; the audit fails the build on any advisory. The maintainer checks Microsoft's .NET release notes after each monthly patch release. Each confirmed issue gets a GitHub issue, or a private security advisory while it is undisclosed, that records the severity, affected component, decision, and target date.
- **Temporary mitigations:** when no fix exists yet, record the mitigation, its reason, and a review date in the issue and in the remediation status of the [release security review](../docs/security/release-security-review.md). If CI must pass before a fix ships, suppress only that advisory with a `NuGetAuditSuppress` item whose comment names the advisory, the reason, and the review date, and remove it when the fix ships.
- **Verification:** after a fix, run the dependency check, the Release build with warnings treated as errors, and the integration and browser tests; confirm that hosted CI passes and that the Dependabot alert closes; then record the result in the issue and the [dependency audit](../docs/security/dependency-audit.md).

## Dependency checks

Run `pwsh -NoProfile -File scripts/Test-Dependencies.ps1` before you submit a package change. The check covers transitive NuGet packages and fails on an advisory or on missing audit data. [The dependency audit](../docs/security/dependency-audit.md) records the package graph and its limits.
