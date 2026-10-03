# ADR 0001: Application foundation

Date: 2026-10-03

Status: Accepted. SDK and test foundation configured; web application pending.

## Context

The owner wants a C#/.NET portfolio project with a two-day implementation budget. Reviewers need a working purchase workflow, database integrity checks, and a repeatable local setup. The first release uses fictional users and synthetic data.

## Decision

Build one .NET 10 ASP.NET Core Razor Pages application. Use Entity Framework Core with SQLite, ASP.NET Core Identity for login, and xUnit for application tests. Keep business rules in services within the web project.

The developer verified SDK 10.0.401 and recorded it in `global.json` with `latestPatch` roll-forward and prerelease SDKs disabled. CI reads that file. The solution contains three test scaffolds; the web application remains pending.

Microsoft lists .NET 10 as an active LTS release, with support through November 14, 2028. The owner selected it over .NET 8 because .NET 8 support ends on November 10, 2026. Source: [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), checked October 3, 2026.

## Alternatives

| Option | Reason for deferral |
| --- | --- |
| React client and separate API | The developer would maintain two build systems and an additional application boundary. |
| SQL Server | The owner has no SQL Server setup requirement for the local demo. The developer can add this provider for a target role after the MVP. |
| Microservices and a message broker | The purchase workflow fits inside one database transaction. Additional services would consume the implementation budget. |

## Consequences

Contributors can develop and run the first release without a separate database service. They must test SQLite contention and constraints against SQLite. They will need provider-specific verification if they change databases.

The owner will treat local setup, critical tests, and the demo recording as release evidence. Cloud hosting remains a later milestone.
