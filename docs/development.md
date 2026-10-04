# Development setup

## Prerequisites

Contributors need Git, PowerShell 7, and a [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), not a runtime alone. `global.json` selects SDK 10.0.401, allows later patches, and selects Microsoft.Testing.Platform for `dotnet test`.

The solution contains the web application in `src/Stockroom.Web/` and unit, integration, and E2E test projects. Only the integration project contains test cases after M2.

## Local setup

The developer executed these commands from the repository root on Windows 11 with SDK 10.0.401 on 2026-10-03. Replace the placeholder passwords with values of your choice. Identity requires at least six characters, an uppercase letter, a lowercase letter, a digit, and a non-alphanumeric character.

```powershell
dotnet restore Stockroom.slnx --locked-mode
dotnet tool restore
dotnet user-secrets set "Seed:MemberPassword" "<member password>" --project src/Stockroom.Web
dotnet user-secrets set "Seed:ManagerPassword" "<manager password>" --project src/Stockroom.Web
dotnet run --project src/Stockroom.Web -- seed
dotnet run --project src/Stockroom.Web --launch-profile http
```

The seed command applies migrations to `src/Stockroom.Web/stockroom.db` and printed `Seed complete: 3 accounts, 10 items, 10 stock movements.` on the first and second runs. Git ignores the database file. The command reads the environment from the launch profile and refuses to run outside Development:

```powershell
dotnet run --project src/Stockroom.Web --no-launch-profile -- seed
```

That command printed `The seed command runs only in Development. Current environment: Production.` and exited with code 1. Environment variables `Seed__MemberPassword` and `Seed__ManagerPassword` can replace user secrets.

Open `http://localhost:5080` and log in with one of the seeded accounts:

| Account | Role | Password setting |
| --- | --- | --- |
| `member1@stockroom.test` | Member | `Seed:MemberPassword` |
| `member2@stockroom.test` | Member | `Seed:MemberPassword` |
| `manager@stockroom.test` | Manager | `Seed:ManagerPassword` |

To recreate the demo database, stop the application, delete `src/Stockroom.Web/stockroom.db`, and run the seed command again. M4 adds an explicit reset option.

## Checks and tests

```powershell
pwsh -NoProfile -File scripts/Test-Repository.ps1
pwsh -NoProfile -File scripts/Test-Dependencies.ps1
dotnet build Stockroom.slnx --configuration Release --no-restore
dotnet test --project tests/integration/Stockroom.IntegrationTests.csproj --configuration Release --no-build
```

The integration tests host the application with `WebApplicationFactory` and give each test its own temporary SQLite file. The repository check covers tracked files only, so add new files to Git before relying on its result.

## Migrations

After changing the model in `src/Stockroom.Web/Data/AppDbContext.cs`, add a migration with the pinned tool:

```powershell
dotnet ef migrations add <Name> --project src/Stockroom.Web --output-dir Data/Migrations
```

The tool writes files with a byte-order mark and CRLF line endings. Convert them to UTF-8 without a BOM and LF line endings before running the repository check.

Keep source under `src/Stockroom.Web/` and tests under `tests/`. Do not add placeholder commands to the README as working setup instructions.

## Dependency management

Use [tests/README.md](../tests/README.md) for project responsibilities. `Directory.Packages.props` contains direct package versions, and each project has a `packages.lock.json`. `Directory.Build.props` enables NuGet Audit for direct and transitive packages at low severity and above. `NuGet.Config` uses the official package and audit sources.

To change packages, edit the central versions, run `dotnet restore Stockroom.slnx --force-evaluate`, inspect lock-file changes, then run `scripts/Test-Dependencies.ps1`. The script restores in locked mode and fails on source errors or advisories. A fresh checkout needs network access to NuGet and its audit feed. Build after changing versions.

## Repository checks and CI

`scripts/Test-Repository.ps1` checks required project documents, UTF-8 text, LF line endings, trailing whitespace, final newlines, and local Markdown file targets among tracked files. NuGet generates lock files with platform-specific line endings and no final newline, so the checker exempts those files from these two formatting checks. Git normalizes their committed line endings. The checker does not inspect remote URLs, Markdown anchors, or prose quality.

The [CI workflow](../.github/workflows/ci.yml) runs this script on Ubuntu with PowerShell 7. It also configures the SDK, restores and audits locked dependencies, builds the solution in Release, and runs the integration tests. It starts on pushes to `main`, pull requests targeting `main`, and manual dispatch. It has read access to repository contents and does not deploy.

The checkout tracks the owner's Stockroom repository on GitHub. The developer has not verified the hosted workflow results. Run the same checks on the local checkout and inspect GitHub Actions before relying on a hosted result.

Add the unit project to the CI test step when it contains cases. Configure browser installation and the running host before enabling E2E CI. A scaffold build proves no business behavior, and the dependency audit covers known NuGet advisories rather than downloaded browser binaries.

References: [NuGet audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages), [GitHub Actions syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), and [SDK setup](https://github.com/actions/setup-dotnet).
