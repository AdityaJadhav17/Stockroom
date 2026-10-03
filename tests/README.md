# Tests

The developer has configured three .NET 10 test projects. These projects contain dependencies and lock files; they contain no application test cases yet. A successful build verifies the test foundation, not the MVP behavior.

| Folder | Responsibility | Tools |
| --- | --- | --- |
| [unit](unit/README.md) | Input rules and purchase-state decisions without infrastructure | xUnit v3 and built-in assertions |
| [integration](integration/README.md) | HTTP authorization, request ownership, database integrity, and transactions | xUnit, ASP.NET Core MVC Testing, and SQLite |
| [e2e](e2e/README.md) | Browser workflows against a running application | Playwright for .NET with xUnit |
| [fixtures](fixtures/README.md) | Shared synthetic-data conventions | Test builders and isolated database fixtures during implementation |

Use the test IDs in [the acceptance plan](../docs/test-plan.md). Add references to the web project after scaffolding it. Avoid placeholder tests that assert constants or label unfinished behavior as a test pass.

## Commands

```powershell
dotnet restore MakerspaceLedger.slnx --locked-mode
dotnet build MakerspaceLedger.slnx --configuration Release --no-restore
pwsh -NoProfile -File scripts/Test-Dependencies.ps1
```

Run application tests after adding cases. The E2E project also needs installed browser binaries and a running application; see its folder README.

Read [the dependency audit](../docs/dependency-audit.md) for the package versions, results, and scope of the initial check.
