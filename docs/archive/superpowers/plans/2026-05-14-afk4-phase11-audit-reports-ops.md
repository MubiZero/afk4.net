# AFK4 Phase 11 Audit, Reports, And Operations Plan

Status: report CSV export slice implemented
Last updated: 2026-05-14

## Goal

Expose operational accountability data to managers, owners, supervisors, and
auditors without adding a web admin panel. Phase 11 starts with branch-scoped
audit search and then adds operational reports over the existing persistence.

## Scope

The implemented Phase 11 slices add:

- shared audit search response contracts;
- backend branch-scoped audit search over existing `audit_records`;
- `GET /api/branches/{branchId}/audit` protected by `audit.view`;
- success and denied audit records for audit reads;
- Operator App typed audit API client;
- Operator App Settings audit panel with action, outcome, target, date, and
  limit filters;
- shared shift, sales, gameplay time, cash operation, and operator action
  report contracts;
- backend branch-scoped operational reports over existing shift, session, POS,
  payment, cash movement, shift-linked ledger, and audit data;
- `GET /api/branches/{branchId}/reports/shifts` and
  `GET /api/branches/{branchId}/reports/sales` protected by `reports.view`;
- `GET /api/branches/{branchId}/reports/gameplay-time`,
  `GET /api/branches/{branchId}/reports/cash-operations`, and
  `GET /api/branches/{branchId}/reports/operator-actions` protected by
  `reports.view`;
- succeeded and denied audit records for report reads;
- Operator App report loading inside the Shifts workspace;
- CSV export endpoints for all five operational reports, protected by
  `reports.view`, returning `text/csv` attachments, and audited with the same
  report-read audit actions plus `Format = csv`;
- Operator App typed CSV download methods and Shifts workspace export actions
  that save report CSV files through a file-writer boundary;
- focused tests for contracts, service behavior, endpoint authorization, API
  client query construction, CSV formatting, CSV endpoints, and ViewModel
  behavior.

## Non-Goals

- No new audit table schema in this slice.
- No new report table schema in this slice.
- No PDF, XLSX, scheduled email, or cross-branch report bundle export yet.
- No web admin panel.
- No cross-branch owner dashboard yet.

## Safety Rules

- Audit rows remain append-only.
- Audit search is tenant- and branch-scoped.
- Audit search returns DTOs, not EF entities.
- Staff must hold `audit.view` before reading audit rows.
- Reading audit data is itself audited.
- Report reads are tenant- and branch-scoped.
- Staff must hold `reports.view` before reading operational reports.
- Reading report data is itself audited.
- Shift report cash math follows the same signs as shift close.

## Follow-Up

1. Add diagnostics and backup/restore runbooks after the core reports are
   visible.
