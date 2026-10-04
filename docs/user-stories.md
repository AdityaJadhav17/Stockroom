# User stories

All stories start in Planned status. Developers record implementation and verification evidence in the table below. Acceptance criteria refer to the rules in [mvp.md](mvp.md).

| ID | Story | Priority | Status | Evidence |
| --- | --- | --- | --- | --- |
| US-01 | Log in with a demo account | Must | Implemented; awaiting review | M1: login, invalid login, logout, and unauthenticated access pass in `AuthenticationTests` (T-01). M2 and M3: member POSTs to approve, reject, receive, and issue redirect to access denied and change no data (`PurchaseWorkflowHttpTests`, `StockAndHistoryHttpTests`, T-02); direct service calls by a member are refused (`PurchaseServiceTests`, `StockServiceTests`). |
| US-02 | Inspect inventory and low-stock items | Must | Implemented; awaiting review | M2: `PurchaseWorkflowHttpTests` covers columns, case-insensitive search, empty state (T-03), and below, equal, and above threshold (T-04). M3: inventory shows the committed quantity after an issue (`StockAndHistoryHttpTests`). Browser run on 2026-10-04 against a local demo database: filament went from 7 to 5 after a two-spool issue, an issue of six reported a shortage and left 5, and the stock and history remained after an application restart. |
| US-03 | Request a purchase | Must | Implemented; awaiting review | M2: `PurchaseServiceTests` and `PurchaseWorkflowHttpTests` cover valid creation, zero, negative, fractional, missing, overflowing, and over-10,000 quantities, blank and 501-character reasons (T-05), ownership, and denial for Manager and dual-role accounts in pages and the service (T-02). |
| US-04 | Approve or reject a request | Must | Implemented; awaiting review | M2: `PurchaseServiceTests` covers approval with unchanged stock, rejection with reason, invalid reasons (T-06), and repeated and competing reviews (T-07). Browser run on 2026-10-04 showed two spools after approval. |
| US-05 | Receive an approved purchase | Must | Implemented; awaiting review | M2: `PurchaseServiceTests` covers receipt, Pending and Rejected refusal (T-08), repeated and competing receipts (T-09), overflow, and rollback after a forced event-write failure (T-11). Browser run on 2026-10-04 showed two to seven spools and an unchanged seven after a repeated receipt. |
| US-06 | Issue stock | Must | Implemented; awaiting review | M3: `StockServiceTests` and `StockAndHistoryHttpTests` cover a valid issue with actor, UTC time, negative delta, and reason; zero, negative, missing, fractional, overflowing, over-10,000, and excessive quantities; blank and 501-character reasons (T-10); rollback after a forced movement-write failure (T-11); and two competing one-unit issues against one unit, leaving zero stock and one Issue movement (T-12). Browser run on 2026-10-04 against a local demo database: filament went from 7 to 5 after a two-spool issue, an issue of six reported a shortage and left 5, and the stock and history remained after an application restart. |
| US-07 | Trace a request or stock change | Must | Implemented; awaiting review | M3: `StockAndHistoryHttpTests` and `StockServiceTests` cover manager history with actor, UTC time, action, item, quantity change, reason, and request link; the purchase reason on Created events; member history limited to their own requests, with visibility resolved from database roles; deterministic newest-first order; empty state; no edit or delete forms; failed operations leaving history unchanged; and persistence through a reopened database (T-13). Browser run on 2026-10-04 against a local demo database: filament went from 7 to 5 after a two-spool issue, an issue of six reported a shortage and left 5, and the stock and history remained after an application restart. |
| US-08 | Prepare a repeatable demo dataset | Must | In progress | M1: the seed command creates ten items, two members, and one manager; `SeedTests` verifies roles and rerun preservation (part of T-14). Request examples and reset remain. |

## US-01: Log in with a demo account

As a member or manager, I need to log in so I can use the actions assigned to my role.

- Given a seeded account, valid credentials open the dashboard; invalid credentials show a neutral login error.
- An unauthenticated visitor cannot read inventory, requests, or history.
- A member's direct POST to an approval, receipt, or withdrawal handler fails authorization and changes no data.
- Logout ends the session. The next protected request requires login.

Rules: BR-01, BR-02. Tests: T-01, T-02.

## US-02: Inspect inventory and low-stock items

As a member, I need to inspect supplies so I can identify materials to request.

- The inventory view shows item name, unit, quantity, and reorder threshold.
- A name search returns matching items and shows a clear empty state if none match.
- The dashboard lists items with quantity at or below the threshold.
- Views show committed quantities after a receipt or withdrawal.

Rule: BR-09. Tests: T-03, T-04.

## US-03: Request a purchase

As a member, I need to request an item and quantity so the manager can review the purchase.

- A valid item, positive whole-number quantity, and reason create a Pending request with the member and UTC creation time.
- Zero, negative, fractional, or out-of-range quantities and blank reasons produce field errors and create no request. Reject reasons over 500 characters.
- The member can view their own request. Another member cannot access it through its identifier.
- A Manager account cannot submit a purchase request.

Rules: BR-02, BR-03, BR-08, BR-10. Tests: T-02, T-05.

## US-04: Approve or reject a request

As a manager, I need to review a pending request so the member can see my decision.

- Approval changes Pending to Approved and records reviewer and UTC time. Stock remains unchanged.
- Rejection changes Pending to Rejected and records the reviewer, time, and reason.
- A blank or overlong rejection reason fails validation and leaves the request Pending.
- A repeated or competing review changes the request once; a later attempt reports the current status.

Rules: BR-01, BR-04, BR-08, BR-10. Tests: T-02, T-06, T-07.

## US-05: Receive an approved purchase

As a manager, I need to record a delivery so the stock reflects supplies on hand.

- Receipt of an Approved request adds its approved quantity, marks it Received, and records the actor, time, and stock movement in one transaction.
- Pending and Rejected requests cannot receive stock.
- Repeated or competing receipts add stock once and create one receipt movement.
- A database failure leaves the request status, quantity, and history at their previous values.

Rules: BR-04, BR-05, BR-07, BR-08, BR-11. Tests: T-08, T-09, T-11.

## US-06: Issue stock

As a manager, I need to issue supplies and record a reason so I can account for their use.

- A valid issue reduces available stock and creates a movement with actor, UTC time, and reason.
- Zero, negative, fractional, excessive, or out-of-range quantities fail without changing stock or history.
- Two competing issues cannot consume more than available stock. With one spool left and two one-spool issues, one succeeds and one fails.
- A database failure rolls back the quantity change and movement together.

Rules: BR-03, BR-06, BR-07, BR-08, BR-10. Tests: T-10, T-11, T-12.

## US-07: Trace a request or stock change

As a manager, I need to inspect history so I can explain stock quantities and purchase decisions.

- History shows actor, UTC timestamp, action, item, quantity delta, and related request where applicable.
- A member sees their own request's decision details; the manager sees the full request and movement history.
- The web interface provides no edit or delete action for history records.
- Failed and duplicate operations create no successful-action history entry.

Rules: BR-01, BR-07, BR-08, BR-11. Tests: T-02, T-13.

## US-08: Prepare a repeatable demo dataset

As the developer, I need a predictable dataset so I can reproduce the demo and tests.

- A documented Development-only command creates ten items, two members, one manager, and consistent request and movement examples.
- A second seed run leaves account counts, quantities, and history unchanged.
- A reset command requires an explicit reset option and touches only the configured local Development database.
- Filament starts at two spools with a reorder threshold of three. Demo credentials stay outside tracked files.

Tests: T-14, T-15.
