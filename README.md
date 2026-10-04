# Stockroom

Members request supplies for a fictional campus makerspace. Managers review purchase requests, record deliveries, and track stock withdrawals. The planned application uses C#, ASP.NET Core Razor Pages, Entity Framework Core, and SQLite.

## Status

Milestones M1 through M4 are implemented. Members log in, browse and search inventory, see low-stock items, submit purchase requests, and read the history of their own requests. The manager approves or rejects pending requests, records receipts, issues stock with a reason, and reads the full request and stock-movement history. Receipts and issues save the stock change and its movement in one transaction, and conditional writes stop competing operations from adding stock twice or overselling. A Development seed command creates a synthetic dataset with one request in each status and reconciled stock history, and `seed --reset` recreates it. One hundred and thirty-five SQLite integration tests and seven Chromium browser tests cover roles enforced in pages and services, request ownership, validation, duplicate and competing operations, overflow, exhausted lock retries, transaction rollback, history visibility, persistence across a restart, seed reruns, reset, and the purchase, rejection, withdrawal, and access workflows in a browser. Release preparation (M6) remains. The project uses synthetic data; the author has no client deployment or production usage to report.

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

Read [the development setup](docs/development.md) to configure demo passwords, seed the database, and run the application. GitHub Actions shows separate jobs for repository checks, dependency auditing, the Release build, integration tests on Ubuntu and Windows, and Chromium browser tests on Ubuntu. Each test run uploads its report. The final `CI Status` check requires every job to pass. A unit-test job will be added when that project contains cases.

Read [CONTRIBUTING.md](CONTRIBUTING.md) before making changes.

## Community

Use [the code of conduct](CODE_OF_CONDUCT.md) for community participation, [support guidance](SUPPORT.md) for questions, and [the security policy](SECURITY.md) for private vulnerability reports. The owner licenses this project under the [MIT license](LICENSE).
