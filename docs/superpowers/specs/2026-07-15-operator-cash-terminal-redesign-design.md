# Operator Cash Terminal Redesign

**Date:** 2026-07-15
**Status:** Approved for implementation planning

## Goal

Turn the Operator App `Cashier` shift and journal surfaces into a cohesive,
dense cash terminal for repeated cashier and manager work. The redesign must
make the current financial state scannable in seconds, keep detailed records
easy to search, and put the selected operation, receipt, shift, or approval in
a stable inspector instead of expanding rows and moving the surrounding UI.

This is a substantial information-architecture and presentation change inside
the existing `Cashier` workspace. Existing authoritative backend commands,
financial rules, immutable records, idempotency, and permission checks remain
unchanged.

## Product Decisions

- The chosen direction is an operational cash terminal, not a card-heavy
  dashboard and not an accountant-only spreadsheet.
- The top cash header remains the persistent anchor for shift status, cash in
  hand, revenue, cash movement commands, shift closure, and the X report.
- The primary tabs remain `Sales`, `Shift`, and `Cash journal`.
- `Shift` answers “is the active cash position healthy and ready to close?”
  while `Cash journal` answers “what exactly happened and who must act?”.
- Journal content uses a master-detail layout: a dense register is primary and
  a stable right inspector owns context and actions.
- The journal segment currently labelled `Review` is renamed `Approvals` in
  user-facing copy because it is an anti-fraud approval and audit surface, not
  receipt verification.
- Permission filtering remains invisible-by-default. Receipt-only staff must be
  able to reach `Cash journal > Receipts`; journal visibility must therefore
  include receipt view/refund permissions as well as shift/report/approval
  permissions.
- The redesign uses only existing production APIs. Missing fields render as an
  em dash or remove the optional column; production UI must not invent data.
- Preview fixtures must cover the same visible states as production contracts,
  including real receipt detail, approvals, and audit rows.

## Approaches Considered

### Recommended: operational cash terminal

A compact shift command screen plus master-detail registers optimizes the
common cashier loop: scan state, locate a record, inspect it, then perform an
authorized action without losing position. It matches the Operator App target
of serious, dense operator software.

### Rejected: financial dashboard

Large KPI cards and charts look polished but consume first-viewport space and
force detailed work into modals. They are slower for repeated journal and
receipt operations.

### Rejected: accountant register everywhere

Table-first screens are strong for audit and export but under-serve the cashier
who needs immediate shift health, reconciliation, and guarded actions.

## Shared Workspace Anatomy

All four redesigned states use the same visual grammar:

```text
+ Context and key metrics -----------------------------------------------+
+ Primary register or operational flow --------+ Selected-item inspector +
| table, movement feed, or shift history        | context and actions     |
+-----------------------------------------------+-------------------------+
```

- The cash header and primary tabs keep their current shell position.
- Metric strips are compact, aligned, and limited to decision-relevant values.
- Registers use stable row height, tabular numbers, explicit selected state,
  restrained dividers, and no hover transform or layout shift.
- Search, filters, date range, and export share one compact toolbar.
- The inspector remains visible beside the register at normal desktop widths.
  At narrower supported widths it becomes a right drawer while the register
  remains primary.
- Green means confirmed positive or reconciled state; red means outflow,
  shortage, or destructive impact; amber means attention is required. Color is
  never the only status signal.
- Loading skeletons preserve the final register and inspector geometry. Errors
  remain local to the failed area and provide retry. Empty states explain the
  active filter or missing prerequisite.

## Shift Screen

### Summary strip

The first row contains four compact values:

- shift revenue;
- cash currently expected in the drawer;
- expected close amount;
- current reconciliation state, or `Calculated at close` while counted cash is
  unavailable.

The strip must not duplicate the same value with competing hero treatments.

### Operational body

The first viewport uses a roughly 60/40 split:

- **Revenue and inflow:** total revenue, time, goods, cash, non-cash, and wallet
  top-ups in a compact breakdown with light proportional indicators rather than
  a heavy charting dependency.
- **Reconciliation:** starting cash, cash sales, cash movements, expected cash,
  counted cash, and difference. The formula is explained in-place as starting
  cash + cash sales + cash in - cash out - cash refunds. The close/reconcile
  action is visually attached to this control block.

Below that split:

- recent cash movements show time, type, reason, available actor, and amount;
- export moves into one compact menu or action cluster instead of occupying a
  full content card;
- shift history becomes a selectable register with date, operator when
  available, revenue, cash result, difference, and state;
- selecting a historical shift opens a read-only summary in the inspector with
  print/export actions backed by existing report data.

When no shift is open, the screen shows the last close summary and an explicit
open-shift action only for authorized staff. It does not render a mostly empty
grid.

## Cash Operations Register

The operations segment becomes a master-detail register.

The primary table contains the supported subset of:

- time;
- operation type;
- reason;
- actor when supplied by the report;
- related receipt or shift when supplied;
- signed amount;
- resulting cash balance when supplied.

The toolbar provides text search, operation type, available period controls,
available staff filtering, reset, result count, and CSV export. Filters that
cannot be represented by the existing endpoint remain client-side over the
loaded report or are omitted; the UI must not suggest server-wide search when
only the loaded page is searched.

