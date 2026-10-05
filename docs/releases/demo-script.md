# Demo script

Aim for 2 minutes 50 seconds, including clicks and the restart. Follow the [recording plan](demo-recording-plan.md) to create a fresh demo database before rehearsal. Keep the browser at `http://localhost:5080` and start signed in as `member1@stockroom.test`.

## 0:00 to 0:15: Purpose

Screen: member dashboard. Point to the low-stock filament row.

> Stockroom tracks supplies for a fictional campus makerspace. Members request purchases, and a manager reviews them, records deliveries, and issues stock. I am using synthetic accounts and inventory for this demonstration.

## 0:15 to 0:50: Member request

Screen: open Inventory, then Request beside PLA filament. Enter quantity `5` and reason `Robotics workshop filament`. Submit and show Pending.

> As a member, I can see two spools of filament against a reorder threshold of three. I request five spools for a robotics workshop and include a reason. The request is pending. I can track its status and read its history.

## 0:50 to 1:25: Approval and receipt

Screen: pause capture for the account switch. Log out, sign in as `manager@stockroom.test`, and resume. Open the new filament request from Requests. Click Approve request, show Approved, then record receipt of five spools. Open Inventory and show quantity `7`.

> As the manager, I review the request and approve it. Approval leaves stock at two. After the delivery arrives, I record the receipt. The quantity increases to seven, and I can see the creation, approval, and receipt events on the request page.

## 1:25 to 1:55: Issue and refusal

Screen: click Issue beside filament. Issue `2` with reason `Workshop prints`. Open Issue again and try `6` with reason `Additional workshop prints`. Show the refusal and quantity `5`.

> I issue two spools for workshop prints, leaving five. I then try to issue six. The page explains that five are available. The refused issue leaves the quantity at five and adds no stock movement.

## 1:55 to 2:20: History

Screen: open History. Show the filament receipt of `+5` and issue of `-2`, with their actor, reason, and UTC time.

> In History, I can trace the receipt and issue to the manager, the reason, and the time. For filament, the opening two, receipt of five, and issue of two add up to the five spools in stock.

## 2:20 to 2:50: Restart and test evidence

Screen: pause capture. Restart the server in the same terminal using the same database, resume capture, and refresh Inventory. Show quantity `5`, then open History and show the recorded issue.

> I have restarted the application. Five spools remain, and I can still read the issue in History. The project uses C#, Razor Pages, EF Core, and SQLite. The test suites contain 156 integration cases and 11 Chromium browser cases, including competing stock writes and restart persistence.

## Optional extension

For a longer technical walkthrough, demonstrate a rejection with a reason, then show the member's view. Label it as an extra segment and keep it outside the three-minute release recording.

## Evidence after recording

Add the recording URL or file reference to the [definition of done](../planning/definition-of-done.md) and [release notes](v1.0.0.md). The script describes the planned recording; it does not prove that you recorded the video.
