# Security review (M7)

## Scope and environment

| Item | Value |
| --- | --- |
| Audited commit | `b28f1a1534d6e59ff362e7002e82aef3d94b5ae3` ("M6 Completed") on `feat/purchase-workflow`. `origin/main` at `2eb4ee7` has an identical tree. |
| Date | 2026-10-04 |
| Environment | Windows 11, .NET SDK 10.0.401, ASP.NET Core and .NET runtime 10.0.12, PowerShell 7.6.6, Playwright 1.63.0 with Chromium 153.0.8010.12 |
| Application scope | Authentication, authorization, inputs, browser security, database and filesystem behavior, resource abuse, secrets, configuration, dependencies, GitHub Actions, and repository settings visible without credentials |
| Method | Manual code review, automated dependency and secret checks, and bounded probes against isolated local application instances with synthetic data and generated credentials |
| Not in scope | Hosted deployments (none exist), third-party systems, and the owner's local databases and user secrets, which the review did not read or change |

The application is a local demonstration with synthetic data. Severity ratings describe the risk in that context; deployment-dependent concerns state when they would increase.

## Threat model

| Element | Details |
| --- | --- |
| Assets | Purchase requests and decisions, stock quantities and movement history, account credentials and session cookies, demo seed passwords, the SQLite database file, and the integrity of the `main` branch and its CI gate |
| Actors | Anonymous visitor, Member, Manager, a member removed from a role while holding a cookie, a same-site or cross-site web page, a pull-request author, and a supply-chain publisher |
| Entry points | `/Account/Login` and `/Account/Logout`; Razor Pages for inventory, requests, review, issue, and history; static assets; the `seed` and `seed --reset` command line; CI triggers (`push`, `pull_request`, `workflow_dispatch`) |
| Trust boundaries | Browser to Kestrel (HTTP form posts with cookies and antiforgery tokens); page handler to service (roles re-read from the database); service to SQLite (transactions and constraints); developer machine to command line (Development-only seed and reset); GitHub to runner (read-only token, public artifacts) |

## Tools, commands, and results

| Check | Command or source | Result |
| --- | --- | --- |
| Vulnerable NuGet packages | `dotnet package list --project Stockroom.slnx --include-transitive --vulnerable --no-restore` (audit source `https://data.nuget.org/v3/index.json`) | No vulnerable packages in any of the four projects |
| Deprecated and outdated packages | `dotnet package list ... --deprecated` and `--outdated` | None deprecated; no newer versions |
| Locked restore with audit | `dotnet restore Stockroom.slnx --locked-mode`; `Directory.Build.props` treats NU1900 to NU1905 as errors | Passed |
| .NET SDK and runtime | Microsoft release metadata `https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json` | 10.0.12 (2026-09-08, security release) and SDK 10.0.401 are the latest .NET 10 patch; support ends 2028-11-14 |
| GitHub Advisory Database | `GET https://api.github.com/advisories?ecosystem=...&affects=...` for every direct package, Playwright, SQLitePCLRaw, `dotnet-ef`, and each action | No advisory affects the resolved versions (details under Dependencies) |
| Bundled SQLite engine | `select sqlite_version()` through Microsoft.Data.Sqlite | 3.53.3 |
| Secret scan | `git grep` over tracked files and `git log --all -p -G` over history with credential, key, token, and connection-string patterns; file-name scan of all history | No credentials. Three expected matches (test-only fixture constants and a documented weak-password example) and four agent-instruction files in early history (F-15, F-16) |
| Repository settings | Unauthenticated public API: repository, `branches/main`, `rules/branches/main`, `private-vulnerability-reporting`, workflow runs, jobs, and artifact metadata | `main` unprotected with no rulesets; private vulnerability reporting disabled; hosted CI run 37176807154 on the audited commit passed all seven jobs |
| Behavior probes | Temporary xUnit and Playwright probes in a disposable clone of the audited commit (not added to the repository) | 18 HTTP and configuration probes and one browser framing probe; results cited as P01 to P18 and F01 below |
| Existing suites | 141 integration and 7 browser tests at the audited commit (M6 local run and hosted CI) | Passing; they cover authorization, ownership, validation, transactions, competing writes, rollback, seed, reset, and error pages |

