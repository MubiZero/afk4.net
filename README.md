# AFK4.NET

AFK4.NET is a cloud-first SaaS platform for managing Windows-based cyber clubs.
It is being built as operator-grade software in the same product category as
Senet, Langame, and SmartShell: cloud backend, native Windows app Organization Admin,
internal Platform Control, Windows Agent Service, Player Shell, sessions,
billing, POS, audit, reports, and centralized client updates.

The current codebase is no longer just a scaffold. It contains an implemented
MVP-oriented vertical slice with tested backend modules, Windows client
surfaces, Agent/Shell foundations, update packaging, and operational runbooks.
Current work is concentrated around pilot/production readiness, Organization Admin
hardening, Windows endpoint smoke, and onboarding/release polish; use the
progress snapshot and production-readiness roadmap below for the exact current
state.

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

Historical phase plans, superseded design notes, and old progress logs are
archived under `docs/archive/` and are not required reading for every new
session.

## Fixed MVP Decisions

- Cloud-first SaaS; no local club server in the MVP.
- Internal browser-based Platform Control is in the MVP for platform-owner
  organization onboarding, subscription/status controls, organization health, and support.
- Native Windows app Organization Admin built as a .NET desktop shell with WebView2 and
  a React/TypeScript operator UI.
- Gaming PCs are Windows 10/11 only in the MVP.
- Backend is ASP.NET Core on .NET 10, starting as a modular monolith.
- PostgreSQL is the production source-of-truth database.
- Agent runtime is split into Windows Agent Service and Player Shell UI.
- Realtime communication uses SignalR over WebSockets.
- Offline mode is grace mode only for already active sessions.
- Billing uses immutable ledger entries, not mutable balances.
- MVP includes POS, inventory, shifts, receipts, audit, reports, and
  centralized signed updates.
- MVP excludes customer browser operational admin as the primary club UI, local
  server, non-Windows agents, kernel driver, country-specific fiscal
  integrations, mobile app, microservices, and full event sourcing.

## Runtime Parts

### Platform API

`src/AFK4.Platform.Api` is the cloud backend. It owns identity, tenancy, device
management, sessions, billing, POS, shifts, reports, audit, updates, and
reconciliation. PostgreSQL persistence is implemented with EF Core migrations.

### Platform Control

The platform-owner surface is the internal browser-based Platform Control for
organization provisioning, first branch setup, owner invites,
subscription/status controls, organization health, support notes, and
suspend/reactivate actions. Its backend endpoints live behind a separate
platform-admin authorization boundary, not branch staff tokens. Sign-in is a
two-step flow: password, then a TOTP code from an authenticator app, with
one-time recovery codes for lost devices. A Settings section lets a full
platform admin manage other platform staff: invite by code, change role,
disable access, and reset a colleague's two-factor setup; self-demotion and
disabling the last active full admin are both blocked. Support mode lets
platform staff open a time-boxed, reason-required session inside an
organization's admin app to help with configuration, with read access almost
everywhere but write access limited to a handful of non-money areas and every
action logged to the organization's own audit trail; see
[Support Mode](docs/runbooks/support-mode.md).

### Organization Admin

`src/AFK4.OrganizationAdmin.App` is the native Windows app for operators, cashiers,
managers, technicians, accountants, and owners depending on permissions. The
approved target is a .NET desktop host with WebView2 and a React/TypeScript
operator UI. It is distinct from the Platform Control; the main working
screen is the floor map. The WebView2/React console now has staff sign-in,
backend-loaded floor-map state, selected-seat session actions, billing-mode
selection, filters/table view, SignalR reloads, active-session ticking, and
backend-confirmed critical commands. The legacy WPF/MVVM implementation remains
the migration source for parity areas and operator workflows that are still
being hardened or ported.

The accepted UI/UX direction for this native operator console is recorded in
[Organization Admin UI/UX Target](docs/product/organization-admin-ui-target.md).

### Agent Service

`src/AFK4.Agent.Service` runs on gaming PCs. It handles device credentials,
heartbeat, realtime commands, lease validation, local session state,
reconciliation, Shell supervision, launcher policy, update download/verification
and installer adapter execution.

### Setup Wizard

