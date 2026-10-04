# Accessibility

Stockroom aims for keyboard access, labelled controls, announced errors, and readable tables. The owner has not commissioned a conformance audit, so this page makes no WCAG conformance claim. It lists the behavior that tests and manual checks have verified.

## Verified behavior

| Area | Behavior | Evidence |
| --- | --- | --- |
| Keyboard | A skip link is the first keyboard stop. Every action is a native link, button, or form control with a visible focus ring. | `ResponsiveLayoutTests.SkipLinkIsTheFirstKeyboardStopAndNavigationMarksTheSection`; manual keyboard walkthrough in M8 |
| Focus visibility | From 56rem, the sticky header keeps keyboard focus and anchor targets below it (WCAG 2.4.11). Narrower screens use a header that scrolls away. | `ResponsiveLayoutTests.StickyHeaderNeverCoversFocusOrAnchorTargets` |
| Forms | Every field has a visible label and a hint linked with `aria-describedby`. After a failed submission, the summary in a `role="alert"` region lists the errors, invalid fields carry `aria-invalid="true"`, and the form keeps the values you entered. | [ADR 0006](../decisions/0006-m6-release-preparation.md); [ADR 0008](../decisions/0008-m8-ui-polish.md); manual checks in M8 |
| Status | Request statuses and stock levels show text (Pending, Approved, Rejected, Received, Low, OK). Colour repeats the text and never replaces it. | [ADR 0008](../decisions/0008-m8-ui-polish.md) |
| Tables | Each table takes its accessible name from the heading above it. A wide table scrolls inside a focusable frame with the same name. | [ADR 0006](../decisions/0006-m6-release-preparation.md); `ResponsiveLayoutTests.PagesFitAPhoneWidthWithoutSidewaysScrolling` |
| Navigation | The current section carries `aria-current="page"`, a raised segment, and bold text. | `ResponsiveLayoutTests` |
| Contrast | Text, badges, and default and hover button states reach at least 4.5:1 in light and dark themes, calculated from the colour tokens. | [ADR 0008](../decisions/0008-m8-ui-polish.md) |
| Preferences | The interface follows `prefers-color-scheme`, `prefers-reduced-motion`, `prefers-reduced-transparency`, and `prefers-contrast`. | `wwwroot/css/site.css`; dark mode reviewed from screenshots |

## Known gaps

- No automated audit tool or screen-reader session has checked the interface.
- On phones, list tables become cards by changing their CSS display. Chromium and Firefox keep the table semantics; older Safari releases may not.
- A blank rejection reason appears in the page alert after the redirect, not beside the field.
- The browser tests run in light mode only.

## Report a barrier

Open a bug report on GitHub. Name the page, browser, and assistive technology, and list the steps that reproduce the problem.
