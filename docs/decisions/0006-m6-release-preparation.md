# ADR 0006: M6 error handling and interface polish

Date: 2026-10-04

Status: Accepted. Implemented in M6.

## Context

Before M6, a database failure other than lock contention reached the user as an unhandled exception. In Development, the developer exception page showed the stack trace and SQL details. Missing pages and rejected antiforgery tokens returned empty responses. Quantities displayed singular units ("5 spool").

## Decision

| Area | Decision |
| --- | --- |
| Service database errors | `ServiceGuard.GuardAsync` catches `DbException` and `DbUpdateException` after the lock-contention case. It clears the change tracker, logs the exception at error level, and returns `DatabaseErrorMessage`. The transaction inside the operation rolls back when it is disposed. |
| Message wording | `DatabaseErrorMessage` does not claim that nothing changed, because a failure during commit can leave the outcome unknown. It asks the user to check the record before retrying. Validation, permission, conflict, and lock messages keep "Nothing was changed", which holds because those paths stop before commit. |
| Error page | `UseExceptionHandler("/Error")` and `UseStatusCodePagesWithReExecute("/Error")` run in every environment, including Development, where the demonstration runs. `/Error` shows a heading and next step for 400, 404, and 500 responses without exception details. The exception handler middleware logs unhandled exceptions. Developers read details in the server log. |
| Not found | A member who opens another member's request receives the same 404 page as for a missing request. |
| Units | `DisplayFormat.Quantity` and `DisplayFormat.Change` pluralize the seeded units ("1 spool", "5 spools", "2 boxes", "-2 spools") in pages and service messages. |
| Accessibility | Validation summaries list field errors as well as form errors in a `role="alert"` region. Invalid fields carry `aria-invalid="true"` and a visible error border. Tables take their accessible names from the adjacent heading or a visually hidden caption. The navigation marks the current section with `aria-current="page"`. Table timestamps use `<time>` elements and stay on one line. |
| Demonstration | `DemoRecording` is an explicit Playwright test. It runs the walkthrough against a fresh fixture database, writes README screenshots to `docs/images`, and records video to the ignored `artifacts/demo` directory. Normal test runs and CI skip it. |

## Alternatives

| Option | Reason for rejection |
| --- | --- |
| Keep the developer exception page in Development | The demonstration runs in Development, and the page exposes stack traces and SQL to viewers. |
| Report "Nothing was changed" for every database error | A failed commit does not guarantee a rollback, so the claim could be false. |
| Catch every exception in the services | Programming errors would be hidden as database errors; the error page handles them instead. |
| Client-side validation scripts | Server-rendered errors with an alert summary meet the requirement without JavaScript. |

## Consequences

Developers no longer see stack traces in the browser and must read the console log. A database error during commit tells the user to check the record, which can require a reload. Seeded units are regular English nouns; a future irregular unit would need its own plural form.
