# ADR 0005: M4 demo dataset and reset

Date: 2026-10-04

Status: Accepted. Implemented in M4.

## Context

US-08 requires a predictable synthetic dataset with request and movement examples, seed reruns that preserve existing data, and an explicit reset limited to the local Development database. [ADR 0002](0002-m1-authentication-and-seed.md) introduced the seed command without examples or reset.

## Decision

| Area | Decision |
| --- | --- |
| Dataset | Three accounts, ten items, four purchase requests (one Pending, one Approved, one Rejected, one Received), their request events, ten opening movements, one receipt movement, and one issue movement. Filament stays at two spools with a reorder threshold of three, and no example request uses filament, so the purchase demo starts from the documented state. |
| Fixed times | Opening movements use 2026-09-01 08:00 UTC, and examples use fixed UTC times on the following days. A fresh seed or reset produces the same records apart from generated identifiers, password hashes, and security stamps. |
| Examples only on a fresh database | The seeder writes examples only in the run that creates the items, in the same transaction as the items and opening movements. A rerun adds only missing roles, accounts, role memberships, and items. A database seeded before M4 keeps its records and receives no examples. |
| Reconciliation | Each item's quantity equals the sum of its movements. The example receipt adds four plywood packs, and the example issue removes two jumper-wire packs. |
| Reset | `dotnet run --project src/Stockroom.Web -- seed --reset` runs only in Development. It checks both seed passwords against the Identity password rules, validates the configured connection string, closes pooled connections to that database only, deletes that one validated file, applies migrations, and seeds the dataset. It does not call EF Core's `EnsureDeletedAsync`, which clears every SQLite connection pool in the process. A missing or rejected password stops the command before it deletes or creates anything. |
| Reset target | The data source must be a plain local path ending in `.db`, opened in the default read-write-create mode. Memory databases, `file:` URIs, wildcards, `DataDirectory` substitutions, directories, other extensions, and existing files without the SQLite header are refused before any deletion. If another process locks the file so it cannot be read, or holds it open on Windows so it cannot be deleted, the command reports the failure and changes nothing. |
| Command testing | `DemoSeeder.RunCommandAsync` holds the command logic and returns the exit code, so tests run it against disposable databases in Development and Production hosts. |
| Test fixture | Integration tests use the base dataset without examples unless they request the full dataset. The base dataset keeps earlier tests independent of example records. |

## Alternatives

| Option | Reason for rejection |
| --- | --- |
| Add examples whenever no purchase requests exist | A rerun on a database with user changes but no requests would change quantities and history. |
| Seed examples through the services | The services stamp the current time, which would make each fresh dataset different. |
| Delete every file matching the database name | Sidecar or similarly named files could belong to another tool. The command deletes only the validated database file. |
| EF Core `EnsureDeletedAsync` | It clears every SQLite connection pool in the process, which closes connections that other code in the same process may be using. |
| Reset on application startup | Reset must be an explicit, deliberate command. |

## Consequences

A developer who wants the examples in an existing local database must run the reset command, which deletes that database's local demo data. Reset requires the application to be stopped on Windows. The base test dataset differs from the demo dataset by design; `DemoDataTests` covers the full dataset.
