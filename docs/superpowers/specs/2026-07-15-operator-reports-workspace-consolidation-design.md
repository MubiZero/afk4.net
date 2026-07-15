# Operator Reports And Workspace Consolidation Design

**Status:** approved for implementation
**Date:** 2026-07-15

## Goal

Turn `Отчёты` into a compact manager-first reporting center and remove the
overlap between reports, the cash journal, approvals, stock history, and audit
logs.

The product rule is:

> Perform work in its domain, compare periods in Reports, and prove what
> happened in Events.

This design preserves every MVP reporting and anti-fraud capability. It changes
where those capabilities live and removes screens that expose the same data in
several different hierarchies.

## Approved Direction

The selected visual reference is the dark, calm, exception-first concept headed
`День под контролем`:

- `Сводка`, `Смены и касса`, and `Выручка` are the only report tabs;
- the default period is the current branch day;
- the page leads with a plain-language status and only the exceptions that need
  attention;
- three compact figures and a seven-day revenue trend provide context without
  turning the screen into a dashboard wall;
- the active shift is a single contextual row, not a second shift-management
  screen;
- dark theme is the primary visual reference, while the existing token-based
  light theme remains supported.

The existing Operator shell, rail, typography, spacing scale, controls, and
status footer remain the design system. This is a redesign of information
hierarchy and workspace composition, not a new visual language.

## Workspace Ownership

| Workspace | Owns | Does not own |
| --- | --- | --- |
| `Касса` | Performing current sales, current-shift cash work, receipt actions, and refunds | Historical period comparison, audit search, a separate approvals inbox |
| `Отчёты` | Reading and comparing branch-day, shift, cash, and revenue aggregates | Executing money actions, managing the active shift, operator-action audit |
| `События` | Immutable evidence of who did what, to which object, when, and with what result | Financial totals, operational action forms, diagnostics dashboards |
| `Склад` | Current stock work and domain-specific stock history | General audit events or branch financial reporting |

The same record may be linked from more than one workspace, but it has only one
canonical detail view. For example, Reports may link to a receipt in `Касса →
Чеки`; it must not recreate receipt actions inside Reports.

## Information Architecture

### Cash

`Касса` contains exactly three tabs:

1. `Продажа` — current POS sale and payment flow.
2. `Смена` — current shift status, drawer reconciliation, cash operations,
   opening and closing actions.
3. `Чеки` — receipt search, receipt detail, permitted refund and void actions.

Remove `Касса → Журнал` and its nested `Операции / Чеки / Согласования`
segments. Current-shift cash operations move into `Смена`; receipt history is
already owned by `Чеки`.

### Reports

`Отчёты` contains exactly three tabs:

#### Summary (`Сводка`)

The default screen answers one question: does anything from the selected day
need a manager's attention?

- Default period: `Сегодня` in the branch timezone.
- Status: `День под контролем` when there are no blocking exceptions, otherwise
  a count such as `1 отклонение требует проверки`.
- Attention list: unreconciled or materially discrepant closed shifts, failed
  critical money operations, and stale close/reconciliation states. Show at
  most three rows, followed by `Ещё N`, so normal days stay calm.
- Compact figures: net revenue, gameplay time, and net POS sales for the chosen
  day.
- Trend: net revenue for the latest seven branch days, including the selected
  day.
- Active-shift context: shift number, start time, operator, and a link to
  `Касса → Смена`. It is labelled as live/provisional and is never presented as
  a closed reconciliation result.

If no attention item exists, the empty space stays empty; the UI does not fill
it with secondary cards or invented tasks.

#### Shifts And Cash (`Смены и касса`)

This tab is for historical accountability:

- period and operator filters;
- closed-shift list with opening/closing staff, expected cash, counted cash,
  discrepancy, cash operations, refunds, corrections, and close notes;
- one stable shift inspector rather than nested journal tabs;
- links to canonical receipts and event evidence;
- export of the currently filtered data.

An active shift may appear as a clearly marked provisional context row. All
open/close/cash-operation commands remain in `Касса → Смена`.

#### Revenue (`Выручка`)

This tab combines the old sales and gameplay reports into one revenue story:

