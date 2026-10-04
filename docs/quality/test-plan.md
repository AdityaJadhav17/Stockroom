# Test plan

Developers will execute these scenarios during implementation. After M5, SQLite integration tests and Chromium browser tests cover T-01 through T-15; the coverage map below lists them. Results below are local runs on Windows; hosted CI results are recorded separately after publication.

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

## Coverage map

Integration classes are in [tests/integration](../../tests/integration/README.md); browser classes are in [tests/e2e](../../tests/e2e/README.md). On 2026-10-04 the developer ran 141 integration cases and 7 browser cases locally on Windows; all passed.

| ID | Integration tests | Browser tests |
| --- | --- | --- |
| T-01 | `AuthenticationTests`; login redirects in `PurchaseWorkflowHttpTests` and `StockAndHistoryHttpTests` | `AccessRestrictionTests.LogoutBlocksProtectedPages` |
| T-02 | Member posts to approve, reject, receive, and issue; Manager and dual-role purchase creation; another member's request; direct service calls (`PurchaseWorkflowHttpTests`, `StockAndHistoryHttpTests`, `PurchaseServiceTests`, `StockServiceTests`) | `AccessRestrictionTests` (issue page and another member's request) |
| T-03 | `PurchaseWorkflowHttpTests` (columns, case-insensitive search, empty state) | Filament search in `PurchaseAndWithdrawalTests` |
| T-04 | `PurchaseWorkflowHttpTests.DashboardListsItemsAtOrBelowThreshold` | None |
| T-05 | `PurchaseServiceTests` and `PurchaseWorkflowHttpTests` (valid, zero, negative, missing, fractional, overflowing, over-limit quantities; blank and overlong reasons) | Valid request in `PurchaseAndWithdrawalTests` |
| T-06 | `PurchaseServiceTests` (approval with unchanged stock, rejection, invalid reasons) | Approval with stock at 2; `RejectionTests` |
| T-07 | `PurchaseServiceTests` (repeated reviews, competing approval and rejection) | `RejectionTests` (approve button gone after rejection) |
| T-08 | `PurchaseServiceTests` (receipt; Pending and Rejected refused) | Receipt to 7 in `PurchaseAndWithdrawalTests` |
| T-09 | `PurchaseServiceTests` (repeated and four competing receipts); `PurchaseWorkflowHttpTests` (repeated receipt) | None |
| T-10 | `StockServiceTests` and `StockAndHistoryHttpTests` (valid issue; invalid, excessive, and overflowing quantities; blank and overlong reasons) | Issue 2, then issue 6 refused, in `PurchaseAndWithdrawalTests` |
| T-11 | `PurchaseServiceTests.FailedHistoryWriteRollsBackReceipt`; `StockServiceTests.FailedMovementWriteRollsBackStockChange` | None |
| T-12 | `StockServiceTests.CompetingOneUnitIssuesCannotOversell` | None |
| T-13 | `StockAndHistoryHttpTests` (history content, ordering, visibility, no edit or delete); `StockServiceTests` (reopened database) | History in `PurchaseAndWithdrawalTests` and `AccessRestrictionTests`; `PersistenceTests` (application restart) |
| T-14 | `SeedTests`; `DemoDataTests` (complete-dataset comparison across reruns) | Each browser test starts from the seeded dataset |
| T-15 | `DemoDataTests` (reset, Production refusal, unsupported and locked targets, seed passwords) | None |

The coverage review found one gap: no test restarted the application process for T-13. `PersistenceTests` closes it. The review found no defect in application behavior. M8 added `ResponsiveLayoutTests` for phone-width layout, clipped card values at 320 pixels, focus and anchors clear of the sticky header, keyboard entry, and CSP console errors; eleven browser cases passed locally after M8. Browser tests do not repeat database races, rollback, or reset, which the SQLite tests cover more precisely.

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

On 2026-10-04 the explicit `DemoRecording` browser test ran this demo from a fresh seed, with a rejection added and without the repeated receipt (step 6), which `PurchaseAndWithdrawalTests` covers. It produced the README screenshots and a video in the ignored `artifacts/demo` directory. A narrated two- to three-minute recording by the owner remains a release task.

## Defect records

Record the story and test ID, dataset state, steps, expected result, and observed result. Attach relevant logs without passwords or session tokens. Reproduce the defect and add a regression test before marking the fix complete.