### Coverage gaps

- Repository settings that need administrator access were not verified: Dependabot alerts, secret scanning and push protection, Actions defaults for fork pull requests and token permissions, and environments. The GitHub CLI is not installed, and the review used no credentials.
- The secret scan used regular expressions, not a dedicated scanner, and may miss unusual formats.
- No static-analysis tool such as CodeQL ran; code review was manual.
- Hosted artifact contents were not downloaded. The review inspected names and sizes only; the passing E2E artifact is 1,832 bytes, consistent with a TRX report without failure diagnostics.
- Cross-site framing could not be reproduced locally: Chromium blocked frames from non-local origins to `127.0.0.1` with `ERR_BLOCKED_BY_LOCAL_NETWORK_ACCESS_CHECKS`. F-04 rests on header evidence and a same-origin control.
- Load testing was limited to bounded probes; no fuzzing was performed.
- Browser binaries were identified by version only; Chromium is a test dependency and was not separately scanned.

## Findings

Severity: Medium, Low, or Informational. No High or Critical issue was found. "Confirmed" means a probe reproduced the behavior at the audited commit.

| ID | Severity | Status | Title |
| --- | --- | --- | --- |
| F-01 | Medium | Confirmed | `main` has no branch protection or ruleset, so the CI gate can be bypassed |
| F-02 | Low (Medium if hosted) | Confirmed | Logout does not revoke a copied session cookie, which renews while in use |
| F-03 | Low | Confirmed | An anonymous caller can lock any account for five minutes |
| F-04 | Low | Confirmed | Authenticated pages send no anti-framing header |
| F-05 | Low | Confirmed | Host header reflected in login redirects |
| F-06 | Low | Confirmed | Login response time reveals whether an account exists |
| F-07 | Low | Confirmed | Lists and history are unbounded, and request creation is unlimited |
| F-08 | Informational | Confirmed | A demoted manager's cookie still renders manager page shells |
| F-09 | Informational | Confirmed | Security headers and HTTPS controls are absent |
| F-10 | Informational | Confirmed | Default password policy and cookie lifetime |
| F-11 | Informational | Confirmed | GitHub Actions pinned to mutable tags |
| F-12 | Informational | Confirmed | Private vulnerability reporting disabled |
| F-13 | Informational | Confirmed | Failure diagnostics in public CI artifacts |
| F-14 | Informational | Confirmed | TRACE and OPTIONS return 200 |
| F-15 | Informational | Confirmed | Agent-instruction files remain in public Git history |
| F-16 | Informational | Confirmed | Fixed test-fixture passwords in tracked test code |

### F-01: `main` has no branch protection or ruleset

- **Affected:** repository settings; `.github/workflows/ci.yml:183-205` (`CI Status`).
- **Prerequisites:** write access to the repository.
- **Evidence:** `GET /repos/AdityaJadhav17/Stockroom/branches/main` returned `protected: false`; `GET /rules/branches/main` returned an empty list.
- **Impact:** the `CI Status` job reports results but does not block merges or direct pushes. A failing build, failed audit, or changed workflow can reach `main`. The repository is public, and the README presents CI as the release control.
- **Fix:** add a ruleset for `main` that requires pull requests and the `CI Status` check, blocks force pushes and deletion, and applies to administrators. Consider a CODEOWNERS entry for `.github/workflows/`.

### F-02: Logout does not revoke a copied session cookie

