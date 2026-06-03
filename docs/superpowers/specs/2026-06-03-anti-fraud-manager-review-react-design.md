# §5.5 React Manager Review screen — design

**Spec:** anti-fraud-controls-design §5.5 (reviewability), React/front-end slice only.
**Branch:** `sp4-tier-0`. **Surface:** `src/AFK4.Operator.App.Web`.
**Status of backend:** done. Pending-approval feed, approve/reject, and audit/report
actor+amount filters all exist server-side. This spec covers ONLY the React UI.

## Goal

Give Owner / BranchManager a dedicated screen to:
1. Review the queue of pending high-risk money actions and **approve / reject** each.
2. Browse the high-risk audit trail with **actor** and **amount** filters.

This is the last open piece of §5.5 (both backend sides — audit search and report —
are already shipped).

## Existing backend contract (already in place)

Endpoints (Program.cs):
- `GET  /api/branches/{id}/money-actions` → `MoneyActionRequestListResponse { Requests: MoneyActionRequestDto[] }` (pending feed; `ApproveMoneyAction` perm).
- `POST /api/branches/{id}/money-actions/{requestId}/approve` — body `MoneyActionDecisionRequest { DecisionReason? }`. 403 if approver == requester; 409 if not-pending / expired; 422 on cap reject.
- `POST /api/branches/{id}/money-actions/{requestId}/reject` — body `MoneyActionDecisionRequest { DecisionReason? }`.
- `GET  /api/branches/{id}/audit` — already supports `actorStaffUserId`, `minAmount`, `maxAmount` query params (amount-bounded query EXCLUDES null-amount records).
- `GET  /api/branches/{id}/staff` → `StaffUserDto[]` (roster, for Guid→name + actor picker).

Contracts (Shared.Contracts/Billing/MoneyActionContracts.cs):
`MoneyActionRequestDto(MoneyActionRequestId, OrganizationId, BranchId, ShiftId, ActionType,
RequestedByStaffUserId, AmountMinorUnits, CurrencyCode, Reason, State, CreatedAtUtc, ExpiresAtUtc)`,
`MoneyActionRequestListResponse(Requests)`, `MoneyActionDecisionRequest(DecisionReason?)`.
`ActionType` ∈ {`refund`, `manual_correction`, `debt_write_off`}.

Permission: `billing.money_action.approve` → Owner + BranchManager only.

## Architecture / placement

New workspace `review` in the existing `App.tsx` workspace switcher (matches the current
"every workspace lives in App.tsx" convention — not extracted to its own file, despite the
file's size, to stay consistent with siblings).

- `WorkspaceId` type += `'review'`.
- `workspaceIds[]` += `'review'`; `navItems` += a 9th entry **«Проверка»** (icon `ClipboardCheck` / `ShieldCheck` from lucide).
- `permissionNames` += `approveMoneyAction: 'billing.money_action.approve'`.
- `workspacePermissionRules.review = [approveMoneyAction]` → rail item is locked for
  anyone without the perm (existing `canOpenWorkspace` / `locked` behaviour).
- Render `{workspace === 'review' && <ReviewWorkspace currencyCode={config.currencyCode} backend={backendContext} />}` alongside the other workspace branches. The catch-all
  `SummarySidePanel` condition (the long `workspace !== ...` chain) must also exclude `'review'`.

`ReviewWorkspace` is a new function component inside `App.tsx`, mirroring
`BackendLogsWorkspace` ({ currencyCode, backend } props, `OperatorBackendContext | null`).

## Components — two tabs inside the workspace

Internal segment switcher (`activeSegment` pattern used by other workspaces).

### Tab A — «Заявки на одобрение» (approval queue)
- Loads `moneyActions.listPending(branchId)`.
- Row/card per request: action type label (возврат / коррекция / списание долга),
  amount via `formatMinorUnits(AmountMinorUnits, CurrencyCode)`, reason, requested-by
  (Guid resolved to display name via staff roster; fall back to short id), created-at,
  expires-at with an "истекает скоро / просрочена" badge derived from `ExpiresAtUtc`.
- **Одобрить** / **Отклонить** buttons. Reject requires a `DecisionReason` (inline field
  or small modal); approve reason is optional.
- On success: refetch the queue + `feedback` toast. Map 403 (self-approval), 409
  (not-pending / expired), 422 (cap) to human-readable Russian messages.

### Tab B — «Журнал операций» (high-risk audit with filters)
- Reuses `auditClient.search`. `AuditSearchRequest` is extended with `actorStaffUserId?`,
  `minAmount?`, `maxAmount?` (string|number|null, trailing-optional).
- Filters: staff picker (dropdown from roster), min / max amount, period. Default focuses
  high-risk actions (`money_action.*`, refund, manual_correction).
- Read-only table of records including amount column.

## Data flow / client

- `operatorApiClients.ts`: new `createMoneyActionClient(api)` with
  `listPending(branchId): Promise<MoneyActionRequestListResponse>`,
  `approve(branchId, requestId, req): Promise<...>`,
  `reject(branchId, requestId, req): Promise<...>`. Register it in `createOperatorApiClients`.
- Ensure the staff roster client is reachable from the aggregator (register if absent) so
  the review screen can resolve names and populate the actor picker.
- Extend `AuditSearchRequest` with the three new optional fields; `normalizeReportQuery`
  forwards them to the query string as-is.
- Load on `useEffect` keyed on backend identity (branchId / platformBaseUrl / accessToken),
  same as `BackendLogsWorkspace`. Manual refetch after approve/reject and on filter apply.

## Error handling

- `projectOperatorError` → `feedback` / `loadError` (existing pattern).
- Empty queue → placeholder «Нет заявок на одобрение».
- 403 self-approval / 409 stale / 422 cap → readable messages, no crash.
- No backend (fixture mode) → `loadStatus='fixture'`, demo-empty render, no throw.

## Tests (`bun test`, `App.test.tsx` patterns)

- Renders the pending queue from a mocked feed.
- Approve → issues POST `/approve` + refetches.
- Reject → requires a reason, issues POST `/reject` with `DecisionReason`.
- Audit filters (staff + amount) build the correct `AuditSearchRequest`.
- Gating: a session without `billing.money_action.approve` sees «Проверка» locked.
- Empty-queue placeholder and backend-error states.

## Out of scope

- WPF parity (operator native shell stays dormant — only WebView chrome localized).
- Any backend change beyond what already ships (none expected).
- Comp valuation / §5.4 checkout-boundary work (tracked separately).
