# AFK4 Production Readiness Roadmap

Last updated: 2026-05-18

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
   The first Coolify-managed Linux VPS staging rehearsal was executed on
   2026-05-17 from `codex/coolify-staging-rehearsal`: Coolify built the
   Platform API container from the repository, ran a managed PostgreSQL
   service, applied EF migrations explicitly, and passed backend health/auth
   smoke. The real staging domain `afk4.staging.mubi.dev` resolves to the
   Coolify VPS and passes `/api/health` over trusted TLS. The rehearsal API
   token and staging database/session secrets were rotated after the
   hardening pass.

2. **CI Gate**

   Use cost-aware GitHub Actions workflows to build and test relevant pull
   requests, run package smoke for client MSI artifacts, and keep release
   packaging manual and guarded. GitHub Actions billing is enabled, but
   workflows must avoid unnecessary manual remote runs, use Windows hosted
   runners only where they add required coverage, cancel stale PR runs, set
   timeouts, and keep artifact retention short. PR #11 merged the cost-aware CI
   gate, and PR #12 opted GitHub JavaScript actions into Node 24 execution.
   GitHub rulesets are not enforced on the current private repository plan, so
   merges must manually follow `AGENTS.md`: the current PR head commit needs a
   green remote `PR Verification Result` before merge.

3. **Real Device Smoke**

   Enroll a real Windows 10/11 gaming PC. Validate device credential auth,
   heartbeat, SignalR commands, session start/end, lease refresh, lock/unlock,
   Player Shell state, installed app report, and diagnostics. A repeatable
   manual staging runbook now exists at
   `docs/operations/real-device-windows-pc-smoke.md`, and the Agent host is
   wired for Windows Service runtime under service name `AFK4.Agent.Service`.
   A staging-only one-click Gaming PC setup executable path now exists for clean
   Windows 11 smoke VMs, and the staging public lease verification key is
   committed for reproducible packaging. One Windows 11 VM passed rebuilt x64
   install/enroll/heartbeat plus session start/end, signed lease, local runtime
   state, and visible Player Shell active/locked evidence. The smoke also
   exposed two hardening gaps: service-started session-0 Shell competition and
   missing `ending` session finalization after accepted lock. Both are now
   mitigated in code on `codex/staging-gaming-pc-bootstrapper`: Agent Service
   Shell auto-start targets the active interactive Windows session with
   session-aware process detection, and accepted/completed lock command results
   or the next heartbeat finalization fallback move sessions to `ended` so the
   seat/device can be reused. After staging was redeployed from that branch,
   the Windows 11 VM reuse smoke passed without SQL cleanup. The gate remains
   open for physical Windows 10/11 hardware evidence and reboot/update
   recovery.

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

- Production hosting provider and deployment topology are not selected for
  commercial production.
- Production environments are not codified.
- Coolify-first staging is deployed and smoke-tested on
  `afk4.staging.mubi.dev`; staging API/database/session secrets were rotated
  after the rehearsal.
- Automated mandatory PR checks are not enforced by GitHub rulesets on the
  current private repository plan; manual green-check merge discipline is
  recorded in `AGENTS.md`.
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
- The Coolify API token and staging database/session secrets used during the
  rehearsal were rotated; future secret exchange must stay out of chat.
- Object-store/CDN provider and presigned upload automation are undecided.
- Update package registration currently supports short-lived staff tokens;
  service credential policy is still open.

### Agent And Windows Runtime

- Automatic Agent-side consumption of rotated device credentials is not
  implemented.
- Agent service registration now has matching Windows Service host lifetime
  wiring, but real service startup must still be validated through the
  real-device smoke runbook.
- A staging-only Gaming PC setup bootstrapper exists in code, with a committed
  staging public lease verification key for release workstation builds. A first
  Windows 11 VM passed rebuilt x64 install/enroll/heartbeat, session
  start/end, signed lease, local runtime state, and visible Player Shell
  active/locked evidence. A second Windows 11 VM smoke confirmed
  interactive-session Shell auto-start and active-state delivery without manual
  Shell restart after the long-lived state pipe fix.
- Lock/unlock enforcement needs real Windows validation beyond test adapters.
- Player Shell service-session competition is mitigated in code by
  session-aware process detection and Agent-driven launch into the active
  interactive Windows session. The Agent-to-Shell state pipe now serves the
  latest state to late or restarted Shell clients instead of relying on a short
  publish timing window. A rebuilt Windows 11 staging VM confirmed active-state
  delivery without manual Shell restart; physical PC smoke is still needed
  before the operational gate is closed.
- Session end/finalization is implemented in code for accepted/completed lock
  command results and heartbeat recovery when accepted lock results were
  already persisted for an `ending` session, including duplicate-result
  idempotency and seat/device reuse tests. After staging was redeployed from
  `codex/staging-gaming-pc-bootstrapper`, Windows 11 VM smoke confirmed an
  ended session returned the seat/device to locked and a second session started
  on the same seat without SQL cleanup.
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

1. Real-device smoke execution

   Repeat `docs/operations/real-device-windows-pc-smoke.md` on physical
   Windows 10/11 hardware, or on a second clean Windows 11 VM if physical
   hardware is unavailable, to broaden confidence beyond the current VM smoke.
   Record pass/fail results and any duplicate Shell process behavior in the
   progress snapshot.

2. `codex/postgres-restore-rehearsal`

   Execute the backup/restore runbook against staging-like data and record
   evidence in progress docs.

3. `codex/pilot-admin-setup`

   Add or document the minimum pilot setup path for organization, branch,
   staff, roles, layout, device-seat assignment, devices, tariffs, and POS
   catalog.

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
