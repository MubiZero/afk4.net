# AFK4 Phase 11 Audit, Reports, And Operations Plan

Status: first slice in progress  
Last updated: 2026-05-14

## Goal

Expose operational accountability data to managers, owners, supervisors, and
auditors without adding a web admin panel. The first slice starts with
branch-scoped audit search because the immutable audit table already exists and
is the safest foundation for later operator-action reports.

## Scope

This first slice adds:

- shared audit search response contracts;
- backend branch-scoped audit search over existing `audit_records`;
- `GET /api/branches/{branchId}/audit` protected by `audit.view`;
- success and denied audit records for audit reads;
- Operator App typed audit API client;
- Operator App Settings audit panel with action, outcome, target, date, and
  limit filters;
- focused tests for contracts, service behavior, endpoint authorization, API
  client query construction, and ViewModel behavior.

## Non-Goals

- No new audit table schema in this slice.
- No full shift, sales, gameplay, or cash report read models yet.
- No report export format yet.
- No web admin panel.
- No cross-branch owner dashboard yet.

## Safety Rules

- Audit rows remain append-only.
- Audit search is tenant- and branch-scoped.
- Audit search returns DTOs, not EF entities.
- Staff must hold `audit.view` before reading audit rows.
- Reading audit data is itself audited.

## Follow-Up

1. Add shift and sales report endpoints backed by existing shift/POS tables.
2. Add gameplay time and operator-action report summaries.
3. Add Operator App report workspace for accountants/managers.
4. Add diagnostics and backup/restore runbooks after the core reports are
   visible.
