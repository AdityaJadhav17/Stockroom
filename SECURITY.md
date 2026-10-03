# Security policy

## Project status

The maintainer has prepared the requirements and test foundation. The repository has no application release or production deployment. The maintainer will address reports affecting the default branch; there are no supported release branches yet.

## Report a vulnerability

Email [Aditya Jadhav](mailto:aditya.jadhav7910@gmail.com) with a description, affected package or commit, reproduction steps, and the impact you observed. Keep passwords, customer records, and session tokens out of the report. Use a private report for exploit details rather than a public issue.

After the owner publishes the repository and enables GitHub private vulnerability reporting, reporters can use the Security tab's reporting option. Email remains the contact until that feature is available. The maintainer will confirm the report, investigate it, and coordinate disclosure with the reporter. This project has no guaranteed response time.

## Dependency checks

Contributors run `pwsh -NoProfile -File scripts/Test-Dependencies.ps1` before submitting package changes. The check includes transitive NuGet packages and fails if it finds an advisory or cannot obtain audit data. Read [the recorded audit](docs/dependency-audit.md) for the initial package graph and limitations.
