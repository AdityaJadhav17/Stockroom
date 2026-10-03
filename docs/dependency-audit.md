# Testing dependency audit

Date: 2026-10-03, America/Los_Angeles.

Result: NuGet reported no known vulnerabilities in the resolved test dependencies, including transitive packages. The inventory contains 70 unique package/version pairs across three projects. The developer restored the packages and built Release with zero warnings and errors.

## Direct packages

| Package | Version | Role |
| --- | --- | --- |
| xunit.v3 | 4.0.1 | Unit, integration, and E2E test framework |
| xunit.runner.visualstudio | 4.0.0 | Test adapter |
| Microsoft.NET.Test.Sdk | 18.10.1 | Test discovery and execution support |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | Integration test host |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | SQLite integration tests |
| Microsoft.Playwright.Xunit.v3 | 1.63.0 | Browser E2E foundation |

The package identifier `xunit.v3` has version 4.0.1; the package name retains v3. The developer obtained these stable versions from the official NuGet package indexes and verified that the projects compile together.

## Method and evidence

The developer used SDK 10.0.401 and an explicit NuGet audit source at `https://data.nuget.org/v3/index.json`. NuGet obtains its advisories from the GitHub Advisory Database. [Microsoft describes the audit source and limitations](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).

Executed report command:

```powershell
dotnet package list --project MakerspaceLedger.slnx --include-transitive --vulnerable --format json --output-version 1 --no-restore
```

Read [the vulnerability report](security/nuget-vulnerability-report.json) and [the package inventory](security/package-inventory.json). The developer converted local absolute project paths to repository-relative paths in those records. Project lock files retain resolved versions and package content hashes.

## Advisory review

The resolved SQLite graph includes `SQLitePCLRaw.lib.e_sqlite3` 2.1.12. [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q) covers versions through 2.1.11 for CVE-2025-6965. The resolved version falls outside that affected range.

The developer checked [Playwright's maintainer advisories](https://github.com/microsoft/playwright/security/advisories) alongside the NuGet result. The maintainer page listed no published advisories at the time of review.

## Continuing checks

Run `pwsh -NoProfile -File scripts/Test-Dependencies.ps1` after package changes and before merging. CI runs the same check. Restore treats NU1900 through NU1905 as errors, including unavailable audit data. Dependabot will propose NuGet and GitHub Actions updates after publication.

A clean result describes known advisories at the time of the check. This review does not certify the application, perform a source-code security audit, or inspect browser binaries downloaded through Playwright. The repository has no application cases, and the developer has not installed project browsers. Review browser updates when enabling E2E execution.
