# ADR 0009: M9 repository organization

Date: 2026-10-04

Status: Proposed. Awaits the owner's review.

## Context

By M8 the root held nine Markdown files, and `docs/` held fifteen documents in one flat folder. A reader had to open the index to tell planning records from setup instructions or security evidence. Several documents still described the project before its first release: the security policy said no application existed, and the accessibility page said the repository had no interface.

## Decision

| Area | Decision |
| --- | --- |
| Root | Keep only the README, license, solution, SDK and build configuration, NuGet configuration, and editor and Git settings. |
| Community files | Move `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, and `SUPPORT.md` into `.github/`. GitHub reads community files from the root, `.github/`, or `docs/`; `.github/` keeps them beside the templates and workflow. |
| Documentation | Group `docs/` by purpose: `planning/`, `development/`, `quality/`, `security/`, `decisions/`, `images/`, and `releases/`. `development.md` becomes `development/setup.md`, `community-setup.md` becomes `development/github-repository-setup.md`, and `ACCESSIBILITY.md` becomes `quality/accessibility.md`. |
| History | Move files with `git mv` so Git records renames. ADR numbers and recorded evidence stay unchanged; only link paths inside them changed. |
| Link check | `Test-Repository.ps1` requires the new paths and keeps every earlier check. It now also fails a Markdown link whose case differs from the tracked path, because Windows accepts such a link and Linux and GitHub do not. |
| Release documents | Add draft release notes, a demo narration script, and interview notes under `docs/releases/`. |

The application, tests, packages, and CI workflow are unchanged; no code or workflow referred to a moved file.

## Consequences

- Links to the old paths from outside the repository, such as bookmarks to `docs/development.md`, stop working.
- The unit-test project still contains no tests; the test documentation says so.
- The ignored local agent files stay where they were; M9 updated their links.
