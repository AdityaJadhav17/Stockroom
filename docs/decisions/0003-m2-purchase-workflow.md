# ADR 0003: M2 purchase workflow

Date: 2026-10-04

Status: Accepted. Implemented in M2.

## Context

M2 adds inventory, purchase requests, manager review, and receipt (US-02 through US-05). The architecture requires conditional writes for transitions, one transaction per receipt, server-side role and ownership checks, and a defined policy for SQLite lock contention.

## Decision

| Area | Decision |
| --- | --- |
| Service | `PurchaseService` holds the request rules: actor roles, input validation, creation, approval, rejection, receipt, and the request-visibility query. Pages call it and contain no transition logic. |
| Actor roles | Each service operation reads the actor's roles from the Identity tables, so a caller cannot assert a role. Creation requires the Member role and rejects any account that also holds the Manager role. Approval, rejection, and receipt require the Manager role. A refused actor receives `ForbiddenMessage` and the service writes nothing. |
| Transitions | Each transition runs `UPDATE ... WHERE Id = @id AND Status = @expected` through `ExecuteUpdateAsync` inside a transaction. If no row changes, the service rolls back and reports the current status. A repeated or competing review or receipt therefore commits once. |
| Receipt | One transaction claims `Approved -> Received`, adds stock with a conditional `UPDATE` that requires `Quantity <= Int32.MaxValue - requestQuantity`, then inserts the `Receipt` movement and the `Received` request event. Any failure rolls back all four writes. The unique receipt index remains a database backstop. |
| Lock contention | Microsoft.Data.Sqlite starts transactions with `BEGIN IMMEDIATE`, so a writer takes the database write lock before its first statement. A competing writer waits and retries until the command timeout, 30 seconds by default or the connection string's `Default Timeout`. If the retries run out with `SQLITE_BUSY` or `SQLITE_LOCKED`, the transaction rolls back and the service returns `BusyMessage`, which tells the user to try again. |
| Quantity range | Request quantities must be whole numbers from 1 to 10,000. The upper bound defines "out of range" for BR-03 and US-03. Model binding rejects fractional and non-numeric input before the service runs. |
| Page authorization | `/Requests/New` uses the `PurchaseRequester` policy, which requires the Member role and excludes the Manager role, so an account with both roles cannot create a purchase (BR-02). Approve, reject, and receive handlers live on `/Requests/Review/{id}`, which requires the Manager role for every handler. A forbidden request redirects to `/Account/AccessDenied`, which returns status 403. |
| Ownership | Members see only their own requests in lists, the dashboard, and details. A member who opens another member's request receives 404, which does not confirm that the request exists. |
| Search | Inventory search lowercases the name and the term and uses `instr()`, so `%` and `_` match literally. Case folding covers ASCII names. |

## Alternatives

| Option | Reason for rejection |
| --- | --- |
| Read the status, then save the entity | Two requests can read Pending or Approved before either writes. |
| Optimistic concurrency tokens | The conditional `UPDATE` already expresses the expected state and needs no extra column. |
| Application-level retry loop | The provider's busy retry and `BEGIN IMMEDIATE` already queue writers within a bounded time. |
| Role checks inside each handler of a shared page | A missed check would expose a manager action; a page-level attribute covers every handler. |

## Consequences

Receipt, review, and creation each hold the SQLite write lock for a short transaction. A database with long-running writers would push competing requests toward the 30-second timeout. Stock issues in M3 should follow the same conditional-update pattern.
