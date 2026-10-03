# Definition of done

The developer completes the story checklist for each change. The owner completes the release checklist before marking the MVP complete. At initialization, the application criteria below remain unchecked.

## Story completion

- [ ] Implement the linked story's acceptance criteria and business rules.
- [ ] Verify input validation and server-side authorization for the affected actions.
- [ ] Exercise the normal workflow and the relevant failure cases.
- [ ] Add a regression test for a behavior fix or a critical invariant.
- [ ] Run applicable repository, build, and test commands; record results in the review notes.
- [ ] Update affected documentation and the backlog evidence column.
- [ ] Have a reviewer inspect the change and resolve defects that break acceptance criteria.

## MVP release

- [ ] Complete US-01 through US-08 and link their verification evidence.
- [ ] Restore, build, and test from a fresh checkout using the documented SDK.
- [ ] Create the schema through migrations and run the Development seed command.
- [ ] Confirm seed reruns preserve existing counts and quantities.
- [ ] Demonstrate purchase approval, receipt, withdrawal, and history.
- [ ] Prove that members cannot perform manager actions or read another member's request.
- [ ] Prove that repeated or competing receipts increase stock once.
- [ ] Prove that competing withdrawals cannot make stock negative.
- [ ] Prove that a failed history write rolls back its stock or status change.
- [ ] Confirm committed data survives application restart.
- [ ] Check forms, field errors, empty states, and keyboard navigation.
- [ ] Run application CI after publishing to GitHub and record the result.
- [ ] Document demo credentials setup without committing passwords or local databases.
- [ ] Update the README with working setup commands, implementation status, screenshots, and known limitations.
- [ ] Record a two- to three-minute demonstration and identify the data as synthetic.
- [ ] Record remaining defects and deferred scope without marking them complete.

## Evidence format

For a story, record the commit or pull request, test IDs, command, and result. For the release, retain a fresh-checkout setup result and the demo reference. A passing repository check covers document and file hygiene; it does not prove application behavior.
