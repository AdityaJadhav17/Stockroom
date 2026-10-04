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

M1 through M8 are implemented and merged through pull requests #1 to #8. Hosted CI run [37184964723](https://github.com/AdityaJadhav17/Stockroom/actions/runs/37184964723) passed all seven jobs on `main` at the M8 merge. M9 reorganizes the repository and adds the release documents in the working tree ([ADR 0009](../decisions/0009-m9-repository-organization.md)); it awaits review and hosted CI. The owner's release sign-off remains open.

| Milestone | Delivered |
| --- | --- |
| M1 | Login, Identity roles, the schema, and the seed command |
| M2 | Inventory search, the low-stock dashboard, purchase requests, manager review, and receipt |
| M3 | Manager stock issues and history |
| M4 | The demo dataset and `seed --reset` |
| M5 | Chromium browser tests, a restart persistence test, and an End-to-End CI job |
| M6 | Controlled database-error handling, error pages, accessibility fixes, and README screenshots |
| M7 | The [security review](../security/security-review.md), plus header, session, and login-throttling fixes |
| M8 | Interface design, phone layouts, dark mode, and layout tests ([ADR 0008](../decisions/0008-m8-ui-polish.md)) |
| M9 | Repository layout, release notes, demo script, and interview notes |

## Two-day implementation budget

The original plan covered M1 to M6. M7 to M9 came after the budget, at the owner's request.

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

Assign an implementer and a reviewer for each milestone. Include story IDs, changed files, and verification results in review notes. Coordinate changes to shared files. The owner reviews behavior through the application and discusses the code before claiming interview readiness.

## Current risks

| Risk | Response |
| --- | --- |
| A contributor's SDK may differ from the configured version. | SDK 10.0.401 is installed on the owner's workstation. Use `global.json` and verify the SDK before M1. |
| Login scaffolding or package restore consumes the setup budget. | Use framework templates and confirm login before adding domain features. |
| Read-then-write stock code permits competing requests to corrupt quantities. | Use conditional writes in a transaction and execute T-12 against SQLite. |
| A reviewer mistakes seeded examples for customer use. | Label synthetic data and retain the implementation status in the README. |
| Contributors add features before completing verification. | Track deferred work in the MVP document and finish the Must stories first. |
