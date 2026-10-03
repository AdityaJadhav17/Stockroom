# Claude implementation handoff

## Context

The owner wants an entry-level C#/.NET portfolio project with a working business workflow and evidence for its failure cases. They plan to use Codex and Claude during development. The project represents a fictional campus makerspace and uses synthetic data.

The owner requested repository setup first. Contributors have not implemented the application. This repository contains requirements, a proposed architecture, an implementation backlog, community files, and three test scaffolds. CI checks files, audits dependencies, and builds the scaffold.

## Read before coding

Read [AGENTS.md](../AGENTS.md), then [mvp.md](mvp.md), [user-stories.md](user-stories.md), and [architecture.md](architecture.md). Consult [delivery-plan.md](delivery-plan.md) for milestones and [test-plan.md](test-plan.md) for verification. Apply [writing-guide.md](writing-guide.md) to prose.

## Agreed release scope

Build one .NET 10 Razor Pages application with C#, EF Core, SQLite, ASP.NET Core Identity, and xUnit tests. Include Member and Manager roles, ten seeded inventory items, purchase requests, manager review, full receipt, stock issues, and history.

Members create purchase requests and read their own requests. Managers review requests, receive supplies, issue stock, and read full history. Managers cannot create purchases in this release. Keep the state sequence Pending to Approved or Rejected, then Approved to Received. Approval does not change stock.

The most important implementation checks are server authorization, request ownership, receipt deduplication, stock integrity during competing requests, and transaction rollback. Use SQLite for database tests and retain evidence for the story criteria.

## First task: M1 foundation

1. Inspect the checkout and Git status. Preserve work from other contributors.
2. Verify the .NET 10 SDK against `global.json`. The owner has installed 10.0.401, and the solution and test projects build.
3. Add the web project to the existing solution using the structure in [development.md](development.md). Add project references from the test scaffolds; preserve central versions and lock files.
4. Configure SQLite, Identity roles, initial schema, and a Development seed command with passwords outside Git.
5. Implement login and logout for demo accounts. Verify unauthenticated access and the role boundaries required by US-01.
6. Add application test execution to CI once cases exist. Preserve the repository check, locked restore audit, and Release build.
7. Run the commands and update setup instructions with the actual command forms. Record completed acceptance criteria, test results, and remaining work.

Finish M1 before starting the purchase workflow. Coordinate ownership of shared files during tool handoffs. Use the existing story IDs in change notes so another contributor can resume work.

## Later milestones

Implement US-02 through US-08 following the delivery plan. Keep app logic in services and conditional database writes inside transactions. Receipt must commit its status, stock movement, and history together. Test competing withdrawals against the same SQLite database file.

Use the two-day budget for the agreed scope. Record item administration, supplier management, partial receipts, and hosting as later work. Update the ADR and affected acceptance criteria if the owner changes a requirement.

## Completion report

Report changed behavior, story IDs, commands and results, known defects, and the next milestone. Do not claim a build, test pass, customer deployment, or performance result without evidence. Describe this project as complete after the owner verifies [definition-of-done.md](definition-of-done.md).
