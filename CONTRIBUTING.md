# Contributing

## Start a change

Read the MVP and the acceptance criteria for the story you intend to implement. Create a branch such as `feat/login-and-demo-accounts` or `fix/duplicate-receipt`. Keep a change small enough for a reviewer to run and inspect.

Use [docs/decisions/0001-application-foundation.md](docs/decisions/0001-application-foundation.md) as the format for architecture decisions. Record the reason, alternatives, and consequences before changing the planned architecture.

## Develop and verify

Keep business rules in services and exercise user workflows through the web application. Add tests for behavior and failure cases. Run the repository check and the application checks available at that milestone. Include the command and result in the review notes.

Use the existing solution and test scaffolds described in [docs/development.md](docs/development.md). Before the first application commit, add the web project, test references, and runnable application instructions.

For package changes, update `Directory.Packages.props`, regenerate the affected lock files through restore, and run `scripts/Test-Dependencies.ps1`. Include the audit result in review notes. Do not suppress advisories to obtain a passing check.

## Submit a change

Use the pull request template to describe the user-visible behavior, related story, and verification. List unresolved issues with a reproduction procedure. Update the README if setup instructions or implementation status change.

Write documentation and review notes in concrete, professional language. Distinguish planned behavior from verified results and check that links resolve. The owner reviews the result against the story acceptance criteria and the [definition of done](docs/definition-of-done.md).

Follow [the code of conduct](CODE_OF_CONDUCT.md) during discussions and reviews. Report vulnerabilities through [SECURITY.md](SECURITY.md), and use [SUPPORT.md](SUPPORT.md) for setup questions. Contributors submit their work under the repository's [MIT license](LICENSE).
