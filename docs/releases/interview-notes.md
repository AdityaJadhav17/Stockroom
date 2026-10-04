# Interview notes

Talking points about Stockroom's design, each tied to the code and the test that proves it.

## Atomic stock updates and overselling

`StockService.IssueAsync` reduces stock with one conditional `UPDATE`: `WHERE Id = @item AND Quantity >= @issued`. The database decides whether enough stock remains, so no read-then-write gap exists for a competing request to slip through. If the update touches zero rows, the service reports the current quantity and writes nothing. The movement record joins the same transaction, so stock and history commit together.

Receipt works the same way: the service moves a request from Approved to Received with a conditional update on its status, then adds the stock. A unique index on the receipt movement's request reference backs this up in the schema, so a second receipt cannot even be stored.

Proof: `StockServiceTests.CompetingOneUnitIssuesCannotOversell` starts two one-unit issues against one remaining unit on separate connections; one succeeds, stock ends at zero, and one movement exists. `PurchaseServiceTests` runs four competing receipts and gets one stock increase.

## Authorization from the database

Hiding a button proves nothing, so each manager action checks the role twice: the page requires the Manager role, and the service asks the database (`HasRoleAsync`) before it writes. History queries embed the role check in their SQL, so a member's query returns only that member's requests. The cookie's role claims are rebuilt from the database on every request (`ValidationInterval = 0`), so a demoted manager loses access on the next click rather than when the cookie expires.

Proof: member POSTs to approve, receive, and issue are refused with no data change (`PurchaseWorkflowHttpTests`, `StockAndHistoryHttpTests`); `RoleRemovalAppliesOnTheNextRequest` demotes a signed-in manager.

## Rollback and concurrency tests

Tests that touch transactions run against real SQLite files. An in-memory substitute cannot show SQLite's locking or rollback. The rollback tests force the second write of a transaction to fail and then check that the first write is gone: `FailedHistoryWriteRollsBackReceipt` and `FailedMovementWriteRollsBackStockChange`. `LockContentionTests` holds the write lock on another connection past the timeout and checks that each operation returns a retry message and leaves the data unchanged.

## Session revocation and login throttling

Logout rotates the account's security stamp. Because the application validates the stamp on every request, a copied session cookie stops working on its next use (`LogoutRevokesACopiedSessionCookie`). The trade-off: logout ends the account's sessions in every browser.

Login POSTs go through ASP.NET Core's sliding-window rate limiter: five per client address per minute, in six segments. A segment can return its permits early, so up to ten attempts fit in one window; the lockout threshold is therefore eleven, which one client needs about 100 seconds to reach. A fixed window failed review because it allowed ten attempts across a boundary. `LoginWindowBoundaryCannotDoubleTheAttemptsAllowed` reproduces that pattern.

## What each test layer proves

| Layer | Count | Proves |
| --- | --- | --- |
| Integration (xUnit, `WebApplicationFactory`, SQLite) | 156 | Business rules, authorization in pages and services, request ownership, validation, transactions, races, lock handling, security headers and sessions, seed and reset |
| Browser (Playwright, Chromium) | 11 | The real application process end to end: the purchase, rejection, and withdrawal workflows, access restrictions, persistence across a restart, phone layouts, keyboard focus, and the absence of CSP errors |

The browser tests avoid repeating race and rollback cases; the SQLite tests cover those more precisely. The unit-test project has no tests yet.

## Résumé bullets

These describe the project, not the author's role. Choose the verb that matches your part in the work before you use them.

- Stockroom: an inventory and purchase-approval web application in C# with ASP.NET Core Razor Pages, EF Core, and SQLite, using conditional updates inside transactions so competing stock issues cannot oversell.
- Verified with 156 SQLite integration tests and 11 Playwright browser tests covering authorization, concurrency, rollback, and session security, run in a seven-job GitHub Actions pipeline on Ubuntu and Windows.
