# Integration tests

Use xUnit and `WebApplicationFactory` from ASP.NET Core MVC Testing for HTTP handlers and authorization. Use SQLite for constraints, conditional writes, and transaction rollback. `StockroomFactory` hosts the application against a seeded temporary SQLite file for each test and supplies test-only seed passwords.

Give each test an isolated database. For a competing-write scenario, connect separate contexts to the same temporary database file and coordinate the operation starts. Inspect the final quantity, status, and history. Reject unhandled lock errors as failures.

Run the cases:

```powershell
dotnet test --project tests/integration/Stockroom.IntegrationTests.csproj --configuration Release
```

Current status: 21 cases pass. `AuthenticationTests` covers T-01 and the absence of registration and password-recovery pages. `SeedTests` covers role assignment and the seed-rerun part of T-14. `SchemaConstraintTests` verifies that SQLite rejects an `Issue` movement with a missing, blank, or 501-character reason, a `Receipt` movement without a purchase request, and a second receipt for one request. Authorization of manager actions (T-02) and the integrity cases wait for M2 and M3.
