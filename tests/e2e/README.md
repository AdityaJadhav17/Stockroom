# Browser E2E tests

Playwright for .NET with xUnit drives Chromium through the main workflows. Selectors use accessible names, roles, and form labels. Assertions wait for visible page state; the tests contain no fixed sleeps.

## Fixture

`StockroomApp` starts the web application's own build output as a separate `dotnet` process for each test:

| Resource | Owned by the fixture |
| --- | --- |
| Directory | A new `stockroom-e2e-*` directory under the system temporary folder |
| Database | `stockroom-e2e.db` in that directory, seeded by the real `seed` command with the documented demo dataset |
| Credentials | Generated member and manager passwords passed as environment variables, which take precedence over user secrets |
| Port | Assigned by the operating system (`http://127.0.0.1:0`) and read from the server log |
| Readiness | The fixture waits until `/Account/Login` returns a success status |

Disposal stops that process and deletes only the directory it created. Cleanup runs every step even if an earlier one fails, skips browser contexts a test already closed, stops the application last, and then reports any cleanup errors. A leaked server would keep the test host's output open and stall `dotnet test`. The fixture never reads or changes `src/Stockroom.Web/stockroom.db` or another configured database. Tests run one at a time because each starts its own server and browser.

`BrowserTest` opens one browser context per signed-in user and records a Playwright trace. When a test fails, it saves each context's trace and full-page screenshot and the server log under `bin/<Configuration>/net10.0/e2e-artifacts/`, or under the directory in `STOCKROOM_E2E_ARTIFACTS`. Git ignores both locations. Open a trace with `pwsh tests/e2e/bin/Release/net10.0/playwright.ps1 show-trace <file>`.

## Cases

| Class | Workflow | Acceptance tests |
| --- | --- | --- |
| `PurchaseAndWithdrawalTests` | Member sees filament at 2 and requests 5; manager approves (stock 2), receives (7), issues 2 (5), and cannot issue 6 (5); history shows the events and movements | T-03, T-06, T-08, T-10, T-13 |
| `RejectionTests` | Blank rejection reason refused; manager rejects with a reason; requester sees the decision; stock stays at 2 | T-06, T-07 |
| `AccessRestrictionTests` | Member cannot open the issue page or another member's request; member history shows only their requests; logout blocks protected pages | T-01, T-02, T-13 |
| `PersistenceTests` | Manager issue survives an abrupt application restart in stock and history | T-13 |
| `CleanupTests` | Fixture regression: after a test closes its own browser context, cleanup still stops the application and deletes its directory | None |
| `ResponsiveLayoutTests` | At 390 pixels wide, member and manager pages do not scroll sideways and log no console errors, including CSP violations; at 320 pixels no stacked-card value is clipped; the sticky header never covers keyboard focus or an anchored heading; the skip link is the first keyboard stop; navigation marks the current section | None (M8 layout) |
| `UiScreenshots` (explicit) | Captures 19 pages at 1280 and 390 pixels, and at 1280 pixels in dark mode, with an overflow report in `artifacts/ui/<label>` (`STOCKROOM_UI_LABEL`) for interface review | None |
| `DemoRecording` (explicit) | Portfolio walkthrough with rejection, receipt, withdrawal, refused withdrawal, history, and restart; writes README screenshots to `docs/images` and video to `artifacts/demo` | T-03, T-06, T-08, T-10, T-13 |

## Commands

Build the solution first, because the fixture runs the web application's build output for the same configuration. Install Chromium once per machine; Linux CI uses `install --with-deps chromium`.

```powershell
dotnet build Stockroom.slnx --configuration Release --no-restore
pwsh tests/e2e/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test --project tests/e2e/Stockroom.E2ETests.csproj --configuration Release --no-build
```

Browser installation downloads executables. The NuGet audit covers the Playwright package, not those browser binaries.

Current status: eleven cases pass locally on Windows with Chromium; the explicit `DemoRecording` case also passed on 2026-10-04 and produced the README screenshots. Normal runs and CI skip explicit cases. The hosted Ubuntu job passed on `main` at the M8 merge in run 37184964723. Run one explicit case with `dotnet test --project tests/e2e/Stockroom.E2ETests.csproj --configuration Release --no-build -- --explicit only --filter-class Stockroom.E2ETests.DemoRecording` (or `Stockroom.E2ETests.UiScreenshots`).