- **Affected:** `src/Stockroom.Web/Pages/Account/Logout.cshtml.cs:14`; Identity cookie defaults from `src/Stockroom.Web/Program.cs:13`.
- **Prerequisites:** an attacker obtains the authentication cookie, for example from a shared machine, a browser extension, or an unencrypted HTTP connection.
- **Evidence (P02):** after logout, the original client was redirected to login (302), but a copy of the pre-logout cookie still received the dashboard (200). P17 shows a 14-day sliding expiration and a 30-minute security-stamp validation interval.
- **Impact:** `SignOutAsync` only deletes the cookie in the user's browser. Cookie authentication keeps no server-side session, so a copied cookie stays valid after logout. The 14 days is a sliding idle window, not an absolute limit: each request made in the second half of the window reissues the cookie with a new expiry. Codex's independent review renewed a copied session on day 8 with a test clock and used it on day 16. A copied cookie that keeps being used therefore has no fixed end; only an idle gap longer than the window, or a security-stamp change detected by validation, ends it.
- **Fix:** call `UserManager.UpdateSecurityStampAsync` during logout and validate the stamp often enough for revocation to matter; shorten `ExpireTimeSpan`. Stamp rotation signs the account out of every session, not only the current browser, and takes effect only when validation next runs. Use a server-side ticket store or an absolute session limit if a session must end at a fixed time regardless of activity.

### F-03: Account lockout can be triggered anonymously

- **Affected:** `src/Stockroom.Web/Pages/Account/Login.cshtml.cs:37-38` (`lockoutOnFailure: true`) with default lockout options.
- **Prerequisites:** knowledge of an account email. Demo emails are documented and predictable.
- **Evidence (P08):** five failed attempts from one client blocked the manager's correct password from another client (200 with the neutral error). P17: five attempts and a five-minute lockout.
- **Impact:** an unauthenticated caller can repeatedly lock the only Manager out of approvals, receipts, and issues. No IP or client throttling exists.
- **Fix:** add the built-in ASP.NET Core rate limiter (`Microsoft.AspNetCore.RateLimiting`, no new package) to the login POST, partitioned by client address. Keep lockout as a second control and document the trade-off.

### F-04: Authenticated pages send no anti-framing header

- **Affected:** `src/Stockroom.Web/Program.cs:39-45` (pipeline without header middleware).
- **Prerequisites:** a page on the same site, or a browser that sends the cookie in frames, plus a user who clicks inside the frame.
- **Evidence (P13, F01):** `/`, `/Requests/Details/{id}`, `/Inventory/Issue/{id}`, and `/History` returned no `X-Frame-Options` and no `Content-Security-Policy`. The antiforgery system added `X-Frame-Options: SAMEORIGIN` only to the anonymous login response. A same-origin frame loaded an authenticated request page with a usable Approve button. Cross-site frames were blocked by Chromium's local-network checks, so the cross-site case remains unverified.
- **Impact:** clickjacking of Approve, Reject, Receive, and Issue buttons from a same-site page. For cross-site pages, `SameSite=Lax` keeps the authentication cookie out of the frame, so they reach the login page, which blocks framing. The risk increases if the application shares a site with untrusted content.
- **Fix:** send `Content-Security-Policy: frame-ancestors 'none'` and `X-Frame-Options: DENY` on every response.

### F-05: Host header reflected in login redirects

- **Affected:** `src/Stockroom.Web/appsettings.json:12` (`"AllowedHosts": "*"`).
- **Evidence (P15, P11):** an unauthenticated request with `Host: attacker.example` received `Location: http://attacker.example/Account/Login?ReturnUrl=%2FHistory`. Requests with that host were accepted (200).
- **Impact:** limited locally, because the response returns to the same requester. Behind a shared cache or a proxy that trusts the Host header, it could enable cache poisoning or redirection.
- **Fix:** set `AllowedHosts` to the deployed host names, and configure forwarded headers only for a known proxy.

### F-06: Login timing reveals account existence

