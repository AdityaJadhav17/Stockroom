# User stories

All stories start in Planned status. Developers record implementation and verification evidence in the table below. Acceptance criteria refer to the rules in [mvp.md](mvp.md).

| ID | Story | Priority | Status | Evidence |
| --- | --- | --- | --- | --- |
| US-01 | Log in with a demo account | Must | In progress | M1: login, invalid login, logout, and unauthenticated dashboard access pass in `AuthenticationTests` (T-01). Member POST checks wait for manager actions (M2, M3). |
| US-02 | Inspect inventory and low-stock items | Must | Planned | None |
| US-03 | Request a purchase | Must | Planned | None |
| US-04 | Approve or reject a request | Must | Planned | None |
| US-05 | Receive an approved purchase | Must | Planned | None |
| US-06 | Issue stock | Must | Planned | None |
| US-07 | Trace a request or stock change | Must | Planned | None |
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
