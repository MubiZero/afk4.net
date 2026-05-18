# AFK4

AFK4 is a cloud-first SaaS platform for managing Windows-based computer clubs.
It is being built as operator-grade software in the same product category as
Senet, Langame, and SmartShell: cloud backend, native Windows Operator App,
Windows Agent Service, Player Shell, sessions, billing, POS, audit, reports,
and centralized client updates.

The current codebase is no longer just a scaffold. It contains an implemented
MVP-oriented vertical slice with tested backend modules, WPF client surfaces,
Agent/Shell foundations, update packaging, and operational runbooks. The main
remaining gap is production readiness: staging/prod infrastructure, secrets,
real Windows device smoke tests, backup rehearsal, signing/CDN decisions, and
Agent hardening.

## Source Of Truth

Read these files first in new sessions:

- [MVP PRD](docs/product/AFK4-MVP-PRD.md) - product scope, users, journeys, and
  non-goals.
- [Architecture Design](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md) -
  platform decisions and module boundaries.
- [Current Progress](docs/progress/2026-05-12-vertical-slice-progress.md) -
  implemented capabilities, latest verification, known gaps, and next work.
- [Production Readiness Roadmap](docs/roadmap/production-readiness.md) - what
  separates the project from pilot and commercial production.

Historical phase plans and old progress logs are reference material, not
required reading for every new session.

## Fixed MVP Decisions

- Cloud-first SaaS; no local club server in the MVP.
- No web admin panel in the MVP.
- Native Windows Operator App built with WPF + MVVM.
- Gaming PCs are Windows 10/11 only in the MVP.
- Backend is ASP.NET Core on .NET 10, starting as a modular monolith.
- PostgreSQL is the production source-of-truth database.
- Agent runtime is split into Windows Agent Service and Player Shell UI.
- Realtime communication uses SignalR over WebSockets.
- Offline mode is grace mode only for already active sessions.
- Billing uses immutable ledger entries, not mutable balances.
- MVP includes POS, inventory, shifts, receipts, audit, reports, and
  centralized signed updates.
- MVP excludes web admin, local server, non-Windows agents, kernel driver,
  country-specific fiscal integrations, mobile app, microservices, and full
  event sourcing.

## Runtime Parts

### Platform API

`src/AFK4.Platform.Api` is the cloud backend. It owns identity, tenancy, device
management, sessions, billing, POS, shifts, reports, audit, updates, and
reconciliation. PostgreSQL persistence is implemented with EF Core migrations.

### Operator App

`src/AFK4.Operator.App` is the native Windows app for operators, cashiers,
managers, technicians, accountants, and owners depending on permissions. The
main working screen is the floor map. Current workflows include auth, floor
map/session actions, players, POS, shifts, reports, settings, device tools,
updates, audit search, diagnostics, and CSV exports.

### Agent Service

`src/AFK4.Agent.Service` runs on gaming PCs. It handles device credentials,
heartbeat, realtime commands, lease validation, local session state,
reconciliation, Shell supervision, launcher policy, update download/verification
and installer adapter execution.

### Player Shell

`src/AFK4.Player.Shell` is the player-facing WPF UI. It displays locked,
active-session, warning, grace/offline, ending, and launcher states. It is not a
trusted authority for sessions, billing, or authorization.

### Shared Projects

- `src/AFK4.BuildingBlocks` - low-level domain primitives.
- `src/AFK4.Shared.Contracts` - DTOs shared by backend, Operator App, Agent,
  and Shell.
- `src/AFK4.Update.Publisher` - CLI for publishing update artifacts and signed
  package metadata.

## Local Requirements

- Windows with PowerShell.
- Git for Windows.
- .NET SDK `10.0.203` or compatible .NET 10 SDK allowed by `global.json`.
- Repository-local .NET tools restored with `dotnet tool restore`.
- Docker or local PostgreSQL for live smoke runs.

Useful explicit tool paths on the main development machine:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' --version
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
```

## Build And Test

From the repository root:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

The latest recorded full verification passed 624 tests with 0 failures. See
[Current Progress](docs/progress/2026-05-12-vertical-slice-progress.md) for the
exact current verification notes.

## Local Runbooks

- [Coolify Staging Deploy](docs/operations/coolify-staging-deploy.md)
- [Real Device Windows PC Smoke](docs/operations/real-device-windows-pc-smoke.md)
- [Local PostgreSQL And Device Smoke](docs/operations/local-postgres-smoke.md)
- [Client Packaging](docs/operations/client-packaging.md)
- [Update Package Publishing](docs/operations/update-package-publishing.md)
- [Client Update Rollout](docs/operations/client-update-rollout.md)
- [Agent Installer Enrollment](docs/operations/agent-installer-enrollment.md)
- [PostgreSQL Backup And Restore](docs/operations/postgres-backup-restore.md)

## Packaging Snapshot

Build local MSI packages:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

Build the staging one-click Gaming PC setup executable for clean Windows 11
smoke VMs by supplying the committed staging session lease and update
verification public keys:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal -StagingLeasePublicKeyPath .\deploy\coolify\staging-session-signing-public.pem -StagingUpdateSigningPublicKeyPath .\deploy\coolify\staging-update-signing-public.pem
```

Signed release jobs then use:

- `scripts/sign-client-packages.ps1`
- `scripts/publish-client-msi-updates.ps1`
- `scripts/register-update-package-requests.ps1`

Secrets, certificates, presigned upload URLs, generated request JSON, and MSI
artifacts must stay outside source control or under ignored `artifacts/`.

## Documentation Hygiene

Keep long historical logs out of required session context. Current status
belongs in [Current Progress](docs/progress/2026-05-12-vertical-slice-progress.md).
Production launch gaps belong in
[Production Readiness Roadmap](docs/roadmap/production-readiness.md). Old
evidence can be archived under `docs/archive/`.
