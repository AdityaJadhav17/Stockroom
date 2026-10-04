# ADR 0007: M7 security hardening

Date: 2026-10-04

Status: Accepted. Implements part of the [security review](../security-review.md) remediation plan.

## Context

The M7 review found that logout left copied cookies valid while they kept being used (F-02), that role changes reached an existing cookie only after 30 minutes (F-08), that an anonymous caller could lock the Manager account (F-03), and that authenticated pages sent no anti-framing or other security headers (F-04, F-09).

## Decision

| Area | Decision |
| --- | --- |
| Session revocation | Logout calls `UpdateSecurityStampAsync` before `SignOutAsync`. `SecurityStampValidatorOptions.ValidationInterval` is zero, so every authenticated request compares the cookie's stamp with the database and rebuilds role claims. If the stamp update fails or returns an error, logout still signs out the current browser, logs the failure, and shows the user that other sessions may remain signed in. |
| Session lifetime | The authentication cookie has a one-hour sliding idle timeout (`SessionPolicy.IdleTimeout`). |
| Login throttling | The built-in ASP.NET Core rate limiter allows five login POSTs per client address per one-minute sliding window with six segments (`SessionPolicy`) and returns 429 with the error page. GET requests are not limited. A fixed window was rejected after review because it allows a full window of attempts on each side of a boundary. Segments return their permits after window minus one segment, so one client can still make 10 attempts within one window; the lockout threshold is 11 (`2 x limit + 1`), and one client starting from no failed attempts needs about 100 seconds to reach it. |
| Security headers | `SecurityHeaders` middleware adds `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'`, `X-Content-Type-Options: nosniff`, and `Referrer-Policy: same-origin` to every response. The antiforgery system's own `X-Frame-Options` header is suppressed so the stricter value applies. |
| Antiforgery cookie | `SecurePolicy` is `SameAsRequest`, so the cookie carries `Secure` over HTTPS. |
| Last-resort error page | `LastResortErrorPage` runs outside `UseExceptionHandler`. If the `/Error` re-execution fails, for example because authentication cannot read the database, it logs the exception and returns a fixed 500 page. Without it, the Development developer exception page would show the details. |

## Trade-offs

- **One lookup per request:** per-request stamp validation adds one user lookup per authenticated request. The demonstration's load makes this acceptable; a hosted release with heavy traffic could use a short interval instead.
- **Logout is account-wide:** rotating the stamp ends every session for the account, including other browsers. The demo accounts are personal, so this is acceptable and covered by a test.
- **Active sessions still renew:** a session in use renews without an absolute limit. A fixed maximum session age would need a custom claim check or a server-side ticket store; it is deferred.
- **Rate limit is per address:** clients behind one shared address share the limit. The failure count persists between windows, so one persistent client can still lock an account after about 100 seconds when starting from no failed attempts, sooner if earlier failures remain, and attackers using many addresses sooner. The limit does not remove the login timing difference (F-06).
- **CSP depends on no inline code:** the policy allows only this origin. A future inline script or style needs a nonce or a policy change.

## Alternatives

| Option | Reason for rejection |
| --- | --- |
| Shorter validation interval only | A copied cookie would remain usable until the interval elapsed. |
| Server-side ticket store | Revokes single sessions but adds storage and code beyond the demonstration's needs. |
| Disable lockout and rely on throttling | Distributed guessing would face no per-account limit. |
| Third-party header or throttling packages | The framework provides both features without new dependencies. |

## Consequences

A copied cookie fails on its next request after logout, and role changes apply immediately. Users who mistype a password five times wait until the oldest segment of attempts leaves the window, at most about one minute. Branch protection (F-01), HTTPS and host configuration (F-05, F-09), and paging (F-07) remain open.
