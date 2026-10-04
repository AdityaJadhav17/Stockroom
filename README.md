# Stockroom

Members request supplies for a fictional campus makerspace. Managers review purchase requests, record deliveries, and track stock withdrawals. The planned application uses C#, ASP.NET Core Razor Pages, Entity Framework Core, and SQLite.

## Status

Milestones M1 and M2 are implemented. Members log in, browse and search inventory, see low-stock items, and submit purchase requests. The manager approves or rejects pending requests and records receipt of approved purchases; receipt updates the request, stock, stock movement, and request event in one transaction. Seventy-four SQLite integration tests cover login, roles enforced in pages and in the service, request ownership, validation, transitions, duplicate and competing reviews and receipts, overflow, exhausted lock retries, and transaction rollback. Stock issues, history views, and the reset command remain planned. The project uses synthetic data; the author has no client deployment or production usage to report.

The first release covers one purchase workflow and its stock history. The implementation budget is two working days, with 12 to 16 hours available for development and verification.

## Planned workflow

1. A member sees two filament spools in stock and requests five more.
2. A manager approves the request. Stock remains at two.
3. A manager records receipt of the five spools. Stock increases to seven.
4. A manager issues two spools and records a reason. Stock decreases to five.
5. A reviewer checks the approvals and stock movements in the history view.

Developers will test permission checks, repeated delivery submissions, and competing withdrawals alongside this workflow.

## Project documents

Start with the [documentation index](docs/README.md).

| Document | Purpose |
| --- | --- |
| [MVP](docs/mvp.md) | Release scope and business rules |
| [Personas](docs/personas.md) | Users, responsibilities, and access |
| [User stories](docs/user-stories.md) | Acceptance criteria for implementation |
| [Architecture](docs/architecture.md) | Application structure and data model |
| [Delivery plan](docs/delivery-plan.md) | Milestones, estimates, and SDLC checkpoints |
| [Definition of done](docs/definition-of-done.md) | Evidence required before release |
| [Test plan](docs/test-plan.md) | Failure cases and demo procedure |
| [Development setup](docs/development.md) | Prerequisites and CI commands |
| [Test structure](tests/README.md) | Unit, integration, E2E, and fixture responsibilities |
| [Dependency audit](docs/dependency-audit.md) | Package versions and vulnerability-check evidence |

## Repository checks

Run this command from the repository root with PowerShell 7:

```powershell
pwsh -NoProfile -File scripts/Test-Repository.ps1
```

The check covers required files, text formatting, and local Markdown file links. With the .NET SDK available, audit dependencies, build the solution, and run the application tests:

```powershell
pwsh -NoProfile -File scripts/Test-Dependencies.ps1
dotnet build Stockroom.slnx --configuration Release --no-restore
dotnet test --project tests/integration/Stockroom.IntegrationTests.csproj --configuration Release --no-build
```

Read [the development setup](docs/development.md) to configure demo passwords, seed the database, and run the application. The GitHub Actions workflow runs repository checks, a dependency audit, the Release build, and the integration tests. It does not run browser tests yet.

Read [CONTRIBUTING.md](CONTRIBUTING.md) before making changes.

## Community

Use [the code of conduct](CODE_OF_CONDUCT.md) for community participation, [support guidance](SUPPORT.md) for questions, and [the security policy](SECURITY.md) for private vulnerability reports. The owner licenses this project under the [MIT license](LICENSE).
