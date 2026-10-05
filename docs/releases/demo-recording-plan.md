# Demo recording plan

Record a narrated walkthrough of 2 to 3 minutes using the [script](demo-script.md). Show the browser and the stock changes; pause capture for password entry and the server restart. Use one theme and a viewport of at least 1280 by 800 pixels.

## Prepare a disposable database

Stop any Stockroom server using port 5080. Use PowerShell 7 from the repository root. Configure the two seed passwords through the [setup guide](../development/setup.md#local-setup) before recording; the seed command reads your existing user secrets.

These commands create a new database in a timestamped folder under ignored `artifacts/`. They leave your working `stockroom.db` in place. Run them before starting capture:

```powershell
$taskDemoDirectory = Join-Path (Get-Location) ('artifacts/demo/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskDemoDirectory | Out-Null
$taskDemoDatabase = Join-Path $taskDemoDirectory 'stockroom-demo.db'
$env:ConnectionStrings__Stockroom = "Data Source=$taskDemoDatabase"
dotnet run --project src/Stockroom.Web --launch-profile http -- seed
if ($LASTEXITCODE -ne 0) { throw 'Demo seed failed. Fix the reported configuration before recording.' }
dotnet run --project src/Stockroom.Web --launch-profile http
```

Keep this terminal open. The connection-string override applies to processes started from it. Sign in as `member1@stockroom.test` at `http://localhost:5080` and confirm that PLA filament starts at two spools with a threshold of three. During the recording, use `manager@stockroom.test` for review, receipt, and stock issues.

For the restart segment, pause capture, press Ctrl+C in that terminal, and run:

```powershell
dotnet run --project src/Stockroom.Web --launch-profile http
```

Refresh the browser after the server starts. Use the same terminal so the application opens the same database. Closing and reopening a browser tab does not demonstrate an application restart.

## Rehearse

| Step | Expected result |
| --- | --- |
| Member requests 5 spools | New filament request is Pending; stock is 2 |
| Manager approves | Request is Approved; stock remains 2 |
| Manager records receipt | Request is Received; stock becomes 7 |
| Manager issues 2 | Stock becomes 5; History includes an issue of -2 |
| Manager attempts to issue 6 | Refusal; stock stays 5; no extra movement |
| Manager restarts the server and refreshes | Stock remains 5 and the issue remains in History |

Write down the new request's number during rehearsal so you can find the right request. For each new take, stop the server and run the preparation block again. The new timestamped folder gives that take a fresh dataset without a reset.

## Capture

1. Close unrelated tabs, hide notifications, and check the microphone with a ten-second sample.
2. Sign in as the member before capture. Display the synthetic-data explanation in the opening narration.
3. Follow the script. Hold the pointer still after each action so the viewer can read the status and quantity.
4. Pause capture before entering the manager password and before the server restart. Resume after each operation completes. State that you restarted the application and refresh the page on camera.
5. Save the recording under `artifacts/demo/` or outside the checkout. Git ignores that folder. Retain one finished take and its database path until you review the recording.

## Review and publish

- [ ] Narration lasts 2 to 3 minutes and identifies the data as synthetic.
- [ ] The viewer can read the stock sequence `2 -> 7 -> 5`, the refused issue of 6, and the history entries.
- [ ] The final segment includes a browser refresh after a server restart using the same database.
- [ ] Audio is clear, and the recording shows no passwords, secrets, personal accounts, or local filesystem paths.
- [ ] The walkthrough makes no customer, deployment, or untested performance claims.
- [ ] You choose a video host or release attachment, upload the finished video, and add its reference to the release checklist and notes.

The automated recorder remains available for screenshots and a silent walkthrough; see the [setup guide](../development/setup.md). The narrated recording adds your explanation of the user actions and the stock rules.

After recording, stop the server and remove the connection-string override before starting Stockroom against your normal database:

```powershell
Remove-Item Env:ConnectionStrings__Stockroom
```

Keep the disposable database and video until you finish the review. The plan does not delete either file.