`src/AFK4.SetupWizard` is the first-run WPF enrollment wizard for the single
Agent MSI flow. It runs as `AFK4.SetupWizard.exe` against install APIs: owner
code discovery, branch/floor-map discovery, role choice, optional gaming-PC
seat selection or seat creation, seatless manager-workstation enrollment,
device enrollment, stable device key, and Agent bootstrap environment writing.
The `AFK4 Agent` MSI packages the wizard with RunOnce/start-menu launch
affordances and starts Agent Service after successful enrollment.

### Player Shell

`src/AFK4.Player.Shell` is the player-facing WPF UI. It displays locked,
active-session, warning, grace/offline, ending, and launcher states. It is not a
trusted authority for sessions, billing, or authorization.

### Shared Projects

- `src/AFK4.Shared.Contracts` - DTOs shared by backend, Organization Admin, Agent,
  and Shell.
- `src/AFK4.Update.Publisher` - CLI for publishing update artifacts and signed
  package metadata.
- `packages/ui` (`@afk4/ui`) - shared web UI layer (styles, layout primitives)
  used by both Platform Control and Organization Admin's React frontends.

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

The latest recorded verification can move quickly. See
[Current Progress](docs/progress/2026-05-12-vertical-slice-progress.md) for the
current build/test counts, smoke evidence, known gaps, and next work.

## Organization Admin Staging Smoke

The Organization Admin defaults to `http://localhost:5074`. To point the same local
build at staging for pilot smoke, set `AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL` before
launching the app:

```powershell
$env:AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL = 'https://api.afk4.net'
& .\src\AFK4.OrganizationAdmin.App\bin\Debug\net10.0-windows\AFK4.OrganizationAdmin.App.exe
```

## Local Runbooks

- [Coolify Staging Deploy](docs/operations/coolify-staging-deploy.md)
- [Real Device Windows PC Smoke](docs/operations/real-device-windows-pc-smoke.md)
- [Local PostgreSQL And Device Smoke](docs/operations/local-postgres-smoke.md)
- [Client Packaging](docs/operations/client-packaging.md)
- [Update Package Publishing](docs/operations/update-package-publishing.md)
- [Client Update Rollout](docs/operations/client-update-rollout.md)
- [Agent Installer Enrollment](docs/operations/agent-installer-enrollment.md)
- [PostgreSQL Backup And Restore](docs/operations/postgres-backup-restore.md)
- [Pilot Branch Setup](docs/operations/pilot-branch-setup.md)
- [Uptime Monitoring](docs/operations/uptime-monitoring.md)
- [Platform/Organization Big-Bang Cutover](docs/operations/platform-organization-big-bang-cutover.md)

## Packaging Snapshot

Build the default local MSI package set:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

This produces the Organization Admin MSI, the Agent + Setup Wizard onboarding MSI
(`afk4-agent-<version>-<channel>.msi`), and the standalone Player Shell MSI
(`afk4-player-shell-<version>-<channel>.msi`). The legacy coordinated
gaming-PC MSI and one-click staging bootstrapper are fallback/recovery paths
only; see [Client Packaging](docs/operations/client-packaging.md) for those
switches.

Signing, upload, package registration, and rollout are covered by:

- [Client Packaging](docs/operations/client-packaging.md)
- [Update Package Publishing](docs/operations/update-package-publishing.md)
- [Client Update Rollout](docs/operations/client-update-rollout.md)

Secrets, certificates, presigned upload URLs, generated request JSON, and MSI
artifacts must stay outside source control or under ignored `artifacts/`.

## Documentation Hygiene

Keep long historical logs out of required session context. Current status
belongs in [Current Progress](docs/progress/2026-05-12-vertical-slice-progress.md).
Production launch gaps belong in
[Production Readiness Roadmap](docs/roadmap/production-readiness.md). Old
evidence can be archived under `docs/archive/`.

## License

`SPDX-License-Identifier: AGPL-3.0-or-later`

AFK4 is distributed under the [GNU Affero General Public License v3.0 or
later](LICENSE). The full license text lives in the [`LICENSE`](LICENSE) file
in the repository root.

What this means in practice:

- Anyone may read, modify, and self-host AFK4. Modifications and combined
  works that touch AGPL-covered code must be released under the same license.
- Operators who expose a modified AFK4 over a network (the SaaS scenario the
  AGPL was written for) must offer the corresponding source code to the
  network users of that service.
- The AFK4 maintainers retain copyright on their contributions. External
  contributions are accepted under the same AGPL-3.0-or-later terms unless a
  separate contributor agreement is signed.

If you need AFK4 under different terms (for example to ship a derivative
inside a closed-source product), contact the maintainers about a commercial
license.
