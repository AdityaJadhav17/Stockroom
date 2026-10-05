# Architecture diagrams

These diagrams describe Stockroom's committed source at [0f30ae2](https://github.com/AdityaJadhav17/Stockroom/tree/0f30ae226eed742bbb5a989898279db2bdac2cae). Source links on each diagram open the corresponding code at that revision.

| Diagram | Contents | Source |
| --- | --- | --- |
| [Runtime architecture](runtime-architecture.html) | Request handling, authorization, services, persistence, and error handling | [JSON](runtime-architecture.json) |
| [Data design](data-design.html) | Records, foreign keys, and database constraints | [JSON](data-design.json) |
| [Purchase lifecycle](purchase-lifecycle.html) | Creation, approval, rejection, and receipt; stock issues are a separate operation | [JSON](purchase-lifecycle.json) |
| [CI workflow](ci-workflow.html) | Job dependencies, the integration-test matrix, browser tests, and the final status gate | [JSON](ci-workflow.json) |

## Viewing

After cloning the repository, open an HTML file from this directory in a browser. Each file contains its viewer and diagram, supports light and dark themes, and requires no server or package installation. Following a source link requires an internet connection. GitHub's file view displays the HTML source; download the file to view the interactive diagram.

## Maintenance

The JSON files are the editable diagram sources. When application structure or CI changes, review the affected diagram, update its source references and pinned revision, and regenerate the HTML with Archify. Update this page to identify the reviewed revision. The diagrams record source structure; they do not establish deployment status or GitHub branch-protection settings.

These files were generated with Archify 3.0.1. Each diagram passed showcase validation, delivery and provenance checks, and browser checks. The layouts were visually reviewed in light and dark themes before publication. Preserve the license notices embedded in the generated HTML. Generation receipts, browser captures, and scratch files remain local under `.archify/`.
