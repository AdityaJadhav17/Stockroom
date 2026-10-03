# Test fixtures

Keep synthetic-data builders and common fixture conventions here during implementation. Add a shared fixture project after two test projects need the same executable code.

- Create ten fictional items and accounts from the requirements. Obtain passwords from test configuration.
- Record opening stock through movements and maintain consistent request histories.
- Use a separate database for each independent case. Share a file between connections only for a competing-write test.
- Dispose connections and remove only the temporary database created by that fixture.
- Keep generated databases, browser traces, credentials, and reports outside Git.

Current status: fixture conventions only; no shared code or customer data.
