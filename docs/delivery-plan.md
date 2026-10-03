# Delivery plan

## SDLC checkpoints

| Stage | Work | Exit evidence |
| --- | --- | --- |
| Requirements | Owner and contributor review MVP rules and story acceptance criteria. | Agreement on scope; open decisions recorded in the change notes. |
| Design | Contributor checks the data model, permissions, and transaction strategy. | ADR changes if needed; first implementation plan. |
| Implementation | Contributor delivers the milestones below in small branches. | Runnable changes and related story updates. |
| Verification | Contributor executes acceptance tests and owner exercises the demo. | Commands, results, and defect reproduction steps. |
| Release | Owner checks a fresh checkout and records the walkthrough. | Completed release checklist and accurate README status. |
| Maintenance | Owner records defects and prioritizes the next release. | Bug reproduction, regression test, and updated requirements. |

Requirements, design documents, and a buildable test foundation exist. Contributors still need to review the requirements before coding. The owner has not verified application behavior.

## Two-day implementation budget

Start the budget after the SDK and package restore work. Reserve 12 to 16 focused hours; unfamiliar tooling or blocked package downloads can extend it.

| Milestone | Budget | Work and exit criteria |
| --- | --- | --- |
| M1: Foundation | 2 hours | Use the existing solution, SDK pin, and test projects; scaffold the web app; configure SQLite and Identity; verify login; add application test execution to CI. |
| M2: Purchase workflow | 3 hours | Implement inventory, requests, approval, and receipt; demonstrate stock changing from two to seven. |
| M3: Integrity and history | 2 hours | Add stock issues, server permissions, validation, conditional writes, and transaction-linked history. |
| M4: Demo data | 1 hour | Prepare ten items and consistent histories; verify seed reruns; capture a working Day 1 walkthrough. |
| M5: Verification | 3 hours | Run permission, duplicate-receipt, concurrency, and rollback tests; fix failures. |
| M6: Release preparation | 3 hours | Improve errors and empty states; check a fresh checkout; update documentation; record a short demo. |
| Contingency | 2 hours | Resolve setup or integration defects discovered during verification. |

The estimates total 16 hours with contingency. Reduce visual polish if implementation takes longer. Keep the stock and permission checks in the release criteria. Record unfinished stories as Planned or In progress.

## Working agreement

Use one coding tool as the editor for a milestone and another as the reviewer. Share the story IDs, changed files, and verification results at handoff. Avoid simultaneous edits to the same files. The owner reviews behavior through the application and discusses the code before claiming interview readiness.

## Current risks

| Risk | Response |
| --- | --- |
| A contributor's SDK may differ from the configured version. | SDK 10.0.401 is installed on the owner's workstation. Use `global.json` and verify the SDK before M1. |
| Login scaffolding or package restore consumes the setup budget. | Use framework templates and confirm login before adding domain features. |
| Read-then-write stock code permits competing requests to corrupt quantities. | Use conditional writes in a transaction and execute T-12 against SQLite. |
| A reviewer mistakes seeded examples for customer use. | Label synthetic data and retain the implementation status in the README. |
| Contributors add features before completing verification. | Track deferred work in the MVP document and finish the Must stories first. |
