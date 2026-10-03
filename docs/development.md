# Development setup

## Current repository

Contributors can run repository checks with Git and PowerShell 7. The solution contains unit, integration, and E2E test projects. The developer has restored their packages and built Release with zero warnings and errors. The web application, database migration, and application test cases remain pending.

The owner has installed .NET SDK 10.0.401. `global.json` selects that SDK feature band and allows later patches. Contributors need a [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), not a runtime alone.

```powershell
git --version
pwsh --version
dotnet --list-sdks
pwsh -NoProfile -File scripts/Test-Repository.ps1
pwsh -NoProfile -File scripts/Test-Dependencies.ps1
dotnet build Stockroom.slnx --configuration Release --no-restore
```

## First application milestone

The developer will:

1. Verify the .NET SDK against the existing `global.json`.
2. Add `src/Stockroom.Web/` to the existing `Stockroom.slnx`.
3. Add references from the relevant test projects to the web project.
4. Configure SQLite, Identity, and the initial migration. Align EF Core package versions with the target framework.
5. Provide Development-only seed and reset commands. Obtain demo passwords through environment variables or user secrets.
6. Document the exact migration, seed, run, and test commands after executing them.
7. Add application test execution to CI after implementing cases. Preserve the locked dependency audit and scaffold build.

Keep source under `src/Stockroom.Web/` and tests under `tests/`. Do not add placeholder commands to the README as working setup instructions.

## Dependency management

Use [tests/README.md](../tests/README.md) for project responsibilities. `Directory.Packages.props` contains direct package versions, and each project has a `packages.lock.json`. `Directory.Build.props` enables NuGet Audit for direct and transitive packages at low severity and above. `NuGet.Config` uses the official package and audit sources.

To change packages, edit the central versions, run `dotnet restore Stockroom.slnx --force-evaluate`, inspect lock-file changes, then run `scripts/Test-Dependencies.ps1`. The script restores in locked mode and fails on source errors or advisories. A fresh checkout needs network access to NuGet and its audit feed. Build after changing versions.

## Repository checks and CI

`scripts/Test-Repository.ps1` checks required project documents, UTF-8 text, LF line endings, trailing whitespace, final newlines, and local Markdown file targets among tracked files. NuGet generates lock files with platform-specific line endings and no final newline, so the checker exempts those files from these two formatting checks. Git normalizes their committed line endings. The checker does not inspect remote URLs, Markdown anchors, or prose quality.

The [CI workflow](../.github/workflows/ci.yml) runs this script on Ubuntu with PowerShell 7. It also configures the SDK, restores and audits locked dependencies, and builds the test foundation. It starts on pushes to `main`, pull requests targeting `main`, and manual dispatch. It has read access to repository contents and does not deploy.

The checkout tracks the owner's Stockroom repository on GitHub. The developer has not verified the hosted workflow results. Run the same checks on the local checkout and inspect GitHub Actions before relying on a hosted result.

After M1, add application test execution and include permission and SQLite integrity cases. Configure browser installation and the running host before enabling E2E CI. A scaffold build proves no business behavior, and the dependency audit covers known NuGet advisories rather than downloaded browser binaries.

References: [NuGet audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages), [GitHub Actions syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax), and [SDK setup](https://github.com/actions/setup-dotnet).