- **Affected:** `src/Stockroom.Web/Pages/Account/Login.cshtml.cs:37`.
- **Evidence (P12):** wrong passwords for an existing account took 40 to 52 ms; unknown accounts took 1 to 2 ms. Each measurement includes the token GET.
- **Impact:** an attacker can confirm valid emails despite the neutral message. Demo emails are already public, so the local impact is minimal.
- **Fix:** rate limiting (F-03) reduces how many accounts an attacker can test per minute but does not remove the timing difference. For a hosted release, run a constant-cost password check when the user does not exist.

### F-07: Unbounded lists and unlimited request creation

- **Affected:** `src/Stockroom.Web/Services/PurchaseService.cs:50` and `:197`; `src/Stockroom.Web/Services/StockService.cs:60`; `src/Stockroom.Web/Pages/Index.cshtml.cs`; `src/Stockroom.Web/Pages/History/Index.cshtml.cs`.
- **Evidence (P09):** one member created 300 requests with 450-character reasons and no limit. The manager's history page grew to 263 KiB and 312 rows, and the requests page to 106 KiB. Responses stayed under 100 ms at that size.
- **Impact:** a member can grow every manager page and the database without bound, degrading responses over time.
- **Fix:** page history and request lists, cap the dashboard's pending list, and limit request creation per member per period.

### F-08: Demoted manager's cookie still renders manager page shells

- **Affected:** `src/Stockroom.Web/Pages/Inventory/Issue.cshtml.cs:13`, `src/Stockroom.Web/Pages/Requests/Review.cshtml.cs:11`, and `src/Stockroom.Web/Pages/History/Index.cshtml.cs:19` (role from cookie claims).
- **Evidence (P01):** after removing the Manager role in the database, the old cookie still opened the issue page (200), showed Issue links and the "Stock movements" heading. Every action and data query was refused: approve and issue showed the forbidden message, history showed zero movements, the request became 404, and data was unchanged. Updating the security stamp did not end the session until the 30-minute interval elapsed.
- **Impact:** confusing UI for up to 30 minutes; no unauthorized data or action, because services read roles from the database.
- **Fix:** update the security stamp whenever roles change, and shorten the validation interval (see F-02).

### F-09: Security headers and HTTPS controls are absent

- **Affected:** `src/Stockroom.Web/Program.cs`; `src/Stockroom.Web/Properties/launchSettings.json`.
- **Evidence (P07, P17):** no `X-Content-Type-Options`, `Content-Security-Policy`, or `Referrer-Policy`; no HTTPS redirection or HSTS. The authentication cookie uses `SecurePolicy=SameAsRequest`, and the antiforgery cookie uses `SecurePolicy=None`, so neither carries `Secure` over HTTP and the antiforgery cookie would lack it even over HTTPS.
- **Impact:** none for loopback HTTP. A hosted release over HTTP would expose cookies to network attackers.
- **Fix:** before hosting, require HTTPS with HSTS, set both cookies to `CookieSecurePolicy.Always`, and add `nosniff`, a restrictive CSP (the site uses no scripts), and `Referrer-Policy: same-origin`.

### F-10: Default password policy and cookie lifetime

- **Evidence (P16, P17):** passwords use the Identity defaults: six characters with character classes. Hashes use PBKDF2-HMAC-SHA512 with 100,000 iterations and a 16-byte salt. Authentication cookies last 14 days with sliding expiration.
- **Impact:** acceptable for seeded demo accounts; weak for real users.
- **Fix:** for a hosted release, require at least 12 characters, consider breached-password screening, and shorten the cookie lifetime.

### F-11: GitHub Actions pinned to mutable tags

- **Affected:** `.github/workflows/ci.yml:33, 47, 52, 68, 73, 96, 101, 124, 140, 145, 176` (`actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`).
- **Evidence:** major-version tags; the GitHub Advisory Database lists no advisory for these actions.
- **Impact:** a compromised or retagged action release would run in CI. The workflow has `contents: read`, uses no secrets, and sets `persist-credentials: false`, which limits the impact.
- **Fix:** pin each action to a full commit SHA with a version comment. The existing Dependabot `github-actions` configuration can update the SHAs.

