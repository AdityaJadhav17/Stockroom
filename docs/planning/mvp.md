# MVP scope

## Purpose

Members need a record of supply requests. Managers need to approve purchases and account for stock movements. The owner will demonstrate this workflow with a fictional campus makerspace and synthetic data.

The first release must support a repeatable local demonstration and a fresh-checkout setup. The owner has a 12 to 16 hour implementation budget across two working days. The estimate assumes a working SDK and basic familiarity with running and debugging a web application.

## Release scope

| Area | Included behavior |
| --- | --- |
| Accounts | Seeded Member and Manager accounts with framework login and logout |
| Dashboard | Low-stock items and pending purchase requests |
| Inventory | Seeded items, name search, quantities, units, and reorder thresholds |
| Purchase requests | One item per request, positive quantity, and a required reason |
| Review | Manager approval or rejection with a required rejection reason |
| Receipt | Manager receipt of the full approved quantity in one operation |
| Withdrawal | Manager stock issue with a positive quantity and a required reason |
| History | Actor, UTC timestamp, action, item, quantity change, and request reference |
| Demo data | Ten items, two member accounts, and one manager account |

The manager creates stock issues from the inventory screen. Members can view inventory, create purchase requests, and view their own requests. Managers can view requests from both members.

## Business rules

| ID | Rule |
| --- | --- |
| BR-01 | Require authentication for inventory, requests, and history. Limit approvals, receipts, withdrawals, and full history to Manager accounts. |
| BR-02 | Members create purchase requests. Managers cannot create purchase requests in this release, which prevents self-approval. |
| BR-03 | Accept positive whole-number request and withdrawal quantities. Use whole units such as spools, boxes, or packs. |
| BR-04 | Allow `Pending -> Approved`, `Pending -> Rejected`, and `Approved -> Received`. Reject other transitions. |
| BR-05 | Approval leaves stock unchanged. Receipt adds the full approved quantity once. |
| BR-06 | Reject a withdrawal that exceeds available stock. Protect this rule during competing requests. |
| BR-07 | Save each quantity change and its stock-movement record in one transaction. Roll back both if either write fails. |
| BR-08 | Record the actor and UTC time for creation, review, receipt, and stock issue. Retain the original records. |
| BR-09 | Mark an item as low stock if its quantity is less than or equal to its reorder threshold. |
| BR-10 | Require a reason for purchase requests, rejection, and stock issues. Trim input and reject blank values; limit reasons to 500 characters. |
| BR-11 | On a repeated receipt, return a clear message and leave quantity and history unchanged. |

## Deferred work

The owner will consider item administration, multi-item orders, partial receipts, cancellations, returns, and stock corrections after the MVP. Supplier management, payments, email, offline support, public registration, password recovery, and cloud hosting sit outside the two-day budget.

## Release evidence

The owner will run the purchase and withdrawal demonstration in [test-plan.md](../quality/test-plan.md), inspect authorization and concurrency tests, and verify the setup from a fresh checkout. Track remaining work against [definition-of-done.md](definition-of-done.md).