- gross revenue, refunds, and net revenue;
- gameplay revenue and gameplay hours;
- POS goods/services revenue;
- breakdown by source, payment method, operator, and chosen period;
- comparison with the previous equivalent period;
- export of the currently filtered data.

Wallet top-ups, debt payments, opening cash, and cash deposits/withdrawals are
cash flows, not revenue. They appear in `Смены и касса`, never as revenue.

There is no separate gameplay report tab and no separate cash-operations report
tab. Their MVP capabilities remain available inside `Выручка` and `Смены и
касса` respectively.

### Events

Rename `Журнал`/`Логи` to `События`. It is one feed, not another hierarchy of
tabs.

The feed has four combinable category filters:

- `Персонал`
- `Касса`
- `ПК`
- `Система`

Period, actor, object, action, result, and text search refine the same feed. An
event inspector shows the immutable event details, before/after values where
available, source application, device/IP context, and links back to the
canonical domain object.

Operator-action reporting is fulfilled here, including filtered export. It is
not duplicated in `Отчёты`.

### Stock

`Склад` contains:

1. `Остатки`
2. `Приёмка`
3. `История`
4. `Инвентаризация`

`История` is stock-domain history. The label `Движения` is not used.

## Financial Definitions

All totals are backend-authoritative and use branch-timezone day boundaries.
The frontend must not recompute financial truth from cached rows.

- **Gross revenue:** settled gameplay charges plus settled POS goods/services
  sales before refunds.
- **Refunds:** value of completed reversing entries in the selected period.
- **Net revenue:** gross revenue minus refunds.
- **Gameplay time:** completed plus currently accrued authoritative session time
  for the selected period, with live values marked provisional.
- **POS sales:** settled goods/services sales minus their completed refunds.
- **Cash discrepancy:** counted cash minus expected cash for a closed shift. No
  discrepancy is claimed for an open shift.

Exports use the same period, timezone, permissions, filters, definitions, and
provisional/closed-state labels as the visible screen.

## Inline Secondary Confirmation

The separate approvals queue and approval workspace are removed. Dual control
remains mandatory for high-risk actions covered by the existing branch policy,
including over-threshold refunds, manual corrections, debt write-offs, comps,
and material shift discrepancies.

The interaction is contextual:

1. The signed-in operator starts the action in its canonical domain screen and
   enters the required reason.
2. The backend evaluates permissions, caps, thresholds, and current financial
   state. If a second person is required, it returns a short-lived,
   action-specific confirmation challenge with no ledger effect.
3. The native Operator host opens a secure confirmation dialog summarizing the
   action, amount, requester, and reason. A different manager enters their
   existing AFK4 account login and password. There is no separate approval PIN
   or second identity system.
4. The backend authenticates that account, requires the branch-scoped
   `billing.money_action.approve` permission, rejects self-confirmation, verifies
   that the challenge and source object are unchanged, and executes the
   original command atomically.
5. The result records both requester and confirmer and becomes visible in
   `События`.

The manager credentials are owned by the native host dialog: they never enter
the WebView DOM, React state, local storage, logs, analytics, or the primary
operator's protected session snapshot. Secondary confirmation does not switch
the signed-in operator.

The challenge is single-use, bound to branch, requester, action type, target,
amount, reason/payload fingerprint, and idempotency key, and expires after a
configurable short interval (five minutes by default). It is not a reusable
manager token.

If no manager is present, the operator cancels the dialog and nothing executes.
The challenge expires without becoming work in a hidden queue. A manager can
later perform the action again from the canonical receipt, client, session, or
shift screen.

## Permissions And Visibility

- `Отчёты` is visible only with report-view permission; its server results are
  filtered to the authenticated branch and permission set.
- Each Cash tab is visible from the actions the staff member can actually
  perform or inspect. Receipt-only staff retain direct access to `Чеки`.
- `События` uses explicit audit/event permissions. Sensitive before/after data
  is filtered by the backend, not merely hidden by the client.
- Secondary confirmation requires a different active staff account in the same
  branch with `billing.money_action.approve`.
- Navigation never renders permanently unauthorized sections as disabled
  promises. Temporarily unavailable actions explain the current state.

## Backend And Data Boundaries

Reports are read models, not a new write authority. The Reports module composes
explicit read contracts from Shifts, Billing, POS, Sessions, and Audit without
directly mutating their tables.