### F-12: Private vulnerability reporting disabled

- **Evidence:** `GET /repos/AdityaJadhav17/Stockroom/private-vulnerability-reporting` returned `enabled: false`. `SECURITY.md` directs reporters to email and says the feature will follow publication.
- **Fix:** enable private vulnerability reporting, and update `SECURITY.md`, whose project-status paragraph predates the application.

### F-13: Failure diagnostics in public CI artifacts

- **Affected:** `.github/workflows/ci.yml:137, 176-181`; `tests/e2e/BrowserTest.cs:32, 103`.
- **Evidence:** on a failed browser test, the job uploads Playwright traces (screenshots, DOM snapshots, network activity), screenshots, and the server log for 14 days. Public-repository artifacts can be downloaded by any signed-in GitHub user. The traces can contain the fixture's generated passwords and session cookies.
- **Impact:** low. The credentials are random per test and valid only for a temporary local application that no longer exists. The concern is future reuse of this path with real secrets.
- **Fix:** keep test credentials generated and ephemeral; never inject repository secrets into the E2E job. Consider shorter retention for diagnostics.

### F-14: TRACE and OPTIONS return 200

- **Evidence (P11, P14):** `TRACE /Requests/Review/1?handler=Approve` returned the rendered page (200) without echoing request headers or cookie names; `OPTIONS` returned an empty 200. `PUT`, `DELETE`, and `PATCH` returned 400.
- **Impact:** none observed; the response does not reflect the request.
- **Fix:** optional. Reject methods other than GET, HEAD, and POST at the edge or in middleware.

### F-15: Agent-instruction files in public Git history

- **Affected:** commits `13c4591` (added) and `bf8c7ee` (deleted): `AGENTS.md`, `CLAUDE.md`, `docs/claude-handoff.md`, `docs/writing-guide.md`.
- **Evidence:** the files are no longer tracked and are ignored, but the pushed history contains them. Scanning their historical content found no credential patterns.
- **Impact:** disclosure of working instructions only.
- **Decision for the owner:** leaving history unchanged is recommended. Rewriting it would require a force push and would invalidate existing clones and pull-request references.

### F-16: Fixed test-fixture passwords in tracked test code

- **Affected:** `tests/integration/StockroomFactory.cs:22-23` (two constants, values not reproduced here).
- **Impact:** none for the application. The integration fixture uses them only against temporary databases. The E2E fixture generates random passwords.
- **Fix:** never reuse these values for real or demonstration accounts; generate demo passwords as `docs/development.md` describes.

## Controls verified

