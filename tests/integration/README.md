# Integration tests

Use xUnit and `WebApplicationFactory` from ASP.NET Core MVC Testing for HTTP handlers and authorization. Use SQLite for constraints, conditional writes, and transaction rollback. Add the web-project reference and test-host configuration during implementation.

Give each test an isolated database. For a competing-write scenario, connect separate contexts to the same temporary database file and coordinate the operation starts. Inspect the final quantity, status, and history. Reject unhandled lock errors as failures.

Run cases after adding them:

```powershell
dotnet test tests/integration/Stockroom.IntegrationTests.csproj --configuration Release
```

Current status: dependency scaffold only; no HTTP or database cases.
