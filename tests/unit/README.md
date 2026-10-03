# Unit tests

Use xUnit v3 for pure input and decision rules. The developer will add cases for positive integer quantities, required reasons, purchase transitions, and low-stock thresholds. Add a reference to the application project during implementation.

Keep database writes, HTTP requests, and browser interactions in their respective test layers. Use built-in assertions and add a mocking library after a specific dependency requires one.

Run cases after adding them:

```powershell
dotnet test --project tests/unit/Stockroom.UnitTests.csproj --configuration Release
```

Current status: dependency scaffold only; no unit cases.
