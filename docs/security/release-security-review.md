# Release security review (v1.0.0 candidate)

## Summary

This review examined the v1.0.0 release candidate on 2026-10-05 and builds on the [M7 security review](security-review.md). It found no confirmed vulnerability. Anonymous visitors and Member accounts could not perform a manager action or read another member's records in any of the 20 live probes, the bounded fuzz, or the 156 integration and 11 browser tests. The review closed three M7 coverage gaps: it inspected the contents of CI failure diagnostics, verified cross-site framing protection in a browser, and ran a bounded input fuzz.

The [Remediation status](#remediation-status) section records the security close-out that followed: R-01 to R-04 are fixed and verified locally, hosted CI has not yet run the fixes, a dedicated secret scan found no secret, and F-01 remains open.

Recommendation: **ready with explicitly accepted limitations** (see [Release recommendation](#release-recommendation)). The application code has no release blocker. The owner should either add the `main` ruleset (F-01) before tagging or record acceptance of that open control, and accept the local-demonstration limitations listed below. A hosted deployment remains out of scope until the deployment requirements are met.

## Scope and baseline

| Item | Value |
| --- | --- |
| Reviewed commit | `6082df1bd05f5f35775b4b498b4b428a8110951d` ("Refresh release evidence and prepare narrated demo") on `docs/release-closeout` |
| `main` | The owner merged pull request #12 during the review. Merge commit `10455712b7dbf5f376af9e797bdce8991c85315e` has a tree identical to `6082df1` (`git diff` empty). |
| Working tree | Clean at the start. This review adds only this report and one link in [docs/README.md](../README.md). |
| Date | 2026-10-05; baseline recorded at 18:34 UTC |
| Environment | Windows 11, .NET SDK 10.0.401, ASP.NET Core and .NET runtime 10.0.12, PowerShell 7.6.6, Git 2.52.0, Python 3.12.10 with `requests` 2.34.2 for probes |
| Changes since M7 | The M7 fixes merged as `1ef4c9a`. Since then, application code changed only in M8 (`2262348`): page markup and `site.css`, the `DisplayFormat.Badge` helper, the dashboard's approved-request list (`Pages/Index.cshtml.cs`), and the last-resort page markup. M9 (`0f30ae2`) changed `scripts/Test-Repository.ps1` and test documentation. Later commits (`9e33555` to `6082df1`) changed documentation, added the diagram viewers under `docs/diagrams/`, and added two Git settings: a `.gitattributes` rule for the generated diagram files and a `.gitignore` entry for the local `.archify/` folder. |
| Uncommitted scope | This report and the `docs/README.md` link. No uncommitted application change exists. |
| Data handling | Every probe and test run used a clone of the reviewed commit outside the repository, a new SQLite file, generated passwords passed as environment variables, and an empty `APPDATA`, so no process read the owner's user secrets or databases. The instances used the Data Protection key ring in the Windows user profile, as every local run of the application does, and added no key to it. Raw output is in the ignored `artifacts/security-review/` directory. |

## Tools, commands, and results

| Check | Tool and version | Command or source | Result |
| --- | --- | --- | --- |
| Repository check | `scripts/Test-Repository.ps1`, PowerShell 7.6.6 | `pwsh -NoProfile -File scripts/Test-Repository.ps1` | Passed: 57 required files, 136 text files |
| NuGet audit | .NET SDK 10.0.401 | `scripts/Test-Dependencies.ps1`; `dotnet package list --include-transitive --vulnerable` | 4 projects, 0 vulnerable packages |
| Outdated and deprecated packages | .NET SDK 10.0.401 | `dotnet package list --include-transitive --outdated` and `--deprecated` | No direct package has a newer version; 25 transitive packages do; 0 deprecated |
| Advisory database | GitHub Advisory Database API, 2026-10-05 18:38 UTC | `GET /advisories?ecosystem=nuget&affects=<94 resolved package@version pairs>`; `ecosystem=actions` for the three actions; `ecosystem=npm` for `playwright@1.63.0` | 0 advisories. Positive controls returned GHSA-2m69-gcr7-jv3q for `SQLitePCLRaw.lib.e_sqlite3@2.1.11` and GHSA-7mvr-c777-76hp for `playwright@1.55.0`, so the filters work. |
| .NET release metadata | Microsoft | `https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json` | 10.0.12 (2026-09-08, security release) and SDK 10.0.401 are the latest .NET 10 releases; support ends 2028-11-14 |
| SQLite engine | `dotnet fsi` with the application's `Microsoft.Data.Sqlite` build output, in-memory database | `select sqlite_version(), sqlite_source_id()` | 3.53.3 (source 2026-06-26) |
| SQLite advisories | sqlite.org | `https://www.sqlite.org/cves.html`; release log for 3.53.4 | Latest CVE fix listed is 3.53.2 (CVE-2026-11822, CVE-2026-11824). 3.53.4 (2026-07-24) exists; the CVE table lists no fix after 3.53.2. |
| Release build | .NET SDK 10.0.401 | `dotnet build Stockroom.slnx --configuration Release --no-restore --warnaserror` | 0 warnings, 0 errors |
| Integration tests | xUnit v3 on Microsoft.Testing.Platform | `dotnet test --project tests/integration/... --report-xunit-trx` (CI options) | 156 total, 156 passed, 0 skipped |
| Browser tests | Playwright 1.63.0, Chromium 153.0.8010.12 | `dotnet test --project tests/e2e/... --report-xunit-trx` | 13 total, 11 passed, 0 failed, 2 skipped (the explicit `DemoRecording` and `UiScreenshots` cases) |
| Live probes | Python script against the reviewed Release build | 20 probes, P01 to P20, below | 20 passed |
| Bounded fuzz | Python, fixed seed 20261005 | Request form, rejection form, search, and route parameters | 170 requests; no 5xx response, no error detail, no unencoded markup |
| Cross-site framing | Browser pane, Chromium 152.0.7977.130 | A page on `http://localhost:8765` framing `http://127.0.0.1:5199` | Both frames blocked by `frame-ancestors 'none'` |
| Local static analysis | Roslyn 5.9.0 with the SDK's .NET analyzers | `dotnet build ... -p:AnalysisModeSecurity=All -p:AnalysisLevel=latest`, SARIF for the web project | Web project: 0 warnings. Solution: 1 CA2100 warning in test code (see [Static analysis](#static-analysis)). |
| CodeQL | GitHub default setup | Public Actions and check-run APIs | `Analyze (csharp)` and `Analyze (actions)` passed at `6082df1` and `1045571`; alert totals need authentication |
| Hosted CI | GitHub Actions | Public Actions API | Run 37357178899 passed all seven jobs at `1045571`; run 37355421207 passed at `6082df1` |
| Secret scan | No dedicated scanner installed (Gitleaks, TruffleHog, detect-secrets, ggshield, git-secrets checked) | Supplementary regular-expression sweep of the HEAD tree and every blob reachable from all refs | No usable credential (see [Secrets](#secrets)) |
| CI diagnostics | Controlled browser-test failure in the isolated clone | `Assert.Fail` added to one test, then restored | Trace contains the generated password and session cookies (R-01) |
| GitHub settings | Public GitHub API without credentials | Repository, branch, rules, rulesets, private vulnerability reporting, alert and settings endpoints | See [GitHub controls](#github-controls) |

## Findings

Severity follows the M7 scale: Medium, Low, or Informational. A finding is **confirmed** when this review reproduced or inspected the behavior at the reviewed commit. Each finding has one category:

- **Vulnerability:** a weakness an attacker can use against the release as documented.
- **Hardening:** a control below current guidance with no demonstrated exploit in the local release.
- **Deployment requirement:** a control needed before hosted use.
- **Documentation:** a missing policy or record.

### Confirmed vulnerabilities

None. No probe, test, fuzz input, or code-review path let an anonymous or Member account perform a manager action, read another member's request or history, bypass antiforgery validation, inject markup or SQL, or obtain error details.

### New findings

| ID | Severity | Category | Status | Title |
| --- | --- | --- | --- | --- |
| R-01 | Informational | Hardening | Confirmed | CI test artifacts contain test credentials |
| R-02 | Informational | Hardening; deployment requirement | Confirmed | Authentication and authorization failures are not logged |
| R-03 | Informational | Documentation | Confirmed | No documented remediation time frames for vulnerable components |
| R-04 | Informational | Hardening; deployment requirement | Confirmed | Password hashing iterations below current OWASP guidance |

### R-01: CI test artifacts contain test credentials

- **Affected:** [tests/e2e/BrowserTest.cs](../../tests/e2e/BrowserTest.cs) lines 32 (tracing starts before login) and 97 (trace saved on failure); [ci.yml](../../.github/workflows/ci.yml) lines 137 and 174 to 181 (upload with 14-day retention); [tests/integration/AuthenticationTests.cs](../../tests/integration/AuthenticationTests.cs) lines 30, 31, and 49 (fixture passwords as theory arguments).
- **Prerequisites:** a browser test fails in CI, and a signed-in GitHub user downloads the `e2e-test-results` artifact within 14 days. For the integration report, any CI run.
- **Reproduction (sanitized):** in the isolated clone, add `Assert.Fail` after the last assertion of `AccessRestrictionTests.MemberHistoryShowsOnlyTheirOwnRequests`, set `STOCKROOM_E2E_ARTIFACTS`, run that test, and open `user1-trace.zip`. Search `trace.trace` for the `fill` action on the Password field. Restore the test.
- **Evidence:** the trace held the generated member password in plain text in four action records in `trace.trace`, once in `trace.network`, and once form-encoded in the captured login POST body. `trace.network` also recorded the `.AspNetCore.Identity.Application` cookie name 16 times and `.AspNetCore.Antiforgery` 14 times: every request after login, to `/`, `/History`, and the stylesheet, carried the authentication cookie, and each response carried `Set-Cookie`. The server log and the failure screenshot contained neither. The integration TRX report, which CI uploads on every run, contains the two fixture passwords six times in theory display names such as `ValidLoginOpensDashboardWithRole(email: ..., password: ...)`.
- **Impact:** low, because the accounts are temporary. The browser-test passwords are random per test. The cookies name accounts in the fixture's temporary database, which the fixture deletes when the test ends. Each seed creates new account identifiers and security stamps (two seeds in this review shared none), and the application checks the stamp against the database on every request, so a cookie from a finished test fails validation. The low risk does not rest on the encryption keys: the fixture does not isolate the Data Protection key ring, and ASP.NET Core persists keys in the user profile by default, under `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` on Windows and `$HOME/.aspnet/DataProtection-Keys` on Linux and macOS ([Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/default-settings?view=aspnetcore-10.0)). On the review machine that folder held three keys created on 2026-02-24 and 2026-10-03, and the 2026-10-05 runs added none, so the keys outlived each test. A GitHub-hosted runner keeps its keys until GitHub discards the virtual machine. The fixture passwords already appear in tracked source (F-16) and work only against temporary test databases. This closes the M7 F-13 gap, which named this exposure without inspecting artifact contents. The risk grows if a future change passes real secrets to these jobs or reuses a test database after a test.
- **Fix:** handle the password and the cookies separately.
  - Password: start tracing after `LogInAsync` signs in, or record the login in a separate trace chunk that is not saved.
  - Cookies: tracing after login still records the authentication and antiforgery cookies on every later request. Stop uploading Playwright traces from CI and upload only the screenshot and server log, which contain neither; reproduce a trace locally when a failure needs one. If traces must stay in CI artifacts, remove `Cookie` and `Set-Cookie` headers and request bodies from `trace.network` and the stored resources before upload, and check the result.
  - General: keep test credentials generated per run, never pass repository secrets to the browser-test job, and consider a shorter `retention-days` for failure diagnostics. Replace the password arguments in `AuthenticationTests` with a role key and resolve the password inside the test.
  - Optional: to make keys end with the test, the application needs a setting that points `PersistKeysToFileSystem` at the fixture's temporary directory. The low-risk conclusion does not depend on it.

### R-02: Authentication and authorization failures are not logged

- **Affected:** [appsettings.json](../../src/Stockroom.Web/appsettings.json) lines 6 to 10 (`Microsoft.AspNetCore` at `Warning`); [Login.cshtml.cs](../../src/Stockroom.Web/Pages/Account/Login.cshtml.cs) lines 40 to 46; [Program.cs](../../src/Stockroom.Web/Program.cs) lines 38 to 45 (no rejection callback on the rate limiter).
- **Prerequisites:** none; the behavior is the default configuration.
- **Evidence:** the probe server log (174 lines) recorded startup, the expected exception entries from the database-failure probe, and the last-resort entry. It recorded nothing for five failed passwords and two throttled login POSTs (P20), the denied member POSTs to manager actions (P06), or the denied manager request creation (P07). ASP.NET Core Identity and the rate limiter log these events below `Warning`, which the configuration filters out.
- **Impact:** an operator cannot detect password guessing, throttling, or probing from the logs. For the local demonstration the impact is negligible. ASVS 5.0.0 requirements 16.3.1 and 16.3.2 (Level 2) are not met.
- **Fix:** before hosted use, log failed sign-ins and lockouts with the account and client address, rate-limit rejections through `RateLimiterOptions.OnRejected`, and authorization failures. Keep passwords and tokens out of logs, and define log retention.

### R-03: No documented remediation time frames for vulnerable components

- **Affected:** [.github/SECURITY.md](../../.github/SECURITY.md); [dependency-audit.md](dependency-audit.md).
- **Evidence:** the documents describe how to run the dependency check, but neither states how quickly the owner fixes a vulnerable package, a .NET security release, or a vulnerable GitHub Action.
- **Impact:** a reader cannot tell whether a future advisory has been handled on time. ASVS 5.0.0 requirement 15.1.1 (Level 1) is not met.
- **Fix:** add time frames to `SECURITY.md` for direct and transitive packages, the .NET runtime and SDK, and Actions, for example by advisory severity. The owner chooses the values.

### R-04: Password hashing iterations below current OWASP guidance

- **Affected:** [Program.cs](../../src/Stockroom.Web/Program.cs) lines 17 to 22 (no `PasswordHasherOptions` or `PasswordOptions`).
- **Evidence:** decoding the header of a stored hash from a throwaway database showed the ASP.NET Core Identity V3 format: PBKDF2-HMAC-SHA512, 100,000 iterations, a 16-byte salt, and a 32-byte subkey. The [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html) recommends 220,000 iterations for PBKDF2-HMAC-SHA512.
- **Impact:** an attacker who copies the database file can test guesses faster than current guidance intends. The release has only operator-seeded demonstration accounts, and the database stays on the local machine. ASVS 5.0.0 requirement 11.4.2 (Level 2) is partly met.
- **Fix:** before hosted use, set `PasswordHasherOptions.IterationCount` to at least 220,000; Identity rehashes an existing password at its next successful sign-in. Apply the F-10 password-policy changes at the same time.

## M7 findings revisited

| ID | M7 severity | Current status | Evidence from this review |
| --- | --- | --- | --- |
| F-01 | Medium | Open (owner) | At 18:36 UTC `GET /branches/main` returned `"protected": false`; `rules/branches/main` and `rulesets` returned empty lists. `main` requires no pull request or `CI Status`, blocks no force push, and has no bypass list to review. |
| F-02 | Low (Medium if hosted) | Fixed with residual risk | P13: a copied cookie received 200 before logout and a login redirect after it. `LogoutRevokesACopiedSessionCookie` passed. Residual: no absolute session lifetime (ASVS 7.3.2). |
| F-03 | Low | Mitigated | P20: five failed POSTs returned 200, the sixth and seventh 429; the failure count stayed at 5 with no lockout. The M7 residual risk is unchanged. |
| F-04 | Low | Fixed; cross-site case now verified | P12 and P15: `X-Frame-Options: DENY` and `frame-ancestors 'none'` on pages, static files, redirects, and 400, 404, 429, and 500 responses. Chromium 152 blocked cross-site frames of the login page and a request page. |
| F-05 | Low | Open (deployment) | P17: an unauthenticated request with `Host: attacker.example` received a `Location` header on that host. |
| F-06 | Low | Reduced | Not re-measured; `Login.cshtml.cs` is unchanged since M7. |
| F-07 | Low | Open | The list queries are unchanged, and M8 added a second unbounded dashboard list (approved requests awaiting delivery). |
| F-08 | Informational | Fixed | P14: after the Manager role row was deleted, the next request to the issue page redirected to access denied and the dashboard showed no Issue links; restoring the row restored access. |
| F-09 | Informational | Partly fixed (deployment) | P03: the authentication cookie carries `HttpOnly`, `SameSite=Lax`, and `path=/`, without `Secure` over HTTP; the antiforgery cookie carries `HttpOnly` and `SameSite=Strict`. P12: no HSTS. |
| F-10 | Informational | Unchanged | Identity default password rules; see R-04 for hashing parameters. |
| F-11 | Informational | Unchanged | [ci.yml](../../.github/workflows/ci.yml) still uses `actions/checkout@v7`, `actions/setup-dotnet@v6`, and `actions/upload-artifact@v7`. The advisory database lists no advisory for any of the three actions. |
| F-12 | Informational | Fixed | `GET /private-vulnerability-reporting` returned `enabled: true`. |
| F-13 | Informational | Unchanged; contents now inspected | See R-01. |
| F-14 | Informational | Unchanged | P16: `TRACE` and `OPTIONS` returned 200 without echoing a test header; `PUT`, `DELETE`, and `PATCH` returned 400. |
| F-15 | Informational | Unchanged | The four agent-instruction files remain in commits `13c4591` and `bf8c7ee`. The pattern sweep found no credential in their contents. Leaving history unchanged remains the recommendation. |
| F-16 | Informational | Unchanged | All four versions of `StockroomFactory.cs` in history, including the current one, hold the same two values. They also appear in integration test reports (R-01). |

## Application checks

### Live probes

The probes ran against the reviewed Release build in Development mode, the documented local configuration.

| ID | Check | Result |
| --- | --- | --- |
| P01 | Anonymous GET of `/`, `/Inventory`, `/Inventory/Issue/1`, `/Requests`, `/Requests/New`, `/Requests/Details/1`, `/History`, `/Account/AccessDenied` | All 302 to `/Account/Login` |
| P02 | Anonymous POST to approve request 4, with and without an anonymous antiforgery token | 302 to login; no data changed |
| P03, P04 | Login with `returnUrl` of `//evil.example/x` and `https://evil.example/x` | Both redirected to `/` |
| P05 | Member reads own requests 1 and 4, another member's requests 2 and 3, request 999, and the issue page | 200, 200, 404, 404, 404, and a redirect to access denied |
| P06 | Member POSTs with a valid member token to approve, reject, receive, and issue | All redirected to access denied; requests, quantities, movements, and events unchanged |
| P07 | Manager POST to create a purchase request (BR-02) | Redirected to access denied; no request created |
| P08 | Member dashboard and history | No link to requests 2 or 3; no stock-movement section |
| P09 | Request reason with script, image, and quote payloads; search term with an SVG payload | Encoded on the member and manager details pages, manager history, and search results; no raw markup anywhere |
| P10 | Search for `' OR 1=1 --`, `%`, `_`, and `"` | No match for each; wildcards and quotes are literal |
| P11 | Extra form fields `Status`, `RequesterId`, `ReviewerId` on request creation | Ignored; the request was Pending and owned by the caller |
| P12 | Security headers and caching | CSP, `X-Frame-Options`, `nosniff`, and `Referrer-Policy` on the dashboard, login page, static CSS, a redirect, and a 404. All 14 authenticated responses checked for both roles, including the 403 and 404 pages, send `Cache-Control: no-cache,no-store`. Responses include `Server: Kestrel`. No HSTS. |
| P13 | Copied session cookie after logout | Rejected with a login redirect |
| P14 | Manager role removed in the database | Next request denied; restored role restored access |
| P15 | 404, 400 without a token, a missing table, and a failed authentication query | Generic pages ("Page not found", "The request could not be processed", "Something went wrong" twice) with security headers and no exception text |
| P16 | `TRACE`, `OPTIONS`, `PUT`, `DELETE`, `PATCH` | 200, 200, 400, 400, 400; no response echoed the request |
| P17 | `Host: attacker.example` | Reflected in the login redirect (F-05) |
| P18 | 501-character reason, quantity 2147483648, quantity 1.5, unknown item | Form errors (200); no record created |
| P19 | `/appsettings.json`, `/Stockroom.Web.dll`, `/stockroom.db`, `/.git/config`, `/Program.cs`, build output | All 404 |
| P20 | Seven failed login POSTs from one address | 200 five times, then 429 twice with the generic "Too many attempts" page; no lockout |

### Bounded fuzz and framing

The fuzz sent 80 request-form submissions, 40 searches, and 30 route variations (`-1`, `0`, very large numbers, `abc`, `%00`, encoded slashes, and markup). Six submissions passed validation; the fuzz then opened each new request as the member and the manager, rejected each with a fuzzed reason, and loaded the manager's history and request list. Inputs included markup, template syntax, SQL fragments, bidirectional and zero-width characters, control characters, 5,000-character strings, and malformed numbers. No response was 5xx, contained exception text, or reflected unencoded markup. Malformed route values returned 400 or 404.

The framing page on `http://localhost:8765` loaded the login page and a request page from `http://127.0.0.1:5199`. The browser reported `Framing 'http://127.0.0.1:5199/' violates the following Content Security Policy directive: "frame-ancestors 'none'"` for both, and the frames stayed empty. Firefox and Safari were not tested.

### Code review

- **SQL:** the application builds every query with EF Core LINQ. No `FromSql`, `ExecuteSql`, `SqlQuery`, or `CommandText` appears in `src/`.
- **Rendered content:** Razor encodes every value. The only `HtmlString` output comes from `DisplayFormat.UtcTime` (formatted dates) and `DisplayFormat.Badge` (enum names). No page uses `Html.Raw`.
- **State-changing handlers:** every handler that changes data is a POST, and Razor Pages validates its antiforgery token. Logout by GET only redirects. Only `/Error` opts out of antiforgery, because it re-renders failed requests and changes nothing.
- **Authorization:** `AuthorizeFolder("/")` covers every page except the login and error pages. Manager pages carry `[Authorize(Roles = Manager)]`, request creation uses `RequesterPolicy`, and the services re-read roles from the database before writing. The M8 dashboard list uses `VisibleRequests`, which keeps the ownership filter.
- **Files and commands:** the web application starts no process. Only the Development `seed --reset` command deletes a file, and it accepts only the configured SQLite path.

### OWASP ASVS 5.0.0 checklist

ASVS served as a checklist, not a certification. The review considered Levels 1 and 2. Exclusions for this local demonstration:

- V4 (API), V5 (file handling), V9 (self-contained tokens), V10 (OAuth and OIDC), and V17 (WebRTC): not applicable.
- V12 (secure communication): excluded, because the release runs over local HTTP.
- 6.2.2 to 6.2.4 (password change and breached-password checks) and 6.3.3 (multi-factor authentication): outside the MVP.

| Requirement | Level | Status | Evidence |
| --- | --- | --- | --- |
| 1.2.1 Context-aware output encoding | 1 | Met | P09; fuzz |
| 1.2.4 Parameterized queries | 1 | Met | Code review; P10 |
| 1.2.5 OS command injection | 1 | Not applicable | No process execution |
| 1.3.2 No dynamic code execution | 1 | Met | The application ships no JavaScript |
| 2.1.1, 2.2.1, 2.2.2 Documented, server-side validation | 1 | Met | BR-03 and BR-10; P18; fuzz |
| 2.3.1 Steps in order | 1 | Met | Conditional transitions (BR-04); T-07, T-08 |
| 2.3.3 Transactions | 2 | Met | T-11 rollback tests |
| 2.3.4 Locking for limited quantities | 2 | Met | T-12 competing issues |
| 2.4.1 Anti-automation | 2 | Partly met | Login throttled; request creation unlimited (F-07) |
| 3.2.1 Correct rendering context | 1 | Met | `nosniff` on every response (P12) |
| 3.3.1 `Secure` attribute and cookie prefix | 1 | Excluded (deployment) | No `Secure` over HTTP; no `__Host-` prefix (P03) |
| 3.3.2 SameSite | 2 | Met | `Lax` and `Strict` (P03) |
| 3.3.4 HttpOnly | 2 | Met | P03 |
| 3.4.1 HSTS | 1 | Excluded (deployment) | Local HTTP (P12) |
| 3.4.3 Content-Security-Policy | 2 | Met | No `unsafe-inline`; browser tests fail on CSP console errors |
| 3.4.4, 3.4.5, 3.4.6 `nosniff`, referrer policy, `frame-ancestors` | 2 | Met | P12; framing check |
| 3.5.1 Cross-site request forgery | 1 | Met | Antiforgery tokens and SameSite cookies (P02, P15) |
| 3.5.3 State changes use POST | 1 | Met | Code review |
| 6.1.1 Documented anti-automation | 1 | Met | [ADR 0007](../decisions/0007-m7-security-hardening.md) |
| 6.2.1 Minimum length of 8 | 1 | Not met | Identity default minimum is 6 (F-10); operators set the seed passwords |
| 6.2.5 No composition rules | 1 | Not met | Identity default character-class rules (F-10) |
| 6.2.6, 6.2.7 Masked input; paste and password managers allowed | 1 | Met | Rendered `type="password"` with `autocomplete="current-password"` |
| 6.3.1 Brute-force controls | 1 | Met with residual risk | P20; F-03 |
| 6.3.2 No default accounts | 1 | Met | Demonstration accounts require operator-chosen passwords |
| 7.2.1, 7.2.4 Server-side session validation; new session on login | 1 | Met | Per-request stamp validation; login issues a new cookie (P03) |
| 7.3.1 Inactivity timeout | 2 | Met | One-hour sliding timeout |
| 7.3.2 Absolute session lifetime | 2 | Not met | F-02 residual |
| 7.4.1 Logout ends the session | 1 | Met | P13 |
| 7.4.2 Sessions end when an account changes | 1 | Met by design | P14 for role removal; deleted accounts fail stamp validation (not separately tested) |
| 8.1.1, 8.2.1, 8.2.2, 8.3.1 Authorization | 1 | Met | P05 to P08, P14; T-02 |
| 11.4.2 Password hashing | 2 | Partly met | R-04 |
| 13.4.1 No source control metadata | 1 | Met | P19 |
| 13.4.2 Debug features off in production | 2 | Deployment requirement | The documented run uses Development; responses stay generic (P15) |
| 13.4.4 No TRACE | 2 | Not met | F-14 |
| 14.2.1 No sensitive data in URLs | 1 | Met | Credentials travel in POST bodies |
| 14.3.1 Client data cleared after logout | 1 | Partly met | No client storage; authenticated pages are `no-store`; logout sends no `Clear-Site-Data` |
| 14.3.2 Anti-caching headers | 2 | Met | P12 |
| 15.1.1 Documented remediation time frames | 1 | Not met | R-03 |
| 15.2.1 Components within time frames | 1 | Met for now | No known vulnerable component |
| 15.3.3 Mass assignment | 2 | Met | P11 |
| 15.3.4 Client address behind proxies | 2 | Deployment requirement | The login limiter keys on the connection address |
| 16.3.1, 16.3.2 Authentication and authorization logging | 2 | Not met | R-02 |
| 16.3.4 Unexpected errors logged | 2 | Met | Exception and last-resort log entries (P15) |
| 16.5.1, 16.5.3 Generic errors; fail securely | 2 | Met | P15; T-11 |

## Dependencies

| Component | Resolved version | Use | Advisory status on 2026-10-05 |
| --- | --- | --- | --- |
| ASP.NET Core and .NET runtime | 10.0.12 | Application | Latest .NET 10 release, itself a security release fixing six CVEs |
| .NET SDK | 10.0.401 | Build | Latest; `global.json` rolls forward to later patches |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Sqlite | 10.0.12 | Application | No advisory |
| SQLitePCLRaw.lib.e_sqlite3 (transitive) | 2.1.12, SQLite 3.53.3 | Application | No advisory; sqlite.org lists no CVE fixed after 3.53.2. Version 3.0.5 of the bundle exists and arrives through a future EF Core update. |
| Microsoft.EntityFrameworkCore.Design | 10.0.12 | Build only | No advisory |
| xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, Microsoft.AspNetCore.Mvc.Testing | 4.0.1, 4.0.0, 18.10.1, 10.0.12 | Test | No advisory |
| Microsoft.Playwright.Xunit.v3 and Microsoft.Playwright | 1.63.0 | Test | No advisory for the NuGet packages or for npm `playwright@1.63.0` |
| Chromium (Playwright revision 1243) | 153.0.8010.12 | Test | Identified by version only |
| actions/checkout, actions/setup-dotnet, actions/upload-artifact | `v7`, `v6`, `v7` tags | CI | No advisory; see F-11 |

A result of "no advisory" means the sources above listed no applicable advisory at the time of the query. It does not show that a component is free of vulnerabilities. Advisories published after 2026-10-05, browser binaries, and the `dotnet-ef` local tool fall outside these checks. The next .NET patch release is expected on 2026-10-13; recheck before tagging if the tag comes later.

## Secrets

- **Dedicated scanner:** none ran. Gitleaks, TruffleHog, detect-secrets, ggshield, and git-secrets are not installed, and the Docker engine was not running. Downloading Gitleaks 8.30.1 requires the owner's approval. GitHub secret scanning and push protection are owner-confirmed (see [GitHub controls](#github-controls)); their alert list needs authentication.
- **Supplementary sweep:** 13 credential patterns (private keys, cloud and GitHub tokens, JWTs, connection-string passwords, credential assignments, seed-password values, URL credentials) over the HEAD tree and all 390 unique blobs reachable from 9 refs and 27 commits. It found 9 matches in tracked files and 24 in history. Every match falls into one of these groups:

| Location | Type | Classification |
| --- | --- | --- |
| [tests/integration/StockroomFactory.cs](../../tests/integration/StockroomFactory.cs) lines 22 and 23, and three earlier versions | Two password constants | Test fixture (F-16); identical in every version; valid only for temporary test databases |
| [tests/integration/DemoDataTests.cs](../../tests/integration/DemoDataTests.cs) line 262 | Password that breaks the rules | Test input that the seed must reject |
| [docs/development/setup.md](../development/setup.md) line 64, and earlier `docs/development.md` | Four-character example value | Documented weak password that the seed rejects |
| `src/Stockroom.Web/Data/DemoSeeder.cs`, `tests/e2e/StockroomApp.cs` | Variable assignments | Code that reads configuration or generates passwords; no literal value |

- **Other surfaces:** `appsettings.json` contains no secret. `launchSettings.json` sets only URLs and the environment. The 26 tracked PNG files carry no text metadata. The README screenshots and diagram previews show only synthetic `.test` accounts and architecture labels. The diagram viewers and their JSON sources contain no local path or credential. Ignored files (agent instructions, local skills, databases, user secrets, recordings, `artifacts/`) never appear in history except the four agent-instruction files of F-15.
- **Accounts:** the seeded accounts `member1@stockroom.test`, `member2@stockroom.test`, and `manager@stockroom.test` are synthetic on the reserved `.test` domain. Their passwords come from the operator's user secrets or environment variables and never appear in the repository.

## Static analysis

- **CodeQL:** GitHub default setup ran `Analyze (csharp)` and `Analyze (actions)` for pull request #12 at `6082df1` (run 37355415883) and for the push to `main` at `1045571` (run 37357178974). Both passed. The pull-request check reported "No new alerts in code changed by this pull request", which covers only the documentation that pull request changed. `GET /code-scanning/alerts` returned 401, so the total of open alerts is unverified. A passing CodeQL job does not show that no alert exists. CodeQL does not analyze the inline JavaScript in the HTML diagram viewers, which this review checked by hand.
- **Local analyzers:** the SDK's .NET analyzers ran with every Security-category rule enabled (`AnalysisModeSecurity=All`), including the injection taint rules CA3001 to CA3012. The web project produced no warning. The solution produced one: CA2100 at `tests/integration/DemoDataTests.cs:203`, a test helper whose only caller passes a constant SQL string. Four compiler results in Razor-generated code are suppressed, and two style notes (CA1862, CA1859) are not security issues.
- **Limits:** the review did not run CodeQL locally, Semgrep, or a dedicated .NET security scanner.

## CI and diagnostics

| Area | Result |
| --- | --- |
| Triggers | `push` and `pull_request` to `main`, and `workflow_dispatch`. No `pull_request_target` or `workflow_run`; fork pull requests run with a read-only token and no secrets. |
| Permissions | `contents: read` for the whole workflow; no job raises it. No secret is referenced. Every checkout sets `persist-credentials: false`. |
| Action references | Three GitHub-owned actions pinned to major tags (F-11); no third-party action. |
| Untrusted input | Run steps interpolate only `github.workspace` and `matrix.os`. The status job reads job results through an environment variable and `ConvertFrom-Json`. No step uses pull-request titles, branch names, or other event text. |
| Artifacts | Integration and browser TRX reports upload on every run; browser failure diagnostics upload on failure. Retention is 14 days. Downloads need a signed-in account (the API returned 401). The passing pull-request run produced a 2,409-byte browser artifact and two integration artifacts of about 25 KB. |
| Report contents | The integration TRX contains the fixture passwords in test names (R-01). The browser TRX of a passing run contains no credential. |
| Failure diagnostics | A controlled failure produced a 333 KB trace, a screenshot, and a 1.5 KB server log. The trace holds the generated password and session cookies (R-01). The server log holds startup messages, the seed summary, the listening port, and the content-root path. On a developer machine that path includes the Windows user name; on a CI runner it is the runner workspace. The screenshot shows synthetic history only. The test file was restored and the build rerun. |
| Hosted runs | CI run 37357178899 passed all seven jobs at `1045571`. Check annotations contained only a notice about the `ubuntu-latest` image migration. |

## Documentation viewers

The four files under [docs/diagrams](../diagrams/README.md) are standalone HTML viewers of about 760 KB each, generated by Archify 3.0.1 according to their README. They are documentation, not application endpoints, and GitHub Pages is off for the repository (`has_pages: false`).

| Check | Result |
| --- | --- |
| External resources | No `fetch`, `XMLHttpRequest`, `WebSocket`, `sendBeacon`, dynamic `import`, or external `src` or `href`. Fonts are embedded with their license notice. The only `https` URLs are GitHub source links and license references. |
| Script behavior | No `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `eval`, or `new Function`. Diagram text and source labels render through `textContent`. |
| URL input | Query parameters accept only fixed values (`theme=light` or `dark`, `embed=1`, `present=1`, `openExport=1`). Hash parameters (`focus`, `relation`, `reach`, `route`, `lens`) take effect only when they match a known node, relationship, or filter identifier, and are otherwise ignored. |
| Source links | Links come from embedded JSON pinned to revision `0f30ae2`. They open with `rel="noopener noreferrer"` and `referrerpolicy="no-referrer"`. |
| Browser storage | `localStorage` keeps only viewer preferences: theme, motion, and panel placement. |
| Local paths and credentials | None in the HTML, the JSON sources, or the PNG previews. The string `.archify` appears only as CSS class names. |

The viewers have no security finding. They run inline scripts when opened from disk, so a reader should open copies from this repository, not from an untrusted fork.

## GitHub controls

| Control | Status | Evidence |
| --- | --- | --- |
| `main` branch protection | Not configured (verified) | `"protected": false`; required status checks `off` |
| Required `CI Status` check | Not configured (verified) | No branch protection, branch rules, or rulesets |
| Force-push and deletion restrictions | Not configured (verified) | Same evidence |
| Bypass allowances | None to review | No ruleset exists |
| Private vulnerability reporting | Enabled (verified) | `enabled: true` |
| CodeQL default setup | Running (verified) | Analysis runs for pull requests and pushes to `main`; alert totals unverified |
| Dependency graph and Dependabot alerts | Enabled (owner-confirmed 2026-10-05) | `GET /dependabot/alerts` returned 401 |
| Secret scanning and push protection | Enabled (owner-confirmed 2026-10-05) | `GET /secret-scanning/alerts` returned 401 |
| Actions approval for fork pull requests | Unverified | `GET /actions/permissions/fork-pr-contributor-approval` returned 401 |
| Default workflow token permissions | Unverified | `GET /actions/permissions/workflow` returned 401. The workflow declares `contents: read`, which overrides the default for this workflow. |

The review used no credentials and changed no setting.

## Coverage gaps

- No dedicated secret scanner ran; the regular-expression sweep may miss unusual formats. GitHub secret scanning is owner-confirmed, but its alerts were not visible.
- Open CodeQL, Dependabot, and secret-scanning alerts, Actions approval defaults for fork pull requests, and default token permissions need authenticated access and remain unverified.
- Hosted artifacts could not be downloaded. The review reproduced their contents locally; no hosted failure artifact exists because every run passed.
- Framing protection was verified in Chromium only.
- The fuzz used one seed and 170 requests; no load test ran. Login timing (F-06) was not re-measured.
- The application ran on Windows only; CI runs the integration and browser tests on Ubuntu as well.
- Chromium and the diagram viewers' embedded scripts were reviewed by version and by hand only.

## Release recommendation

**Ready with explicitly accepted limitations.**

Reasons: no confirmed vulnerability; every probe, fuzz check, framing check, and test passed; the dependency sources list no applicable advisory; the secret sweep found no usable credential; and CI and CodeQL passed on the merge commit that matches the reviewed tree.

Before tagging, the owner should:

1. Add the `main` ruleset that requires pull requests and `CI Status` and blocks force pushes and deletion (F-01, also an open item in the [definition of done](../planning/definition-of-done.md)), or record acceptance of the open control.
2. Accept the local-demonstration limitations: F-02 residual session lifetime, F-03 residual lockout risk, F-05, F-06, F-07, F-09, F-10, F-14, and R-01 to R-04. R-01 and R-03 are small changes that could also be fixed before tagging.
3. Note the coverage gaps above, or approve the Gitleaks download so the dedicated scan can run.

Release blockers: none in the application code. A hosted deployment stays blocked until the owner adds HTTPS with HSTS, `Secure` cookies with the `__Host-` prefix, `AllowedHosts` and forwarded-header settings, the Production environment, security-event logging (R-02), a stronger password policy and hashing (F-10, R-04), paging and request limits (F-07), and persistent Data Protection keys.

## Remediation status

The findings above describe commit `6082df1`. On 2026-10-05 the security close-out on branch `fix/security-closeout`, started from `db00f16`, made the changes below. They passed local verification; hosted CI had not run them when this section was written.

| Item | Status | Change | Code and tests |
| --- | --- | --- | --- |
| R-01 | Fixed; hosted CI not yet run | The integration job uploads only `TestResults/*.trx`. The browser job uploads only `TestResults/*.trx`, `TestResults/e2e-artifacts/**/*.png`, and `TestResults/e2e-artifacts/**/server.log`. Playwright traces stay on the runner; to inspect a trace, reproduce the failure locally. Theories take case keys and resolve passwords inside the test, so test names carry no passwords. | [ci.yml](../../.github/workflows/ci.yml) lines 122 to 129 and 174 to 186; `AuthenticationTests`; `DemoDataTests`; `TheoryDataTests.NoTheoryTakesAPasswordArgument` |
| R-02 | Fixed; hosted CI not yet run | Seven security events under the `Stockroom.Web.SecurityEvents` category with stable IDs: 1001 sign-in succeeded, 1002 sign-in failed, 1003 sign-in refused by lockout, 1004 signed out, 1005 login throttled, 1006 access denied by page authorization, and 1007 permission refused by a service. `appsettings.json` sets the category to `Information`, so raising the default level does not hide them. Each event records only what an investigation needs, such as the account ID, client address, request method and path, failure reason, refused action and its required role or policy, and lockout end time. No event records a password, cookie or token value, form body, or the email submitted for an unknown account. Responses, authorization, and throttling are unchanged. | [SecurityEvents.cs](../../src/Stockroom.Web/SecurityEvents.cs); [Login.cshtml.cs](../../src/Stockroom.Web/Pages/Account/Login.cshtml.cs); [Logout.cshtml.cs](../../src/Stockroom.Web/Pages/Account/Logout.cshtml.cs); [Program.cs](../../src/Stockroom.Web/Program.cs) lines 45 to 51 and 63; [ServiceGuard.cs](../../src/Stockroom.Web/Services/ServiceGuard.cs) line 29 and its callers in `PurchaseService` and `StockService`; [appsettings.json](../../src/Stockroom.Web/appsettings.json) line 10; `SecurityEventLoggingTests` (6 tests) |
| R-03 | Fixed | `SECURITY.md` sets best-effort triage and remediation targets by severity for NuGet packages, the .NET runtime and SDK, and GitHub Actions. It describes how advisories are tracked, how temporary mitigations are recorded, and how fixes are verified. The reporting instructions and contact are unchanged. | [.github/SECURITY.md](../../.github/SECURITY.md) |
| R-04 | Fixed; hosted CI not yet run | Identity hashes new passwords with PBKDF2-HMAC-SHA512 at 220,000 iterations, the OWASP figure checked on 2026-10-05. Existing accounts keep their passwords: a hash with fewer iterations still verifies, and Identity replaces it at the next successful sign-in. | [SecurityPolicy.cs](../../src/Stockroom.Web/SecurityPolicy.cs) lines 34 to 41; [Program.cs](../../src/Stockroom.Web/Program.cs) line 25; `PasswordHashingTests` (2 tests) |
| Dedicated secret scan | Completed | Gitleaks 8.30.1 scanned the full reachable history and the current files and found no secret; see [Dedicated secret scan](#dedicated-secret-scan). | Tool and reports outside tracked files |
| F-01 | Open (owner) | No authenticated GitHub access was available: the GitHub CLI is not installed and no token is set, and the close-out did not use stored Git credentials. At 2026-10-05 19:30 UTC the public API reported `main` as unprotected, with no branch rules or rulesets. | [F-01 owner steps](#f-01-owner-steps) |
| F-13 (M7) | Fixed with R-01; hosted CI not yet run | CI failure diagnostics now hold only screenshots and server logs. | As R-01 |
| F-16 (M7) | Unchanged; exposure reduced | The two fixture passwords remain in `StockroomFactory.cs` for temporary test databases, but no longer appear in test names or reports. | As R-01 |

### Behavior changes and limitations

- The first successful sign-in after the hashing upgrade replaces the stored hash and, as Identity does whenever it changes a password hash, rotates the security stamp. Because the stamp is checked on every request, that account's other sessions end at their next request.
- Each hash and verification costs 2.2 times the previous work. The 165 integration tests took 17 to 21 seconds in the close-out runs; the release review's 156 took 14.9 seconds.
- The log now holds account IDs, client addresses, and request paths. Treat it as personal data when choosing where to store it; retention depends on the host and remains a deployment requirement.
- On Windows, ASP.NET Core's default Event Log provider also writes the five `Warning` events (1002, 1003, 1005, 1006, 1007) to the Application log, as it already did for errors. The integration and browser test fixtures turn that provider off ([StockroomFactory.cs](../../tests/integration/StockroomFactory.cs), [StockroomApp.cs](../../tests/e2e/StockroomApp.cs)), so test runs leave nothing in the machine's Application log; the test log collectors and the browser fixture's server log still receive the events.
- Client addresses come from the connection. Behind a proxy they show the proxy until forwarded headers are configured (a deployment requirement, ASVS 15.3.4).
- Event 1006 covers signed-in users refused by a page's role or policy. An anonymous request redirected to sign-in is not logged.
- CI artifacts no longer include Playwright traces.

### Verification of the close-out

The checks ran in an isolated copy of the working tree (172 files) with an empty `APPDATA`, temporary databases, and generated passwords. After that run only documentation changed, so the repository check, the whitespace check, and the secret scan ran again on the final files; the results below are from those reruns.

| Command | Result |
| --- | --- |
| `scripts/Test-Repository.ps1` | Passed: 57 required files, 141 text files |
| `git diff --check`, with the four new files staged in the isolated copy only | No whitespace errors in the 27 changed files |
| `scripts/Test-Dependencies.ps1` | 4 projects, no known NuGet vulnerabilities |
| `dotnet build Stockroom.slnx --configuration Release --no-restore --warnaserror` | 0 warnings, 0 errors |
| Integration tests with the CI test options | 165 total, 165 passed, 0 skipped (156 before; 9 new) |
| Browser tests with the CI test options | 13 total, 11 passed, 0 failed, 2 skipped (the explicit `DemoRecording` and `UiScreenshots` cases) |

Removing each fix in turn failed the tests that cover it:

| Change removed or reversed | Failing tests |
| --- | --- |
| Rate-limit rejection logging | `ThrottledLoginIsLogged` |
| Password added to the failed sign-in log entry | `FailedSignInsAreLoggedWithoutCredentials`, `ThrottledLoginIsLogged` |
| Authorization denial handler | `PageAuthorizationDenialsAreLogged` |
| Service refusal logging | `ServicePermissionRefusalsAreLogged` |
| Iteration setting | `NewHashesUseTheConfiguredIterationCount`, `LowerIterationHashSignsInAndIsUpgraded` |
| Fixture password in theory data | `NoTheoryTakesAPasswordArgument` |

The integration report from the verification run contains neither fixture password and none of the rejected seed values; its theory names show only case keys such as `member-account` and `wrong-password`. During the integration and browser runs, no .NET host wrote an entry to the Windows Application log. A forced browser failure after the access-denied check saved a server log that still held the console lines for events 1001 and 1006. The owner's database, user secrets, and Data Protection key ring were not modified: the key ring's three files kept their names, sizes, and timestamps, and the user-secrets file and working database kept their 2026-10-03 write times.

### Controlled CI-artifact failure

In an isolated copy, `Assert.Fail` was added after the last assertion of `AccessRestrictionTests.MemberHistoryShowsOnlyTheirOwnRequests`, the test ran with the CI environment variable and test options, and the change was then restored. Applying the three upload patterns from `ci.yml` to the results gave:

| File | Size | Uploaded | Contents |
| --- | --- | --- | --- |
| `e2e.trx` | 2,917 bytes | Yes | Test name and the failure message |
| `user1.png` | 72,317 bytes | Yes | The member's history page with synthetic data |
| `server.log` | 1,646 bytes | Yes | Startup messages, the seed summary, and one sign-in event with an account ID and `127.0.0.1` |
| `user1-trace.zip` | 334,673 bytes | No | Stays on the runner |

The checks over the three uploaded files found no generated password, raw or form-encoded; no Data Protection payload (`CfDJ8`, the prefix of cookie and antiforgery token values); no authentication or antiforgery cookie name; no `__RequestVerificationToken` or `Input.Password` field; and no seed-password setting. As a control, the same checks found the password, its form-encoded copy, Data Protection payloads, both cookie names, and both form fields in the excluded trace.

### Dedicated secret scan

The scan used Gitleaks 8.30.1 (`gitleaks_8.30.1_windows_x64.zip` from the project's GitHub release). Its SHA-256 matched the release's published checksum file.

| Scope | Command | Result |
| --- | --- | --- |
| All 28 commits reachable from 10 refs; `-m` diffs the 12 merge commits against each parent | `gitleaks git . --log-opts="--all -m" --redact` | 7.55 MB scanned; no leaks |
| The working tree's 172 tracked and untracked, non-ignored files, including the close-out changes | `gitleaks dir <export> --redact` | 3.78 MB scanned; no leaks |
| Positive control: a scratch file holding a synthetic, non-functional GitHub-token-shaped string | `gitleaks dir <control> --redact` | Detected by the `github-pat` rule, with the value redacted |

The scan used Gitleaks's default rules with no configuration file and no exclusions. Those rules did not flag the integration fixture passwords (F-16), which remain classified as test values that work only against temporary test databases. The scan found no real secret, so nothing needs rotation. Ignored local files, such as agent instructions and raw diagnostics, were outside its scope.

### F-01 owner steps

F-01 stays open until the owner applies these settings and the check below confirms them. The rules require no approving review, so the owner can still merge their own pull requests.

1. On GitHub, open the repository's **Settings**. In the sidebar, under **Code and automation**, choose **Rules** > **Rulesets**, then **New ruleset** > **New branch ruleset**.
2. Name the ruleset, for example `Protect main`, and set **Enforcement status** to **Active**.
3. Leave the **Bypass list** empty, so the rules apply to the owner too.
4. Under **Target branches**, choose **Add target** > **Include default branch**.
5. Under **Branch rules**, make sure **Restrict deletions** and **Block force pushes** are selected.
6. Select **Require a pull request before merging** and set **Required approvals** to **0**.
7. Select **Require status checks to pass**, choose **Add checks**, and add `CI Status` from GitHub Actions. `CI Status` fails when any other CI job fails or is skipped, so no other check is needed.
8. Leave the other rules at their defaults and choose **Create**.

To confirm, request `https://api.github.com/repos/AdityaJadhav17/Stockroom/rules/branches/main`. The response must list the `deletion`, `non_fast_forward`, `pull_request` (with `required_approving_review_count` 0), and `required_status_checks` (with context `CI Status`) rules. Then record the date and result in the F-01 row above and in the [definition of done](../planning/definition-of-done.md).

## Raw evidence

The ignored `artifacts/security-closeout/` directory holds the close-out evidence: GitHub API responses (`github/`), key-ring metadata snapshots, sabotage logs, verification logs and reports (`verification/`, `final-checks/`), the Event Log isolation check (`eventlog-isolation/`), the simulated CI upload with its inspection output (`ci-upload-simulation/`), and the redacted Gitleaks reports (`secret-scan/`). The Gitleaks binary stays outside the repository.

The ignored `artifacts/security-review/` directory holds the raw output: GitHub API responses (`github/`), dependency queries and release metadata (`dependencies/`), verification logs and TRX reports (`verification/`), the controlled-failure diagnostics (`e2e-failure/`), probe and fuzz results with a scrubbed server log (`probes/`), analyzer logs and SARIF (`static-analysis/`), the pattern-sweep locations (`secrets/`), and the ASVS and OWASP source files (`asvs/`). The raw files contain generated test credentials inside the failure trace; keep that directory out of Git and delete it when no longer needed.

## References

- [M7 security review](security-review.md) and [ADR 0007](../decisions/0007-m7-security-hardening.md)
- OWASP ASVS 5.0.0: <https://github.com/OWASP/ASVS/tree/master/5.0/en>
- OWASP Password Storage Cheat Sheet: <https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html>
- .NET 10 release metadata: <https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json>
- SQLite CVE list: <https://www.sqlite.org/cves.html>
- GitHub Advisory Database API: <https://docs.github.com/en/rest/security-advisories/global-advisories>
- Playwright tracing: <https://playwright.dev/dotnet/docs/trace-viewer>
- GitHub code scanning default setup: <https://docs.github.com/en/code-security/code-scanning/enabling-code-scanning/configuring-default-setup-for-code-scanning>
