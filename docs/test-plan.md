# Test plan

Developers will execute these scenarios during implementation. After M3, the [integration project](../tests/README.md) covers T-01 through T-13 and the seed-rerun part of T-14. Reset (T-15) and the reset-dependent part of T-14 have no cases yet.

Use xUnit v3 for pure input and decision rules. Use ASP.NET Core MVC Testing with xUnit for login, authorization, and request ownership. Test constraints, competing writes, and rollback with SQLite. Concurrency tests need separate contexts and connections to the same temporary database file. Use Playwright for .NET with xUnit for browser workflows against a running application.

## Acceptance scenarios

| ID | Scenario | Expected result |
| --- | --- | --- |
| T-01 | Valid and invalid login; logout; unauthenticated access | Valid login succeeds; invalid login reports an error; protected requests require a session. |
| T-02 | Member posts to manager actions; Manager submits a purchase; another member requests a private request URL | Server rejects access and changes no records. |
| T-03 | Browse and search inventory, including no match | Show accurate quantities and a readable empty state. |
| T-04 | Compare quantities below, at, and above the threshold | Include below and equal quantities in low-stock results. |
| T-05 | Valid request; blank or overlong reason; invalid, missing, fractional, or overflowing quantity | Create one Pending request for valid input; create no record for invalid input. |
| T-06 | Approve or reject Pending request; submit blank rejection reason | Valid review records actor and time; approval leaves stock unchanged; invalid rejection leaves Pending status. |
| T-07 | Submit a review twice; compete approval against rejection | Commit one transition and one review event; report current state to the other request. |
| T-08 | Receive Approved request; attempt receipt of Pending or Rejected request | Valid receipt adds the approved quantity and records history; invalid states change nothing. |
| T-09 | Repeat receipt and send two competing receipt requests | Add quantity once and create one receipt movement and event. |
| T-10 | Issue valid stock; attempt zero, negative, fractional, excessive, or overflowing quantity; omit reason | Accept valid issue; reject invalid input without stock or history changes. |
| T-11 | Force failure during movement/event persistence after a conditional write | Roll back quantity and request status; leave no partial history. |
| T-12 | With one spool available, send two competing one-spool withdrawals | One succeeds; the other reports a conflict or shortage; final quantity is zero with one issue movement. |
| T-13 | Inspect purchase and stock history; restart app | Show actor, UTC time, action, quantity delta, and references; retain committed records after restart. |
| T-14 | Run seed twice and compare accounts, requests, quantities, and movements | Preserve the first seed's records on the second run. |
| T-15 | Reset Development database with explicit option; attempt reset outside Development | Reset the configured demo database in Development; reject reset outside Development. |

For competing-write tests, synchronize the start of the operations. Assert final database state and permitted outcomes. Handle SQLite lock errors through the same policy as the application; do not accept an unhandled exception as successful conflict handling.

## Manual demo

1. Prepare a fresh Development dataset. Show the synthetic-data note.
2. Log in as a Member. Show filament at two spools and threshold three.
3. Request five spools with the reason `Materials for the robotics workshop`.
4. Log in as the Manager. Approve the request and show that stock remains two.
5. Record delivery. Show seven spools and the receipt history.
6. Repeat the receipt action. Show a clear message and quantity seven.
7. Issue two spools with a workshop reason. Show quantity five and the issue movement.
8. Attempt to issue six spools. Show rejection and quantity five.
9. Log in as a Member and verify restricted actions through the server, not button visibility alone.
10. Restart the application and confirm the committed stock and history.

## Defect records

Record the story and test ID, dataset state, steps, expected result, and observed result. Attach relevant logs without passwords or session tokens. Reproduce the defect and add a regression test before marking the fix complete.
