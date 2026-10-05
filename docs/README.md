# Documentation

The documents are grouped by purpose. Start with the [MVP](planning/mvp.md) for the business rules, or the [setup guide](development/setup.md) to run the application.

## Planning

| Document | Contents |
| --- | --- |
| [MVP](planning/mvp.md) | Scope, business rules BR-01 to BR-11, and deferred work |
| [Personas](planning/personas.md) | Member and manager responsibilities |
| [User stories](planning/user-stories.md) | US-01 to US-08 with acceptance criteria and evidence |
| [Delivery plan](planning/delivery-plan.md) | Milestones and their status |
| [Definition of done](planning/definition-of-done.md) | Story and release checklists |

## Development

| Document | Contents |
| --- | --- |
| [Architecture](development/architecture.md) | Application structure, records, transitions, and transactions |
| [Architecture diagrams](diagrams/README.md) | Interactive runtime, data, purchase lifecycle, and CI diagrams with source links |
| [Setup](development/setup.md) | Prerequisites, local setup, demo dataset, reset, checks, and CI |
| [GitHub repository setup](development/github-repository-setup.md) | Community files and GitHub settings |
| [Test structure](../tests/README.md) | Unit, integration, E2E, and fixture projects |

## Quality

| Document | Contents |
| --- | --- |
| [Test plan](quality/test-plan.md) | Acceptance scenarios T-01 to T-15, coverage map, and manual demo |
| [Accessibility](quality/accessibility.md) | Verified behavior and known gaps |

## Security

| Document | Contents |
| --- | --- |
| [Security review](security/security-review.md) | M7 audit findings, evidence, and remediation status |
| [Dependency audit](security/dependency-audit.md) | Package versions and vulnerability-check evidence |

## Decisions

| ADR | Decision |
| --- | --- |
| [0001](decisions/0001-application-foundation.md) | Application stack |
| [0002](decisions/0002-m1-authentication-and-seed.md) | M1: account pages, schema constraints, seed command, and test runner |
| [0003](decisions/0003-m2-purchase-workflow.md) | M2: purchase service, conditional transitions, lock contention, and access rules |
| [0004](decisions/0004-m3-stock-issues-and-history.md) | M3: stock issues, shared service guards, and history visibility |
| [0005](decisions/0005-m4-demo-dataset-and-reset.md) | M4: demo dataset, seed reruns, and reset safeguards |
| [0006](decisions/0006-m6-release-preparation.md) | M6: database-error handling, error pages, and interface accessibility |
| [0007](decisions/0007-m7-security-hardening.md) | M7: security headers, session revocation, and login throttling |
| [0008](decisions/0008-m8-ui-polish.md) | M8: interface design, status badges, phone layouts, and dark mode |
| [0009](decisions/0009-m9-repository-organization.md) | M9: repository layout and release documents |

## Releases

| Document | Contents |
| --- | --- |
| [v1.0.0 release notes](releases/v1.0.0.md) | Draft notes: capabilities, evidence, and limitations |
| [Demo script](releases/demo-script.md) | Narration for a two- to three-minute walkthrough |
| [Interview notes](releases/interview-notes.md) | Design decisions and how the tests prove them |

Screenshots live in [images](images/01-member-dashboard.png); the M8 before-and-after comparisons are in [images/ui-polish](images/ui-polish).

The MVP business rules are the release contract. If the owner changes a requirement, update the affected stories, tests, and ADRs together.