The UI needs three stable report projections:

- summary status, attention items, figures, trend, and active-shift context;
- paged shift/cash list plus shift detail;
- revenue totals, comparison, and breakdowns.

Existing report endpoints may back the first implementation where their
definitions match this spec. A dedicated aggregate endpoint is preferred for
Summary so the client does not join financial truth or infer exception status.

The inline-confirmation path may reuse the existing money-action request and
audit persistence internally, but it must not expose durable actionable
`pending` items after cancellation or expiry. Execution remains idempotent and
passes through the existing billing/refund/shift rules.

## Failure And Empty States

- Backend or network unavailable: reports retain a labelled last-successful
  view where safe, but every critical money action remains blocked.
- Invalid manager credentials: keep the action unexecuted, show a local error,
  and apply normal authentication throttling/lockout policy.
- Same requester and confirmer: reject explicitly; do not silently retry under
  the primary session.
- Confirmer lacks permission or branch access: reject without revealing extra
  account details.
- Expired or changed challenge: close the confirmation attempt, refresh the
  source object, and require the operator to review and submit again.
- Concurrent refund/correction conflict: preserve zero or one authoritative
  effect through the original idempotency key; refresh the canonical object.
- Export failure: leave the report usable and show a retryable error without
  clearing filters.
- No report rows: explain the selected period and provide no fabricated zeros
  where data is unavailable.

All failed privileged attempts are audit-visible according to the existing
security policy.

## Interaction And Accessibility

- Keyboard order follows period controls, tabs, attention rows, figures, then
  chart and active-shift context.
- Selecting an attention row opens its inspector; `Enter` and `Space` mirror
  pointer activation.
- Status, discrepancy, and result never rely on color alone.
- The trend has exact-value tooltips and an accessible tabular alternative.
- Dense rows retain visible focus, readable type, and the existing Operator
  contrast standards in both themes.
- Loading preserves layout; reduced-motion mode removes nonessential value and
  chart transitions.
- At narrower desktop widths the inspector stacks below the list. No critical
  action is hidden behind horizontal scrolling.

## Testing And Acceptance

Implementation follows TDD for financial definitions, authorization, endpoint
behaviour, ViewModels/projection models, and secondary-confirmation rules.

Required coverage:

- navigation ownership and permission-derived visibility;
- branch-timezone boundaries and revenue classification, including proof that
  wallet top-ups and debt payments are excluded from revenue;
- summary attention rules and provisional active-shift labelling;
- filtered exports matching visible report definitions;
- no self-confirmation, permission and branch checks, challenge binding and
  expiry, credential non-persistence, idempotent double-submit, and concurrent
  financial-state changes;
- canonical links between Reports, Cash, Stock, and Events;
- keyboard, focus, reduced-motion, chart alternative, and screen-reader labels;
- rendered dark/light QA at 1920, 1440, 1280, and the existing narrow stacked
  breakpoint, with no overflow or console/page errors;
- Windows native-host evidence for the secure secondary-account dialog at 100%
  and 125% scaling.

The change is accepted when a manager can identify whether today needs
attention from Summary, inspect a historical shift or revenue period without
entering Cash, trace every critical action through Events, and complete a
high-risk action with a present second manager without any approvals inbox.

## Delivery Slices

Use one implementation plan with three independently verifiable slices:

1. **Ownership and navigation:** rename Events; simplify Cash and Stock tabs;
   preserve canonical links and permissions.
2. **Reports center:** add the three report projections, exports, and the
   selected Summary composition.
3. **Inline confirmation:** replace the approvals workspace with the secure
   native secondary-account challenge flow, then remove obsolete queue UI only
   after backend and Windows-host verification passes.

The approvals UI must not be removed before the replacement confirmation path
is working end to end; otherwise existing high-risk actions would dead-end.

## Non-Goals

- No browser-based club admin replacement for the native Operator App.
- No custom report builder, saved report layouts, forecasting, accounting
  ledger editor, or cross-branch owner BI in this slice.
- No new approval PIN, badge, biometric flow, or manager session switch.
- No weakening of thresholds, caps, no-self-confirmation, audit, ledger
  immutability, backend confirmation, or idempotency.
- No general-purpose diagnostics inside Events; operational diagnostics remain
  under management/settings surfaces.
