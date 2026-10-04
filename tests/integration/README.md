# Integration tests

Use xUnit and `WebApplicationFactory` from ASP.NET Core MVC Testing for HTTP handlers and authorization. Use SQLite for constraints, conditional writes, and transaction rollback. `StockroomFactory` hosts the application against a seeded temporary SQLite file for each test and supplies test-only seed passwords.

Give each test an isolated database. For a competing-write scenario, connect separate contexts to the same temporary database file and coordinate the operation starts. Inspect the final quantity, status, and history. Reject unhandled lock errors as failures.

Run the cases:

```powershell
dotnet test --project tests/integration/Stockroom.IntegrationTests.csproj --configuration Release
```

Current status: 74 cases pass. `AuthenticationTests` covers T-01 and the absence of registration and password-recovery pages. `SeedTests` covers role assignment and the seed-rerun part of T-14. `SchemaConstraintTests` covers stock-movement CHECK constraints and the receipt index. `PurchaseServiceTests` covers T-05 through T-09 and T-11 for receipt, including competing reviews and receipts on separate connections to one file, and refuses Member actors for manager actions and Manager actors, including dual-role accounts, for purchase creation. `LockContentionTests` holds the write lock on another connection past a one-second timeout and verifies the retry message and unchanged data. `PurchaseWorkflowHttpTests` covers login redirects, inventory search and empty state (T-03), low stock (T-04), form validation, request ownership, and member, manager, or dual-role access to the wrong actions (T-02). Stock-issue cases wait for M3.