The selected-operation inspector shows exact timestamp, type, amount, reason,
actor, relationships, and support identifiers that the contract actually
returns. It is read-only.

## Receipts Register

The receipt list and selected receipt share the viewport instead of expanding
detail below the list.

The register shows receipt/sale number when available, time, state, compact
line summary, payment information when available, and total. Paid, refunded,
voided, and future partial-refund states use explicit text and restrained tone.

The inspector shows:

- receipt number, sale state, timestamp, and total;
- product lines with quantity, unit price, and line total;
- payment split when returned by the sale contract, including mixed payment;
- receipt/refund history when returned;
- print and text export;
- guarded refund action only when the selected sale is paid and the session has
  `pos.sales.refund`.

Refund remains a critical backend-confirmed action. The confirmation collects a
non-empty reason and states the financial impact. The selected row remains
stable while the command is pending, then the report and detail reload after
confirmation.

The inspector never presents an empty object as a successful zero-value sale.
Until valid detail exists it shows a skeleton, a local error with retry, or a
clear unavailable state.

## Approvals And Audit

The journal segment label becomes `Approvals`. Its internal navigation becomes:

- `Pending decisions`;
- `Decision history`;
- `Operation audit`.

Pending decisions use a register plus inspector. Each row exposes action type,
amount, requester, age/expiry, and risk state. The inspector provides the full
reason and timestamps plus approve and reject actions. Rejection requires a
reason. Expiring and expired requests use both text and tone.

`Decision history` reuses available money-action or audit records; if the
existing API cannot distinguish a durable decision-history dataset, this view
uses an explicitly filtered audit result rather than a fake local history.
`Operation audit` keeps staff and amount filters, adds a result summary, and
uses the same register/inspector language as cash operations.

All decisions wait for backend confirmation. Successful decisions reload the
queue and retain a clear confirmation toast; failures preserve the selected
request and entered rejection reason where safe.

## Permissions

- `Shift` visibility continues to derive from shift open/view/close/cash and
  report permissions.
- `Cash journal` visibility includes cash-operation/report permissions,
  approval permission, `receipts.view`, and `pos.sales.refund`.
- Journal segments are omitted when the session lacks their relevant
  permission; inaccessible segments are not disabled advertisements.
- Direct state changes and commands still perform their existing permission
  checks. Navigation visibility is not an authorization boundary.

## Data And State Flow

- Independent summary, register, and detail requests start in parallel when
  their inputs are known; selecting a row triggers only the relevant detail
  request.
- Late detail responses are ignored after selection, branch, session, or filter
  changes.
- Selected IDs are stored as primitive state; selected records are derived from
  the current result set.
- A filter change clears selection only when the selected record is no longer
  present. Otherwise the inspector remains stable.
- Backend-confirmed mutations increment the relevant refresh token and reload
  only affected summary/register/detail regions.
- Existing idempotency keys, authoritative money responses, error projection,
  toasts, and critical confirmations are reused.

## Keyboard And Responsive Behavior

- Tab order follows toolbar, register, inspector, then actions.
- Register rows are keyboard focusable; arrow keys move selection and Enter
  opens/focuses the inspector.
- Escape closes the responsive inspector drawer or critical confirmation but
  never silently cancels an in-flight command.
- The primary supported desktop validation sizes are 1440x900 and 1280x720.
- Below the side-by-side threshold, the inspector becomes a right drawer. The
  register does not collapse into unrelated cards.
- All amounts remain tabular and all primary actions remain visible without
  accidental horizontal scrolling at the supported sizes.

## Preview Fidelity

The development mock backend must provide:

- sale detail for every fixture report row;
- platform receipt detail and payment breakdown;
- at least one paid and one refunded receipt;
- at least one pending approval, one decided action, and audit rows;
- exact response shapes used by the typed clients.

Mock authentication remains local. Preview realtime must not emit unhandled
SignalR negotiation errors when it intentionally runs without a realtime
backend; the preview either uses a no-op connection or handles the unavailable
connection as a represented offline state.

## Testing And Verification

Follow TDD and observe each focused test fail for the intended reason before
production changes.

- View-model/model tests cover summaries, reconciliation display, filters,
  states, permissions, selection retention, and optional fields.
- Component tests cover all four redesigned states, loading/error/empty states,
  master-detail selection, keyboard behavior, narrow inspector drawer, receipt
  actions, approval/rejection, and backend-confirmed refresh.
- App integration tests prove receipt-only access reaches the receipt segment
  and unavailable journal segments remain absent.
- Preview fixture tests cover sale detail, receipt detail, approvals, audit, and
  safe realtime behavior.
- Run all focused cash/review/App tests, then the complete Operator Web test
  suite and production build because shared cash navigation, styling, preview,
  and review composition change.
- Perform Playwright visual and interaction QA at 1440x900 and 1280x720 in dark
  and light themes. Verify shift scanning, operation filtering/selection,
  receipt detail/refund confirmation, approval decision, audit filtering,
  keyboard focus, overflow, contrast, and console health.

## Out Of Scope

- Backend financial rules, migrations, or new report contracts.
- Country-specific fiscal-device integration.
- New charting dependencies.
- Replacing the existing `Sales` workspace.
- Custom role editing.
- Mobile-phone layouts; the Operator App remains a native Windows workstation
  surface.
