# AFK4 Diagnostics And Backup Design

Status: approved Phase 14 design
Last updated: 2026-05-16

## Purpose

This spec closes the remaining Phase 11 operations scope for diagnostics
dashboards and backup/restore runbooks.

AFK4 is already the operational authority for sessions, devices, updates,
ledger, POS, inventory, receipts, and audit. Before production, club managers
and technicians need a dense branch diagnostics view that answers whether the
club can operate, whether gaming PCs are stale or failing commands, whether
rollouts are failing, and whether recovery procedures are rehearsable.

## Decisions

AFK4 adds a branch-scoped diagnostics read model to the cloud backend and
Operator App. Diagnostics are read-only and do not change device, update,
session, ledger, POS, inventory, or audit authority.

The first diagnostics surface is:

```text
GET /api/branches/{branchId}/diagnostics
```

The endpoint is protected by a new `diagnostics.view` permission and writes
allowed or denied audit records with action `diagnostics.view`.

Initial role access:

- owner;
- branch manager;
- shift supervisor;
- technician;
- accountant/auditor.

Cashier/operator does not receive diagnostics access by default. Diagnostics
include operational and support data that is useful for escalation but not
needed for ordinary cashier workflows.

## Backend Read Model

The diagnostics response is assembled from existing persistence:

- `devices` for total, online, locked, heartbeat, Agent version, and Shell
  version status;
- `device_commands` for pending and failed command counts plus recent command
  failures;
- `device_update_statuses` for installing, failed, rollback, and installed
  rollout status;
- `update_rollouts` for active rollout counts;
- `audit_records` for audit coverage of diagnostics reads.

No new database tables are required in the first slice.

The response includes:

- generation timestamp;
- device totals and heartbeat age using a configurable stale threshold;
- newest device heartbeat timestamp;
- command totals for pending and failed commands;
- recent failed commands with machine name where available;
- update rollout totals and status counts;
- recent failed update statuses with machine name where available.

The stale heartbeat threshold defaults to five minutes. Later releases may make
it tenant-configurable, but the MVP keeps the threshold in the service boundary
so tests and UI behavior remain deterministic.

## Operator App

The Operator App exposes diagnostics from Settings, alongside update status and
audit search.

UI rules:

- dense operational layout, not a marketing dashboard;
- summary counters first, then focused grids for stale devices, failed
  commands, and failed update statuses;
- visible refresh status and actionable error text;
- no local authority beyond displaying backend-confirmed diagnostics.

The Operator App shows diagnostics only when the signed-in staff context has
`diagnostics.view`.

## Backup And Restore Runbook

AFK4 adds a PostgreSQL backup and restore runbook before production.

The runbook must cover:

- backup scope and retention expectations;
- `pg_dump` custom-format backups;
- restore rehearsal into a new database;
- migration script generation and staging rehearsal;
- post-restore smoke checks;
- handling secrets outside source control;
- append-only audit and ledger expectations;
- rollback boundaries for destructive mistakes.

The runbook is documentation only in Phase 14. Provider-specific managed
database backup APIs, PITR automation, encryption key management adapters, and
tenant export tooling remain out of this slice.

## Out Of Scope

Phase 14 does not introduce:

- web admin diagnostics;
- local club server diagnostics;
- background monitoring or alerting infrastructure;
- provider-specific metrics exporters;
- provider-specific backup APIs;
- database schema changes;
- mutable balance fields or destructive data repair tools.

