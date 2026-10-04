# Security policy

## Supported versions

Stockroom is a portfolio application for a local demonstration. It has no production deployment. The maintainer fixes reports against the `main` branch; no release branches receive fixes.

## Report a vulnerability

Use the **Report a vulnerability** button on the repository's Security tab. GitHub private vulnerability reporting is enabled for this repository. You can also email [Aditya Jadhav](mailto:aditya.jadhav7910@gmail.com).

Describe the affected commit or page, the steps to reproduce the problem, and the impact you observed. Keep passwords, session tokens, and personal data out of the report, and send exploit details only through a private channel. The maintainer will confirm the report, investigate it, and agree on disclosure with you. This project has no guaranteed response time.

## Security behavior and review

The [security review](../docs/security/security-review.md) records the M7 audit, its findings, and their status. [ADR 0007](../docs/decisions/0007-m7-security-hardening.md) describes the session, header, and login-throttling controls.

## Dependency checks

Run `pwsh -NoProfile -File scripts/Test-Dependencies.ps1` before you submit a package change. The check covers transitive NuGet packages and fails on an advisory or on missing audit data. [The dependency audit](../docs/security/dependency-audit.md) records the package graph and its limits.
