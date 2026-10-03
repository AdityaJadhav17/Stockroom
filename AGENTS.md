# Contributor guidance

Read [docs/mvp.md](docs/mvp.md), [docs/user-stories.md](docs/user-stories.md), and [docs/architecture.md](docs/architecture.md) before implementing a feature. Use [docs/claude-handoff.md](docs/claude-handoff.md) for the first development session.

## Scope and implementation

- Implement one milestone from [docs/delivery-plan.md](docs/delivery-plan.md) at a time. Record the evidence before marking a story complete.
- Keep the planned single web application and SQLite database. Record a design decision in `docs/decisions/` if you change the stack or business rules.
- Keep business rules in C# services. Enforce permissions in server-side handlers and services; hiding a button does not provide authorization.
- Save stock changes and their history in one database transaction. Use database conditions to protect stock quantities and request transitions during competing requests.
- Test database behavior against SQLite. An in-memory substitute cannot prove SQLite transaction behavior.
- Report commands, results, and remaining work. Separate a successful repository check from an application build or a passing business test.
- Keep changes inside this repository. Preserve work from other contributors and coordinate edits to shared files.

## Documentation

Follow [docs/writing-guide.md](docs/writing-guide.md). Update the requirements and tests alongside behavior changes. Keep proposed behavior distinct from implemented behavior. Use synthetic examples and measured results; do not invent customers, deployments, usage figures, or benchmarks.

## Checks

Run `pwsh -NoProfile -File scripts/Test-Repository.ps1` after documentation changes. Run `pwsh -NoProfile -File scripts/Test-Dependencies.ps1` after dependency changes, then build the solution. Preserve central package versions and lock files. Add application test execution to CI after implementing cases; the current scaffold build proves no business behavior. Follow [docs/definition-of-done.md](docs/definition-of-done.md) before describing the MVP as complete.
