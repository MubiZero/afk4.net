# AFK4 Phase 11 Audit, Reports, And Operations Plan

Status: shift/sales reports slice implemented
Last updated: 2026-05-14

## Goal

Expose operational accountability data to managers, owners, supervisors, and
auditors without adding a web admin panel. Phase 11 starts with branch-scoped
audit search and then adds shift/sales reports over the existing operational
persistence.

## Scope

The implemented Phase 11 slices add:

- shared audit search response contracts;
- backend branch-scoped audit search over existing `audit_records`;
- `GET /api/branches/{branchId}/audit` protected by `audit.view`;
- success and denied audit records for audit reads;
- Operator App typed audit API client;
- Operator App Settings audit panel with action, outcome, target, date, and
  limit filters;
- shared shift and sales report contracts;
- backend branch-scoped shift and sales reports over existing shift, POS,
  payment, cash movement, and shift-linked ledger data;
- `GET /api/branches/{branchId}/reports/shifts` and
  `GET /api/branches/{branchId}/reports/sales` protected by `reports.view`;
- succeeded and denied audit records for report reads;
- Operator App report loading inside the Shifts workspace;
- focused tests for contracts, service behavior, endpoint authorization, API
  client query construction, and ViewModel behavior.

## Non-Goals

- No new audit table schema in this slice.
- No new report table schema in this slice.
- No gameplay, cash operations, or operator-action reports yet.
- No report export format yet.
- No web admin panel.
- No cross-branch owner dashboard yet.

## Safety Rules

- Audit rows remain append-only.
- Audit search is tenant- and branch-scoped.
- Audit search returns DTOs, not EF entities.
- Staff must hold `audit.view` before reading audit rows.
- Reading audit data is itself audited.
- Report reads are tenant- and branch-scoped.
- Staff must hold `reports.view` before reading shift/sales reports.
- Reading report data is itself audited.
- Shift report cash math follows the same signs as shift close.

## Follow-Up

1. Add gameplay time, cash operations, and operator-action report summaries.
2. Add report export formats when managers need shareable files.
3. Add diagnostics and backup/restore runbooks after the core reports are
   visible.
