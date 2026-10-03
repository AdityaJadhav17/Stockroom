# ADR 0002: M1 authentication, schema, and seed

Date: 2026-10-03

Status: Accepted. Implemented in M1.

## Context

M1 adds the web application, Identity, the initial schema, and a Development seed command. The MVP excludes public registration and password recovery. Tests must run through `dotnet test` on the pinned .NET 10 SDK.

## Decision

| Area | Decision |
| --- | --- |
| Account pages | The application provides its own Login and Logout Razor Pages and omits the `Microsoft.AspNetCore.Identity.UI` package. Without that package, no Register or ForgotPassword page exists. Identity registers no token providers, so the application cannot issue password-reset tokens. |
| User record | The application uses the framework `IdentityUser` type. The `ApplicationUser` row in the architecture maps to this type until a custom user field becomes necessary. |
| Authorization default | A Razor Pages convention requires an authenticated user for every page. Only `/Account/Login` allows anonymous access. New pages inherit the requirement. |
| Login errors | Unknown accounts, wrong passwords, and lockouts produce the same message. Identity locks an account for five minutes after five failed attempts. |
| Schema | Request status, request events, and movement actions use string columns with CHECK constraints. CHECK constraints also enforce non-negative stock, positive request quantities, non-zero movement deltas, and reason length. SQLite ignores `HasMaxLength`, so a CHECK constraint limits movement reasons to 1 through 500 non-blank characters, and an `Issue` movement requires a reason. A `Receipt` movement requires a purchase-request reference, and a filtered unique index allows one `Receipt` movement per purchase request. The second migration, `StockMovementReasonAndReceiptChecks`, adds the movement reason and receipt-reference constraints. |
| Seed command | `dotnet run --project src/Stockroom.Web -- seed` applies migrations and seeds the data. The command exits with code 1 outside the Development environment. It reads passwords from `Seed:MemberPassword` and `Seed:ManagerPassword` through user secrets or environment variables. It adds missing roles, accounts, role memberships, and items by email or SKU, and leaves existing records unchanged. Each new item receives an `Opening` stock movement in the same save. |
| Test runner | `global.json` selects Microsoft.Testing.Platform for `dotnet test`. The .NET 10 SDK rejects the VSTest target for xUnit v3 projects, so tests use `dotnet test --project <path>`. |
| Local transport | The default launch profile serves HTTP on `localhost:5080`. The HTTPS profile requires a trusted development certificate. The application omits HTTPS redirection and HSTS until hosting work starts. |
| EF tooling | `.config/dotnet-tools.json` pins `dotnet-ef` 10.0.12 for creating migrations. |

## Alternatives

| Option | Reason for rejection |
| --- | --- |
| Identity UI package with blocked pages | The package ships registration and recovery pages that the application would need to remove or override. |
| `ApplicationUser` subclass with no fields | The subclass adds a type without behavior. |
| Seed on application startup | The requirement calls for an explicit Development command, and startup seeding would change data during tests and production runs. |
| VSTest runner settings | The pinned SDK does not support VSTest for these xUnit v3 projects. |

## Consequences

The account pages contain only the framework calls that the MVP needs. A later account feature requires a new page and, for recovery, token providers. The seed command does not yet create request examples or offer reset; M4 adds them under US-08. Hosted deployment needs HTTPS redirection and HSTS.
