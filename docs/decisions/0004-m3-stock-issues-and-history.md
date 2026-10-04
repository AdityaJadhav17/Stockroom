# ADR 0004: M3 stock issues and history

Date: 2026-10-04

Status: Accepted. Implemented in M3.

## Context

M3 adds manager stock issues (US-06) and history views (US-07). The architecture requires a conditional stock write for withdrawals, one transaction for the quantity change and its movement, server-side authorization, and the lock policy from [ADR 0003](0003-m2-purchase-workflow.md).

## Decision

| Area | Decision |
| --- | --- |
| Service | `StockService.IssueAsync` validates the actor, quantity, and reason, then runs `UPDATE InventoryItems SET Quantity = Quantity - @n WHERE Id = @id AND Quantity >= @n` inside a transaction. If no row changes, it rolls back and reports the quantity in stock. Otherwise it inserts an `Issue` movement with a negative delta, the trimmed reason, the actor, and the UTC time, then commits. |
| Shared guards | `ServiceGuard` holds the database role lookup and the exhausted-lock handling used by `PurchaseService` and `StockService`. Both services return the same forbidden and retry messages. |
| Input rules | Issues reuse `PurchaseRules`: whole numbers from 1 to 10,000 and a trimmed reason of 1 to 500 characters. An issue larger than the stock on hand fails with a shortage message. |
| Authorization | `/Inventory/Issue/{itemId}` requires the Manager role. `IssueAsync` repeats the check against database roles and takes the actor from the authenticated session. |
| History | `/History` shows request events to every signed-in user, filtered by `PurchaseService.RequestHistory`: managers see every request and members see their own. `RequestHistory`, `VisibleRequests`, and `StockService.MovementHistory` take only the viewer's identifier and check the Manager role in the database inside the query, so a caller cannot widen visibility and a removed role takes effect on the next query. Request details also list that request's events. A `Created` event shows the purchase reason as its note. |
| Ordering | History lists the newest entry first by UTC time, with the record identifier breaking ties between entries saved in one operation. |
| Immutability | History pages have no POST handlers. The application exposes no edit or delete operation for request events or stock movements. |

## Alternatives

| Option | Reason for rejection |
| --- | --- |
| Read the quantity, then save the reduced value | Two issues can both read one unit and both save zero, which oversells the item. |
| Rely on the `Quantity >= 0` CHECK constraint alone | The constraint prevents negative stock but surfaces as a database error instead of a shortage message. It remains as a backstop. |
| A separate history service | The visibility rules already live with the request and stock services that own the records. |

## Consequences

An issue holds the SQLite write lock for one short transaction. Opening, receipt, and issue movements reconcile with item quantities. Correcting an incorrect issue requires a later stock-correction feature, which the MVP defers.