| Area | Result | Evidence |
| --- | --- | --- |
| SQL injection | No raw SQL in the application; LINQ queries are parameterized, including inventory search. Raw SQL appears only in tests with constant strings. | Code review |
| Stored and reflected XSS | Request reasons, rejection notes, movement reasons, and the search term render encoded on every page tested. The `HtmlString` helpers contain only formatted dates and, since M8, status badges built from enum names. | P04 |
| Antiforgery | Every state-changing POST, including login and logout, returned 400 without a token and changed no data. | P05 |
| Open redirect | `//`, `/\`, and absolute return URLs redirect to `/`; encoded slashes stay same-origin. | P03 |
| Overposting | Extra fields for status, requester, and reviewer were ignored; the new request was Pending and owned by the caller. | P11 |
| Authorization | Roles are enforced on pages, re-read from the database in services, and dual-role accounts cannot create purchases. Ownership hides other members' requests (404). | Existing suites, P01 |
| Transactions and integrity | Conditional writes, competing receipts and issues, overflow guards, rollback after forced failures, and controlled database errors. | Existing suites |
| Static exposure | The database file, configuration, source, build output, `.git`, and project files return 404; only `wwwroot` assets are served. | P06 |
| Input size | A 1,000,000-character reason fails validation without creating a record; a 5,000,000-character form value returns 400. A 1,000,000-character password costs about the same as a short one. | P10 |
| Password storage | PBKDF2-HMAC-SHA512, 100,000 iterations, 16-byte salt | P16 |
| Seed and reset | Development-only; passwords validated before deletion; target validation, refusal of unsupported or locked targets, and pool isolation are tested. Neither command is reachable over HTTP. | Existing suites |
| Error pages | No exception details in responses; details go to the server log. | Existing suites |
| CI workflow | `contents: read`; `pull_request` rather than `pull_request_target`; no secrets; no untrusted event text in `run` steps; `persist-credentials: false`; the gate job fails on any non-success dependency. | Code review, run 37176807154 |

## Dependencies

| Component | Version | Use | Advisory status |
| --- | --- | --- | --- |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.12 | Application | None |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | Application | None |
| SQLitePCLRaw.lib.e_sqlite3 (transitive) | 2.1.12, SQLite 3.53.3 | Application | GHSA-2m69-gcr7-jv3q (CVE-2025-6965) affects 2.1.11 and earlier; not applicable |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | Build only (`PrivateAssets=all`) | None |
| ASP.NET Core and .NET runtime | 10.0.12 | Application | Latest .NET 10 security release |
| .NET SDK | 10.0.401 | Build | Latest; CI rolls forward to the latest 10.0.4xx patch |
| dotnet-ef (local tool) | 10.0.12 | Development | None; outside the NuGet audit graph |
| xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing | 4.0.1, 4.0.0, 18.10.1, 10.0.12 | Test | None |
| Microsoft.Playwright.Xunit.v3 and Microsoft.Playwright | 1.63.0 | Test | None for NuGet. GHSA-7mvr-c777-76hp (CVE-2025-59288, browser download without certificate validation) affects npm `playwright` before 1.55.1; the bundled driver is 1.63.0, so it does not apply |
| Chromium (Playwright browser) | 153.0.8010.12 | Test | Not scanned; downloaded by the Playwright installer |
| actions/checkout, actions/setup-dotnet, actions/upload-artifact | v7, v6, v7 tags | CI | None; see F-11 |
| Bundled frontend assets | None | Application | `wwwroot` contains only `css/site.css`; no JavaScript libraries |

## Accepted limitations

- **Non-atomic demo reset (ADR 0005):** `seed --reset` deletes the database and then reseeds it. A failure after the password and target checks pass, such as a full disk, would leave an empty local database until the next reset. The command is Development-only, cannot run over HTTP, and affects only local synthetic data. It is not a security boundary.
- **Local HTTP:** the application targets a local demonstration without TLS (see F-09).
- **Single application and database:** SQLite file permissions, backups, and access control depend on the host account.

## Deployment-dependent concerns (unverified)

- HTTPS termination, HSTS, and secure cookies (F-09).
- `AllowedHosts` and forwarded-header configuration behind a proxy (F-05).
- Data Protection key storage. Without configuration, keys live in the user profile (`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`, protected by DPAPI on Windows). Containers or multiple instances need persistent, protected keys, or cookies break or keys are exposed.
- Secret storage. User secrets are plaintext JSON for development only; a hosted deployment needs a secret store or protected environment variables.
- Log storage and retention. Database errors log exception details server-side.
- Database file location, permissions, and backups.
- Actions approval defaults for fork pull requests and default token permissions remain unverified. The owner confirmed the dependency graph, Dependabot alerts, secret scanning, and push protection on 2026-10-05; see [GitHub repository setup](../development/github-repository-setup.md).

## Remediation plan

| Priority | Findings | Action | When |
| --- | --- | --- | --- |
| 1 | F-01, F-12 | Add a `main` ruleset requiring pull requests and `CI Status`, blocking force pushes and deletion; enable private vulnerability reporting. Settings only; no code change. | Before calling the repository release-ready |
| 2 | F-04, F-09 (headers) | Add one middleware that sets `frame-ancestors 'none'`, `X-Frame-Options: DENY`, `nosniff`, and `Referrer-Policy`, with a test for each header. | Before release |
| 3 | F-02, F-08 | Update the security stamp on logout and on role changes; shorten the validation interval; add replay and demotion regression tests. | Before release |
| 4 | F-03, F-06 | Rate-limit the login POST with the built-in middleware; test lockout and throttling behavior. | Before release |
| 5 | F-05, F-09 (TLS), F-10 | Configure `AllowedHosts`, HTTPS, HSTS, secure cookies, Data Protection persistence, and a stronger password policy. | Before any hosted deployment |
| 6 | F-07 | Page history and request lists; limit request creation per member. | Before hosted use with real users |
| 7 | F-11, F-13, F-14 | Pin actions to SHAs, review artifact retention, and optionally restrict HTTP methods. | Maintenance |
| 8 | F-15, F-16 | Record the owner's history decision; keep fixture passwords test-only. | Owner decision |

## Remediation status

The findings above describe commit `b28f1a1`. The owner merged the M7 fixes after independent review. The status table includes public API checks through 2026-10-05. On that date, the owner confirmed that the dependency graph, Dependabot alerts, secret scanning, and push protection are in place and working; the developer did not run authenticated API checks for those controls. [ADR 0007](../decisions/0007-m7-security-hardening.md) records the code decisions.

| ID | Status | Change and evidence |
| --- | --- | --- |
| F-01 | Open (owner) | On 2026-10-05 the public GitHub API reported `main` as unprotected (`"protected": false`) with no branch rules or rulesets, so `main` does not require pull requests or `CI Status` and does not block force pushes. |
| F-02 | Fixed with residual risk | Logout rotates the security stamp, and the stamp is validated on every request (`ValidationInterval = 0`), so a copied cookie is rejected on its next request. Idle timeout reduced to one hour. `LogoutRevokesACopiedSessionCookie` replays a pre-logout cookie and gets the login redirect. If rotation fails, logout still signs out the current browser, tells the user that other sessions may remain signed in, and logs the failure (`FailedSessionRevocationIsReportedToTheUser`). Residual: an active session still renews without an absolute limit, and logout ends every session for the account (`LogoutSignsTheAccountOutInEveryBrowser`). |
| F-03 | Mitigated | Login POSTs are limited to five per client address per one-minute sliding window (six segments) with the built-in rate limiter. Because a segment returns its permits when it leaves the window, attempts made at the end of a segment come back after 50 seconds, so one client can make 10 attempts within one window. The lockout threshold is therefore 11 (`SessionPolicy.LockoutThreshold = 2 x limit + 1`), and the 11th attempt cannot come sooner than 2 x (window - one segment), about 100 seconds after the first, assuming the account starts with no failed attempts. Tests: `LoginPostsAreThrottledBeforeTheAccountLocks` (sixth POST receives 429, account unlocked); `LoginWindowBoundaryCannotDoubleTheAttemptsAllowed` (the fixed-window boundary pattern, rejected after review); `OneClientCannotReachTheLockoutWithinTheGuaranteedInterval` (with a 2-second window, three seconds of continuous attempts accept at most 10 and do not lock the account; with a threshold of 10 it fails). Residual: the failure count persists until a successful login or a lockout, so one client can lock an account after about 100 seconds of attempts from a clean count, sooner if earlier failures remain, attackers using many addresses sooner, and clients sharing an address share the limit. |
| F-04 | Fixed | Every response sends `X-Frame-Options: DENY` and `Content-Security-Policy` with `frame-ancestors 'none'`. `EveryResponseCarriesSecurityHeaders` checks pages, static files, and error pages. Cross-site framing in a browser remains unverified locally. |
| F-05 | Open (deployment) | Set `AllowedHosts` when hosting |
| F-06 | Reduced | Throttling limits the rate of probing; the timing difference remains |
| F-07 | Open | Paging and per-member limits not implemented |
| F-08 | Fixed | Per-request validation rebuilds role claims from the database. `RoleRemovalAppliesOnTheNextRequest` shows the demoted account is denied the issue page and sees no manager links. |
| F-09 | Partly fixed | `nosniff`, `Referrer-Policy: same-origin`, and a `default-src 'self'` CSP added; the antiforgery cookie is marked `Secure` over HTTPS (`AntiforgeryCookieIsSecureOverHttps`). HTTPS, HSTS, and an always-secure authentication cookie remain deployment tasks. |
| F-10 to F-11 | Unchanged | See the remediation plan |
| F-12 | Fixed (owner) | On 2026-10-04 `GET /repos/AdityaJadhav17/Stockroom/private-vulnerability-reporting` returned `enabled: true`. M9 updated `SECURITY.md` to name the Security tab. |
| F-13 to F-16 | Unchanged | See the remediation plan |

### Regression found while reviewing the fixes

Per-request stamp validation added a database read to authentication. When that read failed, the re-executed `/Error` request failed in authentication too, the exception handler rethrew the original exception, and in Development the framework's developer exception page showed the exception details. A last-resort handler now sits outside the exception handler: it logs the exception and returns a fixed 500 page that needs no database, authentication, or Razor rendering, and the security headers wrap it. `DatabaseFailureDuringAuthenticationShowsOnlyTheGenericErrorPage` renames the users table and checks that an authenticated request receives only the generic page.

Verification of the fixes: 156 integration tests and 7 browser tests passed locally on Windows. With each fix removed in turn, its regression tests failed: all 11 initial tests, the generic-page test, the revocation-failure test, the window-boundary test (all ten attempts accepted under a fixed window), and the lockout-interval test (ten attempts accepted within three seconds with a threshold of 10). The two timing tests passed five consecutive runs. The owner reports that hosted CI passed for the merged M7 changes.

### CSP observation during M8

While checking the M8 interface for CSP errors, the browser console showed that the empty validation summary on the login, request, and issue forms carried an inline `style="display:none"` from the ASP.NET Core tag helper, which the policy blocked. The block had no visible or security effect, because the summary was already hidden, but it logged a violation on each form load. M8 renders the summary only when the form has errors. `ResponsiveLayoutTests` now fails on any console error on the main pages, including CSP violations. The policy is unchanged.

## Remaining risks

No confirmed vulnerability allows an anonymous or Member account to read another member's data or perform a manager action. After the fixes, the main residual risks are the unprotected `main` branch (F-01), sessions that renew without an absolute limit while in use (F-02), lockout by distributed attackers (F-03), and unbounded lists (F-07). Cross-site framing in a browser, Actions approval defaults for fork pull requests, and default token permissions remain unverified. The owner confirmed the dependency graph, Dependabot alerts, secret scanning, and push protection on 2026-10-05.

## References

- ASP.NET Core Identity configuration: <https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration>
- Security stamp validation: <https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions.validationinterval>
- Antiforgery in ASP.NET Core: <https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery>
- Rate limiting middleware: <https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit>
- Host filtering: <https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering>
- HTTPS and HSTS: <https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl>
- Data Protection key storage: <https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings>
- OWASP clickjacking defense: <https://cheatsheetseries.owasp.org/cheatsheets/Clickjacking_Defense_Cheat_Sheet.html>
- GitHub Actions security hardening: <https://docs.github.com/en/actions/security-for-github-actions/security-guides/security-hardening-for-github-actions>
- GitHub rulesets: <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets>
- Private vulnerability reporting: <https://docs.github.com/en/code-security/security-advisories/working-with-repository-security-advisories/configuring-private-vulnerability-reporting-for-a-repository>
- NIST SP 800-63B password guidance: <https://pages.nist.gov/800-63-4/sp800-63b.html>
- Advisories: <https://github.com/advisories/GHSA-2m69-gcr7-jv3q> and <https://github.com/advisories/GHSA-7mvr-c777-76hp>
- .NET 10 release metadata: <https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json>
