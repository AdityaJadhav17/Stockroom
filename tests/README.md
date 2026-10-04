# Tests

The solution contains three .NET 10 test projects. The integration project contains the M1 cases for login, logout, unauthenticated access, role assignment, and seed reruns. The unit and E2E projects contain no cases yet.

| Folder | Responsibility | Tools |
| --- | --- | --- |
| [unit](unit/README.md) | Input rules and purchase-state decisions without infrastructure | xUnit v3 and built-in assertions |
| [integration](integration/README.md) | HTTP authorization, request ownership, database integrity, and transactions | xUnit, ASP.NET Core MVC Testing, and SQLite |
| [e2e](e2e/README.md) | Browser workflows against a running application | Playwright for .NET with xUnit |
| [fixtures](fixtures/README.md) | Shared synthetic-data conventions | Test builders and isolated database fixtures during implementation |

Use the test IDs in [the acceptance plan](../docs/test-plan.md). The integration project references the web project; add the reference to another project when it gains cases. Avoid placeholder tests that assert constants or label unfinished behavior as a test pass.

## Commands

```powershell
dotnet restore Stockroom.slnx --locked-mode
dotnet build Stockroom.slnx --configuration Release --no-restore
pwsh -NoProfile -File scripts/Test-Dependencies.ps1
dotnet test --project tests/integration/Stockroom.IntegrationTests.csproj --configuration Release --no-build
```

`global.json` selects Microsoft.Testing.Platform, so `dotnet test` takes the project through `--project`. The E2E project also needs installed browser binaries and a running application; see its folder README.

Read [the dependency audit](../docs/dependency-audit.md) for the package versions, results, and scope of the initial check.
