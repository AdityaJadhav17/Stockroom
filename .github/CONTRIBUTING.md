# Contributing

## Start a change

Read the [MVP](../docs/planning/mvp.md) and the acceptance criteria for the story you plan to change. Create a branch such as `feat/partial-receipts` or `fix/duplicate-receipt`, and keep the change small enough for a reviewer to run.

Record architecture changes as a numbered decision in [docs/decisions](../docs/decisions/0001-application-foundation.md). State the reason, the alternatives, and the consequences.

## Develop and verify

Keep business rules in the C# services and enforce permissions on the server. Add tests for the behavior and its failure cases, and put database behavior in the SQLite integration tests. The [setup guide](../docs/development/setup.md) lists the commands; include each command and its result in the pull request.

For a package change, update `Directory.Packages.props`, restore to regenerate the lock files, and run `scripts/Test-Dependencies.ps1`. Fix advisories rather than suppressing them.

## Submit a change

Fill in the pull request template with the user-visible behavior, the related story, and your verification. List open issues with steps to reproduce them. Update the README when setup or behavior changes.

Write documentation in plain language, keep planned behavior separate from verified results, and check that links resolve. The owner reviews the change against the story's acceptance criteria and the [definition of done](../docs/planning/definition-of-done.md).

Follow [the code of conduct](CODE_OF_CONDUCT.md). Report vulnerabilities through [the security policy](SECURITY.md) and ask setup questions as described in [support](SUPPORT.md). You submit your work under the repository's [MIT license](../LICENSE).
