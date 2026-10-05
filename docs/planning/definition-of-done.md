# Definition of done

The developer completes the story checklist for each change. The owner completes the release checklist before calling the MVP complete. An item is checked only where the evidence column points to a verified result; the developer reviewed M9 on 2026-10-04 and refreshed release evidence on 2026-10-05.

## Story completion

These criteria apply to each of US-01 to US-08. The [user stories](user-stories.md) record the evidence for each story.

| Done | Criterion | Evidence |
| --- | --- | --- |
| [x] | Implement the linked story's acceptance criteria and business rules. | Evidence column in [user stories](user-stories.md) |
| [x] | Verify input validation and server-side authorization for the affected actions. | T-02, T-05, and T-10 in the [test plan](../quality/test-plan.md) |
| [x] | Exercise the normal workflow and the relevant failure cases. | Coverage map in the [test plan](../quality/test-plan.md) |
| [x] | Add a regression test for a behavior fix or a critical invariant. | Regression tests named in ADRs [0006](../decisions/0006-m6-release-preparation.md) to [0008](../decisions/0008-m8-ui-polish.md) and the [security review](../security/security-review.md) |
| [x] | Run the repository, build, and test commands and record the results. | Each milestone's ADR and the [setup guide](../development/setup.md) |
| [x] | Update affected documentation and the backlog evidence column. | [User stories](user-stories.md) |
| [x] | Have a reviewer inspect the change and resolve defects that break acceptance criteria. | Pull requests #1 to #8, merged by the owner after independent review |

## MVP release

| Done | Criterion | Evidence |
| --- | --- | --- |
| [x] | Complete US-01 to US-08 and link their verification evidence. | All eight stories merged; see [user stories](user-stories.md) |
| [x] | Restore, build, and test from a fresh checkout using the documented SDK. | Isolated M9 copy on 2026-10-04; see the [setup guide](../development/setup.md#local-setup) |
| [x] | Create the schema through migrations and run the Development seed command. | Same isolated copy: the seed applied the migrations to a new database |
| [x] | Confirm seed reruns preserve existing counts and quantities. | `DemoDataTests` (T-14); a second seed in the isolated copy printed the same counts |
| [x] | Demonstrate purchase approval, receipt, withdrawal, and history. | `PurchaseAndWithdrawalTests`; `DemoRecording` produced the README screenshots |
| [x] | Prove that members cannot perform manager actions or read another member's request. | T-02 in `PurchaseWorkflowHttpTests`, `StockAndHistoryHttpTests`, and `AccessRestrictionTests` |
| [x] | Prove that repeated or competing receipts increase stock once. | T-09 in `PurchaseServiceTests` |
| [x] | Prove that competing withdrawals cannot make stock negative. | T-12 in `StockServiceTests.CompetingOneUnitIssuesCannotOversell` |
| [x] | Prove that a failed history write rolls back its stock or status change. | T-11 in `PurchaseServiceTests` and `StockServiceTests` |
| [x] | Confirm committed data survives an application restart. | T-13 in `PersistenceTests` |
| [x] | Check forms, field errors, empty states, and keyboard navigation. | Manual checks in M8 and `ResponsiveLayoutTests`; see [accessibility](../quality/accessibility.md) |
| [ ] | Run application CI on the release commit and record the result. | Run [37249415888](https://github.com/AdityaJadhav17/Stockroom/actions/runs/37249415888) passed all seven jobs at `12ffc87`, including M9 and the diagram updates. Keep this item open until CI passes on the commit selected for the release tag. |
| [x] | Document demo credential setup without committing passwords or local databases. | [Setup guide](../development/setup.md); `.gitignore` excludes `*.db` and secrets files |
| [x] | Update the README with setup commands, status, screenshots, and known limitations. | [README](../../README.md), revised in M9 |
| [ ] | Record a two- to three-minute demonstration and identify the data as synthetic. | Use the [demo script](../releases/demo-script.md) and [recording plan](../releases/demo-recording-plan.md). Add the video reference after recording. |
| [x] | Record remaining defects and deferred scope without marking them complete. | README limitations and the [draft release notes](../releases/v1.0.0.md) |
| [ ] | Protect `main` with a ruleset that requires pull requests and `CI Status`. | On 2026-10-05 the public API reported `main` as unprotected with no branch rules; see [GitHub repository setup](../development/github-repository-setup.md). |
| [ ] | Owner signs off on the release. | Pending |

## Evidence format

For a story, record the commit or pull request, test IDs, command, and result. For the release, keep a fresh-checkout setup result and the demo reference. A passing repository check covers file and link hygiene; it does not prove application behavior.
