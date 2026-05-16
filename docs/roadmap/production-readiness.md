# AFK4 Production Readiness Roadmap

Last updated: 2026-05-16

## Purpose

This roadmap tracks what separates the current AFK4 codebase from production.
It is intentionally operational: infrastructure, release gates, security,
backups, device validation, and pilot readiness.

The product scope and architecture decisions remain in:

- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`

The current implementation snapshot remains in:

- `docs/progress/2026-05-12-vertical-slice-progress.md`

## Production Definitions

### Pilot Production

Pilot production means AFK4 can run in one controlled club or lab environment
with close operator/developer supervision.

Minimum bar:

- Platform API deployed to a production-like environment.
- PostgreSQL hosted outside a developer machine.
- TLS, domain, and environment-based secrets configured.
- One real Windows gaming PC enrolled with Agent Service and Player Shell.
- Operator App can perform the core day flow against the deployed backend.
- Backup and restore rehearsal completed once.
- Client MSI installation and update rollout tested on a real PC.
- Manual/mock payments are acceptable.
- Manual operational setup is acceptable if documented.

### Commercial Production

Commercial production means AFK4 can be given to real clubs without developers
standing next to every critical operation.

Minimum bar:

- Mandatory CI gates and release discipline.
- Staging environment mirrors production closely enough for migration, update,
  and smoke rehearsal.
- Backup retention, encryption, restore ownership, and incident procedure are
  defined and tested.
- Production certificate/signing/CDN/secrets policy is settled.
- Agent lock/reboot/update/rollback behavior is validated on real Windows
  devices.
- Staff, role assignment, layout, device, update, audit, and reporting
  workflows are usable by operators/managers.
- Operational monitoring and support diagnostics are actionable.

## Critical Path To Pilot Production

1. **Staging Infrastructure**

   Deploy the Platform API and PostgreSQL in a production-like environment.
   Configure TLS, domain, environment variables, secrets, logging, health, and
   EF migrations.

2. **CI Gate**

   Use cost-aware GitHub Actions workflows to build and test relevant pull
   requests, run package smoke for client MSI artifacts, and keep release
   packaging manual and guarded. GitHub Actions billing is enabled, but
   workflows must avoid unnecessary manual remote runs, use Windows hosted
   runners only where they add required coverage, cancel stale PR runs, set
   timeouts, and keep artifact retention short. After the first successful
   remote PR run, enable branch protection for the `PR Verification Result`
   check.

3. **Real Device Smoke**

   Enroll a real Windows 10/11 gaming PC. Validate device credential auth,
   heartbeat, SignalR commands, session start/end, lease refresh, lock/unlock,
   Player Shell state, installed app report, and diagnostics.

4. **Backup And Restore Rehearsal**

   Run `docs/operations/postgres-backup-restore.md` against staging data:
   backup, restore into a clean database, apply migrations, start the API, and
   smoke health/auth/floor-map/diagnostics/audit/reports/update status.

5. **Signed Client Release Rehearsal**

   Decide temporary pilot signing/hosting approach, build MSI artifacts, sign
   them, publish update metadata, register packages, create an internal rollout,
   and verify update status from the real Agent.

6. **Pilot Setup Runbook**

   Document exactly how to create the first organization, branch, staff users,
   roles, zones, seats, devices, tariffs, POS products, and update channels for
   a pilot club.

## Commercial Production Blockers

### Infrastructure And Release

- Production hosting provider and deployment topology are not selected.
- Staging and production environments are not codified.
- Mandatory PR checks and branch protection are not configured.
- Migration rehearsal is documented but not automated.
- No production incident/rollback checklist exists for backend deployment.

### Data Protection

- Backup/restore runbook exists, but a real rehearsal must be completed.
- Backup encryption, retention, off-host storage, and restore owner must be
  named before launch.
- Point-in-time recovery and provider-managed backup policy are not selected.

### Secrets And Signing

- Production Authenticode certificate authority is undecided.
- Certificate storage policy is undecided.
- ECDSA update metadata signing key storage policy is undecided.
- Object-store/CDN provider and presigned upload automation are undecided.
- Update package registration currently supports short-lived staff tokens;
  service credential policy is still open.

### Agent And Windows Runtime

- Automatic Agent-side consumption of rotated device credentials is not
  implemented.
- Lock/unlock enforcement needs real Windows validation beyond test adapters.
- Reboot recovery must be exercised on physical PCs.
- Update rollback must be tested against MSI installs on real devices.
- Production lease duration and heartbeat refresh threshold need telemetry.

### Operator Workflows

- Staff management workflow is not implemented.
- Custom roles and role editing UI are not implemented.
- Branch layout management UI is not implemented.
- Pilot can work with seeded/manual setup, but commercial production needs
  operator-safe configuration screens.

### Observability And Support

- Backend and Agent logs exist at implementation level, but production log
  aggregation, metrics, alerting, and correlation policy are not configured.
- Diagnostics screen exists, but support runbooks for common incidents are
  still needed.

## Recommended Next Branches

1. `codex/staging-deploy-runbook`

   Document and script the first staging deployment path, environment variables,
   migrations, and smoke commands.

2. `codex/real-device-smoke`

   Create a focused smoke checklist/script set for one real Windows gaming PC
   covering Agent, Shell, sessions, lock/unlock, updates, diagnostics, and logs.

3. `codex/postgres-restore-rehearsal`

   Execute the backup/restore runbook against staging-like data and record
   evidence in progress docs.

4. `codex/pilot-admin-setup`

   Add or document the minimum pilot setup path for organization, branch,
   staff, roles, layout, devices, tariffs, and POS catalog.

## Decision Rules

- Do not add web admin, local server, non-Windows agents, microservices, kernel
  driver, fiscal integrations, or mobile app to solve production readiness
  unless the PRD and architecture spec are updated first.
- Prefer runbooks and explicit release gates before adding provider-specific
  SDKs.
- Prefer one real-device smoke loop over more theoretical docs once staging is
  available.
- Treat backup restore and Agent update rollback as launch gates, not cleanup
  tasks.
