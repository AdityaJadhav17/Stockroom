# Architecture

Status: implemented. M1 built the application shell, Identity, and the schema below. M2 added `PurchaseService` with the receipt transaction, M3 added `StockService` with the withdrawal transaction and history views, and M4 added the demo dataset and reset. M7 added the security pipeline in `SecurityPolicy.cs` ([ADR 0007](../decisions/0007-m7-security-hardening.md)). ADRs [0002](../decisions/0002-m1-authentication-and-seed.md), [0003](../decisions/0003-m2-purchase-workflow.md), [0004](../decisions/0004-m3-stock-issues-and-history.md), and [0005](../decisions/0005-m4-demo-dataset-and-reset.md) record the decisions; [0006](../decisions/0006-m6-release-preparation.md) records M6 error handling.

## Application boundaries

Stockroom is one ASP.NET Core Razor Pages application on .NET 10. Razor Pages render the forms and tables. C# services handle purchase transitions and stock changes. Entity Framework Core stores the application and Identity records in SQLite.

```text
Browser -> Razor Page handler -> C# service -> EF Core -> SQLite
                     |
             ASP.NET Core Identity
```

Keep the web project under `src/Stockroom.Web/` and tests under `tests/`. Organize the web project into `Pages/`, `Services/`, `Data/`, and `Models/`. Start with these folders inside one project. Create another project after a concrete dependency or testing need appears.

## Records

| Record | Fields and responsibility |
| --- | --- |
| ApplicationUser | Framework-managed user identifier, email, and role membership |
| InventoryItem | Identifier, unique SKU, name, unit, non-negative integer quantity, non-negative reorder threshold |
| PurchaseRequest | Identifier, item, positive integer quantity, reason, requester, status, creation time, reviewer, review time, rejection reason, receiver, receipt time |
| StockMovement | Identifier, item, signed quantity delta, action type, actor, UTC time, reason, optional purchase-request reference |
| RequestEvent | Identifier, request, action, actor, UTC time, and decision note |

Use database constraints for quantity bounds and foreign keys for references. Use a unique constraint on a receipt movement's request reference to prevent a second receipt record. Record opening stock through opening movements so the developer can reconcile an item's quantity with its movement history.

Use UTC for stored timestamps. Label displayed times with their timezone. Validate input on the server and reject arithmetic overflow before accepting a quantity change.

## Request transitions

| Current status | Actor and action | Next status | Stock effect |
| --- | --- | --- | --- |
| Pending | Manager approves | Approved | None |
| Pending | Manager rejects with reason | Rejected | None |
| Approved | Manager receives full delivery | Received | Add request quantity |

Read the actor from the authenticated session. Do not accept a submitted user identifier as proof of identity. A member can read only their own purchase requests. Limit stock issues and full history to Manager accounts.

## Transaction design

For receipt, start a database transaction and claim the transition with a conditional write that requires Approved status. If no row changes, return the current state without changing stock. Add stock, a receipt movement, and a request event before committing. A failure must roll back the status and stock writes together.

For a withdrawal, reduce stock with a conditional write that requires enough quantity. Check the affected-row count, then create the stock movement in the same transaction. A read followed by an unconditional write cannot protect the quantity during competing requests.

Use the database conditions for the invariant and a clear conflict message for the user. SQLite serializes writes and can return lock-contention errors. The implementation must either retry a transient lock within a bounded limit or return a retry message without partial writes. Test this behavior with separate connections to the same database file.

## Login and demo data

Use ASP.NET Core Identity with cookie authentication and framework form protections. Restrict purchase creation to Members and manager actions to Managers. Seed roles and demo accounts through a Development-only command. Obtain demo passwords from environment variables or .NET user secrets; document local setup without committing credentials.

Seed once, preserve existing records on a later seed run, and offer reset through an explicit Development-only option. Keep generated database files outside Git.

## Design limits

SQLite suits the local demonstration. The owner will reassess the database before public hosting or a SQL Server requirement. A database migration will require new provider-specific tests. The owner will add supplier records or multi-item purchases after reviewing the MVP workflow.

Rationale: [foundation decision](../decisions/0001-application-foundation.md).
