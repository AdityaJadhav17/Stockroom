# Demo script

A narrated walkthrough of two to three minutes: about 280 words of narration, plus time for the clicks. Seed a fresh database first (`dotnet run --project src/Stockroom.Web -- seed --reset`) so the numbers match. Each step names the screenshot that shows it.

All data is synthetic. Say so on screen or in the video description.

## 1. Problem and roles (20 seconds)

Screen: the login page.

> Stockroom tracks supplies for a makerspace. Members ask for materials; a manager decides what to buy, records deliveries, and hands out stock. Every change leaves a record. The data here is synthetic.

## 2. A member requests a purchase (35 seconds)

Screen: log in as `member1@stockroom.test`. [Member dashboard](../images/01-member-dashboard.png), then [the request form](../images/02-request-form.png).

> I'm logged in as a member. The dashboard flags PLA filament: two spools left against a reorder point of three. I open Request on that row, ask for five spools for a robotics workshop, and submit. The request is Pending. As a member, I can't approve it or change stock.

## 3. The manager approves and receives it (40 seconds)

Screen: log out, then log in as `manager@stockroom.test`. [Manager dashboard](../images/03-manager-dashboard.png), [review](../images/04-manager-review.png), then [receipt](../images/05-receipt-recorded.png).

> The manager's dashboard shows the request waiting for review. I open it and approve. Stock still reads two, because approval only authorizes the purchase. When the delivery arrives, I record the receipt. Stock goes to seven, and the request history shows who created, approved, and received it. The receipt button is gone, and the server ignores a repeated receipt.

## 4. Issuing stock, and a refusal (30 seconds)

Screen: Inventory, then Issue on the filament row. [Refused issue](../images/06-issue-refused.png).

> Now I hand out two spools for workshop prints, which leaves five. If I try to issue six, the application refuses and states that only five are in stock. Nothing changes.

## 5. The audit trail (25 seconds)

Screen: [History](../images/07-manager-history.png).

> The history page lists each decision and each stock movement with the actor, the time in UTC, the quantity change, and the reason. The movements add up to the current stock, and the page offers no way to edit or delete a record.

## 6. Persistence and security (30 seconds)

Screen: stay on History, or show the [member's rejected request](../images/08-member-rejection.png).

> Everything lives in SQLite. Each stock change and its history record commit in one transaction, and a conditional update stops two competing issues from overselling. Permissions come from the database on every request, so a demoted manager loses access at once, and logging out ends every session for the account. Integration tests cover the races and rollbacks; browser tests replay this walkthrough.

## Recording checklist

- Use the throwaway passwords from your user secrets, and keep them off screen.
- Hide the browser's address bar history and any local file paths.
- Keep the walkthrough under three minutes; cut step 6 to two sentences if it runs long.
