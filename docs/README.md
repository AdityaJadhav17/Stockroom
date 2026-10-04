# Project documentation

The owner plans a two-day MVP for a fictional makerspace. Contributors use these documents to agree on behavior, implement it, and collect release evidence.

## Requirements

- [MVP](mvp.md): purpose, scope, business rules, and constraints.
- [Personas](personas.md): member and manager responsibilities.
- [User stories](user-stories.md): implementation backlog with acceptance criteria.

## Design and delivery

- [Architecture](architecture.md): application boundaries and proposed data model.
- [Foundation decision](decisions/0001-application-foundation.md): rationale for the planned stack.
- [M1 decision](decisions/0002-m1-authentication-and-seed.md): account pages, schema constraints, seed command, and test runner.
- [M2 decision](decisions/0003-m2-purchase-workflow.md): purchase service, conditional transitions, lock contention, and access rules.
- [M3 decision](decisions/0004-m3-stock-issues-and-history.md): stock issues, shared service guards, and history visibility.
- [Delivery plan](delivery-plan.md): milestones and SDLC checkpoints.
- [Development setup](development.md): prerequisites, repository checks, and application CI work.
- [Test structure](../tests/README.md): configured projects and fixture conventions.

## Verification

- [Test plan](test-plan.md): acceptance scenarios, regression checks, and demo script.
- [Definition of done](definition-of-done.md): story and release criteria.
- [Dependency audit](dependency-audit.md): initial resolved graph and vulnerability evidence.
- [Community setup](community-setup.md): files in place and GitHub settings after publication.

Treat the MVP business rules as the release contract. Use the architecture decision record for stack changes. Update affected stories and tests if the owner changes a requirement.
