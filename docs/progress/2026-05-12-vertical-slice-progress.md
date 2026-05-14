# AFK4 Vertical Slice Progress

Status: Phase 11 report CSV export slice is implemented, the Phase 10 update
publisher now has production-style artifact hosting and signing key source
boundaries, Docker PostgreSQL live smoke passes locally for the update slice,
Agent update execution/recovery adapter boundaries are implemented, and
Operator App update status visibility, audit search, branch-scoped operational
reports, and report CSV exports are available to permissioned staff.
Last updated: 2026-05-14

## Scope

This document tracks delivery progress and known implementation deviations for
the first technical vertical slice and the realtime device channel follow-up.
It is intentionally separate from `AGENTS.md` because progress changes more
frequently than agent instructions.

Stable product and architecture decisions live in:

- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`

The implementation plans for this slice live in:

- `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md`
- `docs/superpowers/plans/2026-05-12-afk4-realtime-device-channel.md`
- `docs/superpowers/plans/2026-05-12-afk4-phase2-identity-tenancy-rbac-audit.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase3-club-layout-device-management.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase4-session-lifecycle-grace-mode.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase5-billing-ledger-tariffs-packages.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase6-pos-inventory-shifts-receipts.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase7-operator-app-production-ux.md`
- `docs/superpowers/plans/2026-05-14-afk4-phase8-agent-enforcement-player-shell.md`
- `docs/superpowers/plans/2026-05-14-afk4-phase9-updates-installers.md`
- `docs/superpowers/plans/2026-05-14-afk4-phase10-update-publishing-automation.md`
- `docs/superpowers/plans/2026-05-14-afk4-phase11-audit-reports-ops.md`

## Phase 10 Production Artifact Hosting And Key Source Boundary

Implemented on `codex/phase11-operational-reports` after the report CSV export
commit `3133fd4`.

Implemented in this Phase 10 follow-up:

- Added `file-system` and `http-put` artifact-store selection to the Update
  Publisher CLI.
- Added presigned HTTP PUT artifact upload support with a separate public CDN
  artifact URI recorded in `CreateUpdatePackageRequest`.
- Added signing-key loading from a controlled environment variable so key
  vaults, secret managers, or CI runners can inject the ECDSA PEM without a
  file on disk.
- Preserved the local file-system artifact publishing path and file-based
  signing-key path for development use.
- Updated the PowerShell wrapper and update package publishing runbook for both
  artifact stores and both signing key sources.

Targeted verification on 2026-05-14 from `D:\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Update.Publisher.Tests\AFK4.Update.Publisher.Tests.csproj --filter FileSystemUpdatePackagePublisherTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Update Publisher targeted tests passed 6/6.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 563/563:
  - BuildingBlocks 3/3;
  - Shared.Contracts 71/71;
  - Update.Publisher 6/6;
  - Agent.Service 65/65;
  - Player.Shell 11/11;
  - Operator.App 111/111;
  - Platform.Api 296/296.

## Phase 11 Report CSV Export Slice

Implemented on `codex/phase11-operational-reports` after the operational report
summary commit `f2e94fd`.

Implemented in this Phase 11 export slice:

- Added a backend CSV formatter for shift, sales, gameplay time, cash
  operation, and operator-action report DTOs.
- Added `GET /api/branches/{branchId}/reports/*/export.csv` routes for all
  five operational report families.
- Export routes reuse the same report filters, `reports.view` permission, and
  report-read audit actions as JSON report reads, with audit details marking
  `Format = csv`.
- CSV responses are returned as `text/csv` attachments with deterministic
  manager-friendly filenames.
- Added Operator App typed CSV download methods and Shifts workspace export
  commands.
- Added a file-writer boundary for Operator App CSV saves, with the production
  implementation using the native Windows save-file dialog.
- Added report CSV export buttons to the Operator App Shifts reports panel.

Targeted verification on 2026-05-14 from `D:\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "ReportEndpointTests|ReportCsvExporterTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorShiftApiClientTests|ShiftWorkspaceViewModelTests|OperatorShellViewModelTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build src\AFK4.Operator.App\AFK4.Operator.App.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Platform report endpoint/exporter tests passed 28/28.
- Operator shift/report/shell targeted tests passed 32/32.
- Operator App project build succeeded with 0 warnings and 0 errors.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 560/560:
  - BuildingBlocks 3/3;
  - Shared.Contracts 71/71;
  - Update.Publisher 3/3;
  - Agent.Service 65/65;
  - Player.Shell 11/11;
  - Operator.App 111/111;
  - Platform.Api 296/296.

## Phase 11 Gameplay, Cash Operations, And Operator Actions Reports Slice

Implemented on `codex/phase11-operational-reports` after the shift/sales
report merge commit `f300fdc`.

Implemented in this Phase 11 operational reports slice:

- Added shared report contracts for gameplay time, cash operations, and
  operator-action summaries.
- Extended `EfReportService` over existing sessions, shift-linked ledger
  entries, cash movements, POS cash payments/refunds, and audit records.
- Added `GET /api/branches/{branchId}/reports/gameplay-time`,
  `GET /api/branches/{branchId}/reports/cash-operations`, and
  `GET /api/branches/{branchId}/reports/operator-actions`, all protected by
  `reports.view` and audited on succeeded or denied reads.
- Extended the Operator App Shifts workspace typed API client and report loader
  to fetch all five report families with one date/limit filter set.
- Added Operator App report grids and summaries for gameplay duration/revenue,
  cash impact, and operator action groups.

Targeted verification on 2026-05-14 from `D:\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter ReportContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "EfReportServiceTests|ReportEndpointTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorShiftApiClientTests|ShiftWorkspaceViewModelTests|OperatorShellViewModelTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build src\AFK4.Operator.App\AFK4.Operator.App.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Report contract serialization tests passed 6/6.
- Report Platform API service/endpoint tests passed 20/20.
- Operator shift/report targeted tests passed 26/26.
- Operator App project build succeeded with 0 warnings and 0 errors.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 541/541:
  - BuildingBlocks 3/3;
  - Shared.Contracts 71/71;
  - Update.Publisher 3/3;
  - Agent.Service 65/65;
  - Player.Shell 11/11;
  - Operator.App 105/105;
  - Platform.Api 283/283.

Remaining Phase 11 work:

- diagnostics dashboards and backup/restore runbooks.

## Phase 11 Shift And Sales Reports Slice

Implemented on `codex/phase11-shift-sales-reports` after the Phase 11 audit
search merge commit `910a83e`.

Implemented in this Phase 11 reports slice:

- Added shared report contracts for branch-scoped shift report rows and sales
  report rows/totals.
- Added `reports.view` and mapped it to owner, branch manager, shift
  supervisor, and accountant/auditor roles.
- Added `EfReportService` over existing shift, cash movement, payment, POS
  sale/line, and shift-linked ledger persistence.
- Added `GET /api/branches/{branchId}/reports/shifts` protected by
  `reports.view`, with succeeded and denied audit records.
- Added `GET /api/branches/{branchId}/reports/sales` protected by
  `reports.view`, with succeeded and denied audit records.
- Added Operator App report loading in the Shifts workspace with date/limit
  filters, shift rows, sales rows, and sales gross/refund/net totals.

Targeted verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Shared.Contracts tests passed 68/68.
- Platform.Api tests passed 271/271.
- Operator.App tests passed 102/102.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 523/523:
  - BuildingBlocks 3/3;
  - Shared.Contracts 68/68;
  - Update.Publisher 3/3;
  - Agent.Service 65/65;
  - Player.Shell 11/11;
  - Operator.App 102/102;
  - Platform.Api 271/271.

## Phase 11 Audit Search First Slice

Started on `codex/phase11-audit-search-ops` after the Phase 10 signature
verification merge commit `329c72c`.

The focused Phase 11 plan was added at
`docs/superpowers/plans/2026-05-14-afk4-phase11-audit-reports-ops.md`.

Implemented in the first Phase 11 slice:

- Added shared audit search contracts for immutable audit rows and search
  result envelopes.
- Added `EfAuditSearchService` over existing `audit_records` with branch scope,
  action/outcome/target/date filters, newest-first ordering, and capped limits.
- Added `GET /api/branches/{branchId}/audit` protected by `audit.view`.
- Added succeeded and denied `audit.view` audit rows for audit read attempts.
- Added Operator App typed audit API client and Settings audit panel with
  action, outcome, target, date, and limit filters.

Targeted verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter AuditContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfAuditSearchServiceTests|AuditSearchEndpointTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorAuditApiClientTests|AuditSearchWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Audit contract serialization tests passed 1/1.
- Platform API audit service/endpoint tests passed 5/5.
- Operator audit/settings/shell targeted tests passed 17/17.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 508/508:
  - BuildingBlocks 3/3;
  - Shared.Contracts 66/66;
  - Update.Publisher 3/3;
  - Agent.Service 65/65;
  - Player.Shell 11/11;
  - Operator.App 97/97;
  - Platform.Api 263/263.

Remaining Phase 11 work after the audit-search slice is tracked in the
subsequent report sections above.

## Phase 10 Agent Update Signature Verification

Implemented after the Phase 10 publisher merge on 2026-05-14:

- Moved update package signature algorithm names and canonical package metadata
  payload construction into shared update contracts so the publisher and Agent
  verify the same bytes.
- Added `Agent:UpdatePackageSigningPublicKeyPem`.
- Extended `Sha256UpdatePackageVerifier` to reject unsupported algorithms,
  missing verification public keys, invalid signatures, and modified signed
  metadata before invoking installer execution.
- Documented Agent public-key configuration in the update package publishing
  runbook.

Verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter Sha256UpdatePackageVerifierTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Update.Publisher.Tests/AFK4.Update.Publisher.Tests.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter UpdateContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Targeted Agent update verifier tests passed 4/4.
- Full Agent Service tests passed 65/65.
- Update Publisher tests passed 3/3 after moving canonical signature helpers to
  shared contracts.
- Update contract serialization tests passed 6/6.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 497/497:
  - BuildingBlocks 3/3;
  - Shared.Contracts 65/65;
  - Update.Publisher 3/3;
  - Agent.Service 65/65;
  - Player.Shell 11/11;
  - Operator.App 92/92;
  - Platform.Api 258/258.

## Phase 10 Update Publishing Automation

Started on `codex/update-publisher-automation` after Operator update
visibility merge commit `9dcfecf`.

The focused Phase 10 plan was added at
`docs/superpowers/plans/2026-05-14-afk4-phase10-update-publishing-automation.md`.

Implemented in the first Phase 10 slice:

- Added `AFK4.Update.Publisher`, a local CLI that takes an already-built update
  artifact, copies it into a deterministic hosting path, computes SHA-256 and
  size from the copied artifact, signs canonical package metadata with ECDSA
  P-256 SHA-256, and emits `CreateUpdatePackageRequest` JSON for the existing
  package registration endpoint.
- Added a PowerShell wrapper at `scripts/publish-client-update.ps1` that
  publishes a Windows client project, zips the publish output under ignored
  `artifacts/update-packages/`, and calls the publisher.
- Added `docs/operations/update-package-publishing.md` for local key handling,
  artifact publishing, and package registration flow.

Verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Update.Publisher.Tests/AFK4.Update.Publisher.Tests.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Update Publisher tests passed 3/3.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 493/493:
  - BuildingBlocks 3/3;
  - Shared.Contracts 65/65;
  - Update.Publisher 3/3;
  - Agent.Service 61/61;
  - Player.Shell 11/11;
  - Operator.App 92/92;
  - Platform.Api 258/258.

Remaining Phase 10 work:

- provider-specific object-store/CDN and key-vault SDK adapters only if the
  presigned URL and environment-secret boundary is not enough for production;
- MSI/MSIX authoring decision and CI release jobs.

## Phase 9 Operator Update Visibility Follow-Up

Implemented after the Phase 9 merge on 2026-05-14:

- Added shared rollout status read contracts with per-device installed/target
  version, status, message, and update timestamp snapshots.
- Added backend `GET /api/branches/{branchId}/updates/rollouts` protected by
  `updates.status.view`, returning rollout list rows plus device status
  snapshots and writing update view audit records.
- Added `IUpdateService.ListRolloutStatusesAsync` over the existing
  update-rollout and device-status persistence tables.
- Added Operator App typed update API client and Settings workspace update
  status ViewModel.
- Added an Updates panel in Operator App Settings for technicians/managers with
  rollout target, batch, state, progress, schedule, and per-device status rows.
- Allowed the Settings workspace to be visible for staff that only have
  `updates.status.view`.

Verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter UpdateContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfUpdateServiceTests|UpdateEndpointTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorUpdateApiClientTests|UpdateStatusWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Results:

- Update contract serialization tests passed 6/6.
- Phase 9 Platform API service/endpoint tests passed 20/20.
- Operator update/settings/shell targeted tests passed 15/15.
- Full Operator App tests passed 92/92.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 490/490:
  - BuildingBlocks 3/3;
  - Shared.Contracts 65/65;
  - Agent.Service 61/61;
  - Player.Shell 11/11;
  - Operator.App 92/92;
  - Platform.Api 258/258.

## Phase 9 Updates And Installers

Started on `codex-phase9-updates-installers` after Phase 8 verification commit
`24ed9b2`.

The focused Phase 9 plan was added at
`docs/superpowers/plans/2026-05-14-afk4-phase9-updates-installers.md`.

Implemented in Phase 9 first slice:

- Added shared update contracts for package metadata, rollout targeting, device
  update checks, device update status reports, and package/rollout lifecycle
  requests;
- added backend EF persistence for update packages, rollouts, rollout targets,
  and per-device update status with migration `AddUpdateRollouts`;
- added staff-protected package/rollout/status endpoints with branch-scoped
  permissions and audit;
- added package state transitions for registered, validated, rejected, and
  retired package states;
- added rollout state transitions for active, paused, completed,
  rollback-requested, rolled-back, and cancelled rollout states;
- added device-credential update check/status endpoints for Agents;
- added staff-protected rollout status list reads for Operator visibility;
- added Agent update client boundary for checking available updates and
  reporting update progress;
- added Agent update execution coordinator and background update worker with
  component version reporting, artifact download, SHA-256 package verification,
  persisted recovery state, configurable external install/rollback/restart
  adapters, interrupted-install recovery, and offered/downloading/downloaded/
  installing/installed/failed/rollback-started/rolled-back status progression;
- added installer enrollment and rollout runbooks under `docs/operations/`;
- updated README with Phase 9 routes, Agent update channel configuration, and
  implementation state.

Phase 9 first-slice commits on the branch:

- `1896e17 docs: add phase 9 updates plan`
- `32112c4 feat: add update rollout contracts`
- `f7a83d5 feat: add update rollout persistence`
- `fcaa5d3 feat: expose update rollout endpoints`
- `1a33616 feat: add agent update client boundary`
- `b5d6375 docs: add installer update runbooks`
- `605899d feat: add update lifecycle contracts`
- `dc10529 feat: add update lifecycle transitions`

Verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter UpdateContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfUpdateServiceTests|UpdateEndpointTests" -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter HttpAgentUpdateClientTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations script --idempotent --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --output artifacts/phase9-update-rollouts.sql
```

Results:

- Update contract serialization tests passed 5/5.
- Phase 9 Platform API service/endpoint tests passed 18/18.
- Agent update client tests passed 3/3.
- Agent update execution, safe installer, rollback recovery, and background
  update worker tests passed 10/10.
- Full Agent Service tests passed 61/61.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 482/482:
  - BuildingBlocks 3/3;
  - Shared.Contracts 64/64;
  - Agent.Service 61/61;
  - Player.Shell 11/11;
  - Operator.App 87/87;
  - Platform.Api 256/256.
- EF migration idempotent SQL script generation succeeded; generated output is
  under ignored `artifacts/phase9-update-rollouts.sql`.

Verification notes:

- The endpoint tests cover package registration, package state changes, rollout
  creation/status read, rollout state changes, device update check, and device
  update status report through the in-memory test host.
- A live PostgreSQL smoke harness was prepared under ignored
  `artifacts/phase9-update-smoke/`. It starts a temporary PostgreSQL instance,
  applies migrations, starts Platform API, registers and validates an update
  package, enrolls a device, creates a branch rollout, checks for updates,
  reports device update status, pauses/resumes/completes the rollout, and
  verifies update rows and audit rows.
- After Docker was installed on 2026-05-14, the repository `compose.yaml`
  PostgreSQL service was started on `127.0.0.1:5432`, EF migrations were
  applied through `20260514081906_AddUpdateRollouts`, and the ignored Phase 9
  live PostgreSQL smoke harness passed. The harness used a temporary PostgreSQL
  container on port `55543` because the original harness port `55439` was in a
  Windows excluded port range. The command was:

```powershell
powershell -ExecutionPolicy Bypass -File artifacts/phase9-update-smoke/phase9-update-smoke.ps1
```

- Tooling checks on 2026-05-14:

```powershell
docker --version
docker compose version
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
powershell -ExecutionPolicy Bypass -File artifacts/phase9-update-smoke/phase9-update-smoke.ps1
```

- Results: Docker `29.4.3`, Compose `v5.1.3`, `afk4-postgres` healthy on
  `127.0.0.1:5432`, Phase 9 smoke passed, and
  `artifacts/phase9-update-smoke/phase9-update-smoke-result.json` reported
  one validated package, one completed rollout, one installed device status,
  first update check count `1`, and paused update check count `0`.

Out of Phase 9 first-slice scope:

- web admin, local club server, microservices, non-Windows agents, and kernel
  drivers;
- binary artifact hosting and production signing key storage;
- CI installer build automation;
- production binary artifact hosting and installer build automation;
- Operator App update package/rollout management UI;
- reports, audit search, diagnostics dashboards, and backup/restore runbooks.

## Phase 8 Agent Enforcement And Player Shell

Started on `codex-phase8-agent-enforcement-player-shell` after `main` commit
`1dc8dd2`.

The focused Phase 8 plan was added at
`docs/superpowers/plans/2026-05-14-afk4-phase8-agent-enforcement-player-shell.md`.

Implemented in Phase 8:

- Added shared local Player Shell contracts under
  `AFK4.Shared.Contracts.Shell`:
  - `PlayerShellStateNames`;
  - `LauncherAppDto`;
  - `PlayerShellStateDto`;
  - `PlayerShellCommandDto`;
  - `PlayerShellCommandResultDto`.
- Added Player Shell contract serialization tests.
- Added persistent Agent runtime state foundation:
  - `AgentOptions.StateDirectory`;
  - `AgentRuntimeState`;
  - `IAgentRuntimeStateStore`;
  - file-backed `AgentRuntimeStateStore`;
  - file-backed `FileSessionLeaseStore`;
  - registration of file-backed lease storage in the Agent runtime.
- Added Agent runtime/lease persistence tests.
- Added Agent session enforcement coordinator:
  - `ISessionEnforcementCoordinator`;
  - `SessionEnforcementCoordinator`;
  - `IWorkstationLockController`;
  - MVP-safe `WorkstationLockController` boundary;
  - `SessionEnforcementResult`;
  - command handler delegation for `unlock`, `refresh-session-lease`, and
    `lock`.
- Added enforcement coordinator tests and updated existing command handler
  tests to use the coordinator path.
- Added grace lease expiry enforcement:
  - `IGraceModeMonitor`;
  - `GraceModeMonitor`;
  - Worker integration before reconciliation and before each heartbeat;
  - heartbeat and reconciliation now use `IAgentRuntimeStateStore.Current`
    instead of hardcoded `isLocked: true`.
- Added grace monitor tests and Worker coverage for runtime lock-state
  heartbeat payloads.
- Added Player Shell process supervision and local IPC:
  - `IPlayerShellProcessSupervisor`;
  - `PlayerShellProcessSupervisor`;
  - process query/starter boundaries for testable supervision;
  - `IPlayerShellStatePublisher`;
  - named-pipe Player Shell state publishing from Agent;
  - Player Shell named-pipe state client.
- Added Player Shell process supervisor tests.
- Added Player Shell MVVM session UI:
  - `PlayerShellViewModel`;
  - `RemainingTimeFormatter`;
  - `LauncherAppViewModel`;
  - fullscreen WPF layout for locked, active, warning, grace, and launcher
    states;
  - Player Shell test project and ViewModel/formatter tests.
- Added local launcher and process policy foundation:
  - Agent launcher allow-list options;
  - process launcher boundary;
  - process policy enforcer and denied-process terminator boundary;
  - Player Shell command handler for `launch-app`;
  - named-pipe Player Shell command server hosted by Agent;
  - Player Shell launcher command client;
  - launcher command and policy tests.

Phase 8 Task 8 verification scope is complete:

- full Phase 8 targeted verification;
- full solution build/test verification;
- local Agent/Player Shell IPC smoke evidence.

Phase 8 keeps centralized updates/installers, reports, audit search, web admin,
local server, microservices, non-Windows agents, and kernel drivers out of
scope.

Phase 8 commits on the branch:

- `5e66fa9 docs: add phase 8 agent shell plan`
- `ec8f84f feat: add player shell local contracts`
- `472683d feat: persist agent runtime lease state`
- `be856cb feat: add agent session enforcement coordinator`
- `e9f06e0 feat: enforce grace lease expiry in agent`
- `da20a9c feat: supervise player shell runtime`
- `39db4a4 feat: add player shell session UI`
- `fc6cbcd feat: add local launcher policy foundation`
- `c329e7 fix: overwrite persisted agent state safely`

Verification on 2026-05-14 from `D:\projects\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj --no-restore -p:UseSharedCompilation=false
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; & 'C:\Program Files\dotnet\dotnet.exe' restore src/AFK4.Agent.Service/AFK4.Agent.Service.csproj -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build src/AFK4.Agent.Service/AFK4.Agent.Service.csproj --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' restore src/AFK4.Player.Shell/AFK4.Player.Shell.csproj -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build src/AFK4.Player.Shell/AFK4.Player.Shell.csproj --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter PlayerShellContractSerializationTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "AgentRuntimeStateStoreTests|FileSessionLeaseStoreTests|SessionEnforcementCoordinatorTests|GraceModeMonitorTests|PlayerShellProcessSupervisorTests|PlayerShellCommandHandlerTests|WorkerTests" --no-restore -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' run --project artifacts/phase8-smoke/Phase8Smoke.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false
```

Results:

- Shared contracts build succeeded with 0 warnings and 0 errors.
- Agent Service restore succeeded.
- Agent Service build succeeded with 0 warnings and 0 errors.
- Player Shell restore succeeded.
- Player Shell build succeeded with 0 warnings and 0 errors.
- Shared Phase 8 Player Shell contract tests passed 3/3.
- Agent targeted Phase 8 tests passed 27/27.
- Player Shell tests passed 11/11.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution test run passed 446/446:
  - BuildingBlocks 3/3;
  - Shared.Contracts 59/59;
  - Agent.Service 48/48;
  - Player.Shell 11/11;
  - Operator.App 87/87;
  - Platform.Api 238/238.
- Local Agent/Player Shell IPC smoke passed with state publish/read,
  allowed-launch command handling, and unavailable command-pipe rejection.

Verification notes:

- Initial test-project restore hit transient NuGet timeout retries for
  xUnit/coverlet packages, then completed successfully on this device.
- The first targeted Agent test run found a Windows overwrite issue while
  replacing persisted Agent state files. Commit `c329e7` switched the runtime
  state and lease stores to overwrite via copy plus temp-file cleanup.
- The local smoke harness lives under ignored `artifacts/phase8-smoke` and is
  intentionally not tracked as production code.

Known planning environment notes:

- The current working clone is `D:\projects\afk4.net`; `AGENTS.md` still names
  `D:\afk4.net` as the historical project root.
- Git and .NET SDK `10.0.203` are installed on this device.
- Creating the branch required elevated filesystem access to update `.git`.

## Phase 7 Operator App Production UX

Started on `codex/phase7-operator-app-production-ux` after `main` commit
`fc762ea`.

Fast-forward merged to `main` at `55e56b4` after merged-main verification. The
feature branch `codex/phase7-operator-app-production-ux` was deleted after
merge.

- Added the focused Phase 7 implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase7-operator-app-production-ux.md`.
- Replaced the Operator App static shell with authenticated WPF/MVVM operator
  workspaces:
  - staff sign-in and protected token storage;
  - permission-filtered navigation;
  - realtime floor-map loading and device-status merge;
  - selected-seat session context actions;
  - player search, player creation, wallet top-up, debt payment, and package
    summary refresh;
  - POS catalog, cart, cash/card-manual payment, refund, void, sale read, and
    receipt read workflow;
  - shift open/current/cash movement/close workflow;
  - settings workspace with connection, device technician tools, POS catalog,
    stock, tariffs, packages, roles, and audit panels filtered by permission;
  - production hotkeys for floor map, session actions, refresh, POS, players,
    shifts, sign-out, and transient selection clearing.
- Added backend Operator reference read endpoints for player search, tariff
  options, and package options, with branch permission checks and denied audit
  coverage.
- Kept the backend authoritative for sessions, money, POS, shifts, and device
  state; Operator App state remains UI acceleration only.

Phase 7 commits on the branch:

- `7db9525 docs: add phase 7 operator ux plan`
- `97f21c1 feat: add operator auth shell`
- `c9be82e feat: load realtime operator floor map`
- `ccf1ca1 feat: add operator session context actions`
- `e3f5dec feat: add operator reference data endpoints`
- `05e4a82 feat: add operator player search workflow`
- `d87f754 feat: add operator pos workflow`
- `ca1040b feat: add operator shift workflow`
- `bf6836f feat: add operator settings workspace`
- `62aec89 feat: integrate operator production layout`

Final Phase 7 verification was run from `D:\afk4.net` on 2026-05-14:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter OperatorReferenceContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter OperatorReferenceDataEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- Operator reference shared contract tests passed 3/3.
- Operator reference Platform API endpoint tests passed 4/4.
- Operator App tests passed 87/87.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 409/409:
  - BuildingBlocks 3/3;
  - Shared.Contracts 56/56;
  - Operator.App 87/87;
  - Agent.Service 25/25;
  - Platform.Api 238/238.

The Phase 7 local PostgreSQL operator-workflow smoke was run from
`D:\afk4.net` on 2026-05-14 against a temporary PostgreSQL 17 container on
localhost port `55435` and Platform API on `http://localhost:5074`. A temporary
ECDSA PEM key pair was generated with Git for Windows OpenSSL for session lease
signing. EF migrations were applied through
`20260513170215_AddPosInventoryShiftsReceipts`.

Smoke commands included:

```powershell
docker run --rm -d --name afk4-phase7-smoke-postgres -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=afk4_dev -p 55435:5432 postgres:17-alpine
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --connection "Host=localhost;Port=55435;Database=afk4_dev;Username=postgres" --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074 --no-build
```

The smoke harness then exercised the API paths consumed by the WPF Operator
App ViewModels.

Live smoke results:

- `GET /api/health` returned `ok`.
- Branch manager sign-in returned 37 permissions.
- Cashier/operator sign-in returned 19 permissions and did not include
  manager-only settings permissions such as `identity.roles.manage`,
  `pos.catalog.manage`, or `devices.detail.view`.
- Device enrollment and credential-authenticated heartbeat succeeded.
- Floor map returned seeded seat `PC-PHASE7-001` in `Main Hall` with the
  enrolled device assignment.
- Shift open/current succeeded.
- Player creation and player search returned one matching smoke player.
- POS category/product creation, stock purchase movement, and catalog read
  succeeded; catalog returned stock on hand `24`.
- Tariff creation/version/calculation, tariff options, package definition, and
  package options succeeded.
- Prepaid wallet top-up succeeded with wallet balance `100000`.
- Session start returned state `active`, floor map showed the active session,
  session extend returned the same session id, and session end accepted the
  session command.
- POS sale create/pay/read and receipt read succeeded; paid sale state was
  `paid` and receipt number was `POS-20260513-0001`.
- Device detail read returned assigned seat `PC-PHASE7-001`.
- Cash movement and shift close succeeded with expected cash `157400`, counted
  cash `157400`, and difference `0`.
- The API process, temporary PostgreSQL container, and temporary signing key
  files were stopped/removed after smoke verification.

Known Phase 7 limitations:

- The live smoke covered the API-backed WPF ViewModel paths and automated
  Operator App ViewModel tests; a manual interactive WPF desktop smoke was not
  run in this environment.
- Agent enforcement and Player Shell production UX remain out of Phase 7 and
  belong to Phase 8.
- Centralized updates/installers, reports, audit search, diagnostics, and
  backup/restore runbooks remain out of Phase 7 and belong to Phase 9.
- POS payments remain manual/mock; fiscal printers, tax authority integration,
  card acquirer integration, and external payment gateways remain out of MVP
  scope unless the PRD and architecture spec change first.

## Phase 6 POS, Inventory, Shifts, And Receipts

Started on `codex/phase6-pos-inventory-shifts-receipts` after `main` commit
`3400873`:

- Added the focused Phase 6 implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase6-pos-inventory-shifts-receipts.md`.
- Added shared contracts and permission constants for:
  - operator shifts and cash movements;
  - POS catalog categories and products;
  - append-only inventory stock movements and derived stock projection;
  - manual payment requests and payment method names;
  - POS sale states, sale lines, refunds, and voids;
  - receipt projections.
- Added shift persistence and services:
  - `shifts`;
  - `cash_movements`;
  - open/current/close shift flows;
  - cash movement append flow;
  - command idempotency through `billing_command_idempotency`.
- Added nullable `ledger_entries.ShiftId` and required future
  money-changing ledger writes to resolve an open shift before appending
  entries.
- Added POS catalog and inventory persistence/services:
  - `pos_product_categories`;
  - `pos_products`;
  - `stock_movements`;
  - stock on hand derived from stock movement history.
- Added POS sale, manual payment, refund, void, and receipt foundation:
  - `pos_sales`;
  - `pos_sale_lines`;
  - `payments`;
  - `receipts`;
  - manual provider support for `cash` and `card_manual`;
  - receipt numbers with `POS-YYYYMMDD-0001` and `REF-YYYYMMDD-0001`
    branch/date/type sequences.
- Added protected backend endpoints for:
  - shift open/current/cash movement/close;
  - POS categories/products/catalog;
  - inventory stock movements;
  - POS sale create/pay/refund/void/read;
  - receipt reads.
- Added audit action names and role permission mapping for protected Phase 6
  actions.
- Generated EF Core migration `AddPosInventoryShiftsReceipts` for the Phase 6
  schema.

Final Phase 6 verification was run from `D:\afk4.net` on 2026-05-13:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "ShiftContractSerializationTests|InventoryContractSerializationTests|PaymentContractSerializationTests|PosContractSerializationTests|ReceiptContractSerializationTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfShiftServiceTests|BillingShiftIntegrationTests|EfInventoryServiceTests|EfPosServiceTests|ReceiptNumberGeneratorTests|PosEndpointTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- EF migration generation for `AddPosInventoryShiftsReceipts` completed after
  a successful design-time build in the migration/runbook task.
- Migration sanity confirmed creation of `shifts`, `cash_movements`,
  `pos_product_categories`, `pos_products`, `stock_movements`, `pos_sales`,
  `pos_sale_lines`, `payments`, and `receipts`.
- Migration sanity confirmed nullable `ledger_entries.ShiftId` and index
  `IX_ledger_entries_ShiftId_CreatedAtUtc`.
- Forbidden schema field grep found no mutable stock/cash/wallet/debt balance
  columns and no fiscal or external payment gateway tables.
- Shared contract tests passed 26/26.
- Targeted Phase 6 Platform API tests passed 42/42 after adding coverage for
  shift close reconciliation with cash movements, POS cash payments/refunds,
  and cash-impacting shift-linked ledger entries.
- A live PostgreSQL smoke regression found that `debt_payment` ledger entries
  are stored as negative debt-account movements while they must count as
  positive cash drawer inflow during shift close reconciliation. The regression
  test was updated to use the real ledger sign and passed after fixing
  `EfShiftService`.
- Full solution build succeeded with 0 warnings and 0 errors.
- Full solution tests passed 336/336.
- Scope self-review confirmed no Operator production UX files were changed, no
  fiscal/payment gateway/web-admin/local-server/microservice code was added,
  POS sales remain separate from billing ledger entries, stock projection is
  derived from `stock_movements`, money-changing billing entries are linked to
  open shifts through nullable `ShiftId`, critical Phase 6 commands have
  idempotency coverage, and protected endpoint tests cover allowed and denied
  audit paths.

The Phase 6 local PostgreSQL live smoke was run from `D:\afk4.net` on
2026-05-13 against a temporary PostgreSQL 17 container on localhost port
`55434` and Platform API on `http://localhost:5074`. A temporary ECDSA PEM key
pair was generated with Git for Windows OpenSSL for session lease signing.

Live smoke results:

- EF migrations applied through
  `20260513170215_AddPosInventoryShiftsReceipts`.
- Staff sign-in and refresh succeeded for the seeded branch manager, with
  Phase 6 permissions present.
- Device enrollment, heartbeat, installed-app report, command dispatch/status,
  command result, credential rotation, and credential revocation succeeded.
- Floor map returned the seeded `Main Hall` seat assigned to `PC-SMOKE-001`.
- Shift open/current, final cash movement, and shift close succeeded; direct
  PostgreSQL inspection confirmed the closed shift had expected/count/difference
  cash values `158100/158100/0`.
- POS category/product creation, purchase stock movement, catalog read, sale
  create/pay/read/refund, and receipt reads succeeded.
- Direct PostgreSQL inspection confirmed POS sale state `refunded`, sale total
  `2400`, one sale line snapshot `Smoke Cola 0.5:2:1200:2400`, cash payment
  `2400`, cash refund `-2400`, sale receipt `POS-20260513-0001`, refund
  receipt `REF-20260513-0001`, and stock movements `purchase:24`, `sale:-2`,
  `refund:2`.
- Billing, tariff, package, prepaid wallet session start/extend, package
  extension, postpaid debt start/payment, gameplay refund, active
  reconciliation, session end, and ending reconciliation succeeded.
- Direct PostgreSQL inspection confirmed protected Phase 6 audit rows for
  shift, POS, inventory, device command, and device credential actions.
- Direct PostgreSQL inspection confirmed `ledger_entries.ShiftId` was populated
  for all smoke money-changing entries.
- The API process and temporary PostgreSQL container were stopped after smoke
  verification.

Known Phase 6 limitations:

- Operator production POS/shift/inventory UX is not implemented in this phase.
- Agent enforcement and Player Shell UI changes are out of this phase.
- Fiscal printers, tax authority integration, card acquirer integrations, and
  external payment gateways are out of this phase.
- Shift cash reconciliation includes cash movements, POS cash provider
  payments/refunds, and cash-impacting linked ledger entries such as top-ups,
  debt payments, and manual corrections; it intentionally excludes wallet
  gameplay charges and package time consumption from drawer cash.

## Phase 5 Billing, Ledger, Tariffs, And Packages

Started on `codex/phase5-billing-ledger-tariffs-packages` after `main` commit
`c0e7927`:

- Added the focused Phase 5 implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase5-billing-ledger-tariffs-packages.md`.
- Added shared contracts for:
  - player account creation;
  - wallet summaries, top-ups, refunds, debt payments, and manual ledger
    corrections;
  - tariff creation, versioning, and calculation;
  - package definitions, package purchases, and player package projections;
  - session billing modes on start/extend requests.
- Added EF Core billing persistence and migration
  `AddBillingLedgerTariffsPackages` for:
  - `player_accounts`;
  - `ledger_entries`;
  - `billing_command_idempotency`;
  - `tariffs`;
  - `tariff_versions`;
  - `package_definitions`;
  - `player_packages`.
- Kept balances derived from immutable `ledger_entries`; no mutable wallet,
  debt, or package balance columns were added.
- Added ledger projection for wallet/debt and package remaining seconds.
- Added idempotent billing command handling for player creation, wallet
  top-ups, refunds, debt payments, manual corrections, tariff changes, and
  package purchase flows.
- Added tariff versioning and calculation foundation with billable-minute
  rounding.
- Added package definition, purchase, and ledger-backed time consumption
  foundation.
- Added protected backend endpoints for player creation, billing summaries,
  wallet/debt/refund/manual correction flows, tariff management/calculation,
  package management/purchase, and player package reads.
- Integrated session start/extend with prepaid wallet, postpaid debt, and
  package-backed billing modes.
- Added authenticated HTTP fallback for device command results returned through
  heartbeat polling when the realtime hub is unavailable.

Latest Phase 5 verification:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddBillingLedgerTariffsPackages --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "BillingContractSerializationTests|TariffContractSerializationTests|PackageContractSerializationTests|SessionContractSerializationTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "LedgerBalanceProjectorTests|EfBillingCommandServiceTests|EfTariffServiceTests|EfPackageServiceTests|BillingEndpointTests|EfSessionBillingIntegrationTests|EfSessionCommandServiceTests|SessionEndpointTests|DeviceHeartbeatServicePersistenceTests|DeviceCommandEndpointTests|DeviceCommandDispatchServiceTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
docker run --rm -d --name afk4-phase5-smoke-postgres -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=afk4_dev -p 55433:5432 postgres:17-alpine
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --connection "Host=localhost;Port=55433;Database=afk4_dev;Username=postgres" --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

Results:

- local `dotnet-ef` 10.0.4 restored successfully;
- EF migration generation completed after a successful design-time build;
- targeted shared contract tests passed 9/9;
- targeted Platform API Phase 5/session/device fallback tests passed 137/137;
- Agent Service tests passed 25/25;
- full solution build succeeded with 0 warnings and 0 errors;
- full solution tests passed 268/268;
- Phase 5 live PostgreSQL smoke passed on a temporary PostgreSQL container
  bound to `localhost:55433`;
- migration sanity confirmed the seven Phase 5 tables and expected indexes for
  player accounts, ledger chronology, session/package ledger lookups, billing
  idempotency, tariff versions, tariffs, package definitions, and player
  package purchases;
- forbidden schema field grep found no `wallet_balance`, `debt_balance`,
  `remaining_seconds`, or `remaining_bonus_seconds` columns in generated
  migrations.

Phase 5 local PostgreSQL live smoke was run from `D:\afk4.net` on 2026-05-13
using a temporary PostgreSQL container on `localhost:55433`, because the usual
local PostgreSQL ports were not available for the smoke harness. The temporary
API process used `http://localhost:5074` and a temporary ECDSA signing key
generated outside the repository.

Live smoke results:

- PostgreSQL accepted connections on `localhost:55433`.
- EF migrations applied through
  `20260513151112_AddBillingLedgerTariffsPackages`.
- `GET /api/health` returned `status = ok`.
- local branch manager sign-in and refresh returned non-empty tokens and Phase
  5 billing/tariff/package permissions.
- two devices enrolled and authenticated heartbeats succeeded for
  `PC-SMOKE-001` and `PC-SMOKE-002`.
- persisted floor map returned both smoke seats/devices.
- player account creation returned player
  `65b9b565-eb5c-4ff5-890c-85f3e12a0fc2`.
- wallet top-up with `smoke-topup-001` was idempotent and returned wallet
  balance `100000`.
- manual wallet correction with `smoke-manual-correction-001` succeeded.
- tariff/version creation succeeded and 60-minute calculation returned `6000`
  minor units.
- prepaid wallet session start with `smoke-start-prepaid-001` returned session
  `da0939b5-54cb-40c6-b657-09c18309d14b`; repeating the start returned the
  same session id.
- postpaid session start with `smoke-start-debt-001` created debt `3000`, and
  debt payment with `smoke-debt-pay-001` reduced debt balance to `0`.
- package definition/purchase succeeded, package-backed extension consumed time,
  and player package projection reported `6900` seconds remaining.
- prepaid wallet extension succeeded with lease sequence `3`.
- refund with `smoke-refund-001` succeeded.
- active reconciliation returned `continue`; ending reconciliation returned
  `lock`.
- installed apps report persisted two apps and device detail reflected
  `installedAppCount = 2`.
- device command HTTP fallback moved command status from `Pending` to
  `Accepted`.
- device credential rotation, heartbeat with the rotated credential, and
  revocation succeeded.
- SQL inspection confirmed rows in `player_accounts`, `ledger_entries`,
  `billing_command_idempotency`, `tariffs`, `tariff_versions`,
  `package_definitions`, `player_packages`, `sessions`,
  `session_command_idempotency`, `device_commands`, and `audit_records`.
- SQL ledger inspection confirmed `debt_payment`, `gameplay_charge`,
  `manual_correction`, `package_consumption`, `package_purchase`,
  `postpaid_debt`, `refund`, and `top_up` entries.
- Temporary API process was stopped; temporary PostgreSQL container was removed;
  temporary private/public key files were removed.

Known Phase 5 limitations:

- Operator production UX is not implemented in this phase.
- POS, inventory, shifts, and receipts remain out of this phase.
- Agent enforcement and Player Shell billing UI remain out of this phase.
- Phase 4 still leaves ended sessions in `ending` until a later Agent
  acknowledgement/completion workflow; run billing-mode session smoke starts on
  fresh or separate assigned seats.

## Implemented Foundation

- Repository baseline with `global.json`, `Directory.Build.props`,
  `.editorconfig`, `.gitignore`, and expanded `README.md`.
- `AFK4.sln` with backend, shared contracts, building blocks, Agent Service,
  Operator App, Player Shell, and tests.
- Strongly typed Guid ID primitives in `AFK4.BuildingBlocks`.
- Shared DTO contracts for device heartbeat and floor map.
- Platform API endpoints:
  - `GET /api/health`
  - `GET /api/branches/{branchId}/floor-map`
  - `POST /api/devices/{deviceId}/heartbeat`
- SignalR hub at `/hubs/devices`.
- Server broadcast event `deviceStatusChanged`.
- Agent Service options, heartbeat payload factory, and HTTP heartbeat worker
  loop.
- WPF Operator App shell with floor map ViewModel.
- WPF Player Shell fullscreen locked-state skeleton.
- Master MVP PRD at `docs/product/AFK4-MVP-PRD.md`.

## Realtime Device Channel

Implemented on branch `feature/realtime-device-channel`:

- Shared realtime contracts and stable event/method names for device
  connection, command dispatch, and command result acknowledgement.
- Backend device hub registration and `POST /api/devices/{deviceId}/commands`
  dispatch endpoint with basic command request validation.
- Agent SignalR client, command acknowledgements, reconnect re-registration,
  and HTTP heartbeat compatibility when realtime startup fails.
- Operator realtime status state path with dispatcher-safe ViewModel updates
  and startup failure handling.

## Device Enrollment, Credentials, And Command Status

Implemented on `main` after the realtime device channel merge:

- Shared contracts for enrollment code creation, device enrollment responses,
  device credential headers, and command status responses.
- Backend in-memory enrollment code flow:
  - `POST /api/branches/{branchId}/device-enrollment-codes`
  - `POST /api/devices/enroll`
- Enrollment-issued device credentials with server-side hashed secret storage.
- Heartbeat credential validation through `X-AFK4-Device-Credential`.
- SignalR device registration credential validation through
  `DeviceConnectionRequest.CredentialSecret`.
- Backend in-memory command status tracking:
  - commands are stored as `Pending` when dispatched;
  - reported command results update the stored status;
  - `GET /api/devices/{deviceId}/commands/{commandId}/status` returns current
    status.
- Agent options and HTTP heartbeat now carry the enrollment-issued device
  credential secret.

## PostgreSQL-Backed Device Persistence

Implemented on `main` after the in-memory device management foundation:

- Added `PlatformDbContext` with EF Core/Npgsql configuration.
- Added initial migration `AddDevicePersistence` for:
  - `devices`
  - `device_credentials`
  - `device_enrollment_codes`
  - `device_commands`
- Replaced runtime device enrollment and credential validation with
  `EfDeviceEnrollmentService`.
- Replaced runtime command status storage with `EfDeviceCommandStore`.
- Replaced heartbeat handling with `DeviceHeartbeatService`, which persists the
  latest device heartbeat state before broadcasting realtime status.
- Added API test host override that uses EF InMemory provider so automated tests
  do not require a local PostgreSQL server.

## Local PostgreSQL Runbook And Smoke

Implemented on `codex/local-postgres-smoke-runbook` after commit `69a7f4c`:

- Added root `compose.yaml` for a localhost-bound PostgreSQL dev database.
- Added `.config/dotnet-tools.json` to pin local `dotnet-ef` at version
  `10.0.4`.
- Added `docs/operations/local-postgres-smoke.md` with commands for:
  - starting PostgreSQL through Docker Compose;
  - restoring the EF CLI tool;
  - applying EF migrations;
  - starting the Platform API;
  - running health, device enrollment, heartbeat, command creation, and command
    status checks.
- Linked the runbook from `README.md`.

## Phase 2 Identity, Tenancy, RBAC, And Audit Baseline

Started on `codex/phase2-identity-tenancy-rbac-audit` after commit `892c3d3`:

- Added a focused implementation plan at
  `docs/superpowers/plans/2026-05-12-afk4-phase2-identity-tenancy-rbac-audit.md`.
- Added shared staff sign-in contracts and explicit permission names.
- Added EF Core entities and migration `AddIdentityTenancyAndAudit` for:
  - `organizations`
  - `branches`
  - `staff_users`
  - `staff_role_assignments`
  - `staff_access_tokens`
  - `audit_records`
- Added EF Core entity and migration `AddStaffRefreshTokens` for
  `staff_refresh_tokens`.
- Added predefined MVP role-to-permission mapping.
- Added staff sign-in through `POST /api/auth/staff/sign-in` with opaque
  bearer access tokens stored as hashes.
- Added `POST /api/auth/staff/refresh` with refresh token rotation,
  hash-only refresh token storage, and replay rejection.
- Added request-time staff context resolution from `Authorization: Bearer`.
- Added branch-scoped authorization for
  `POST /api/branches/{branchId}/device-enrollment-codes`.
- Added audit records for allowed and denied device enrollment-code creation.
- Added staff authorization and audit coverage for:
  - `POST /api/devices/{deviceId}/commands` with
    `devices.commands.dispatch`;
  - `GET /api/devices/{deviceId}/commands/{commandId}/status` with
    `devices.commands.status.view`.
- Added shared contracts for credential rotation and revocation responses.
- Added `EfDeviceCredentialLifecycleService`:
  - rotation revokes current active credentials and issues a new hashed-secret
    credential;
  - revocation marks the target credential with `RevokedAtUtc`;
  - existing credential validation rejects revoked credentials.
- Added staff authorization and audit coverage for:
  - `POST /api/devices/{deviceId}/credentials/rotate` with
    `devices.credentials.rotate`;
  - `POST /api/devices/{deviceId}/credentials/{credentialId}/revoke` with
    `devices.credentials.revoke`.
- Added Operator App protected token storage abstraction using Windows DPAPI
  for access and refresh token snapshots.
- Updated API tests so device enrollment and heartbeat setup use an authorized
  technician before creating enrollment codes.
- Added a shared Operator-facing `DispatchDeviceCommandRequest` contract.
- Added Operator App typed device API client coverage for:
  - bearer-authenticated enrollment-code creation;
  - device command dispatch;
  - device command status inspection;
  - credential rotation;
  - credential revocation.
- Added a dense WPF/MVVM technician panel on the existing floor-map shell for
  enrollment, command status, and credential lifecycle workflows.

## Phase 3 Club Layout And Device Management

Started on `codex/phase2-identity-tenancy-rbac-audit` after commit `f072f6d`:

- Added a focused implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase3-club-layout-device-management.md`.
- Expanded the shared floor-map seat contract with persisted layout and device
  attachment fields:
  - `ZoneId`
  - `SortOrder`
  - `DeviceId`
  - `DeviceName`
  - `IsDeviceOnline`
  - `IsDeviceLocked`
  - `LastHeartbeatAtUtc`
  - `AgentVersion`
  - `ShellVersion`
- Added EF Core entities and migration `AddClubLayout` for:
  - `zones`
  - `seats`
  - `device_seat_assignments`
- Added `EfFloorMapReadService`, which builds the branch floor map from
  persisted branch, zone, seat, active device-seat assignment, and latest
  device heartbeat state.
- Replaced the runtime floor-map service registration with the EF-backed read
  service.
- Protected `GET /api/branches/{branchId}/floor-map` with staff bearer
  permission `floor_map.view`.
- Added tests for:
  - floor-map contract serialization with persisted layout/device fields;
  - EF floor-map read service state projection;
  - unauthorized, forbidden, and authorized persisted floor-map endpoint paths.
- Added shared installed app report contracts:
  - `InstalledAppReportRequest`;
  - `InstalledAppDto`.
- Added EF Core entity and migration `AddDeviceInstalledApps` for
  `device_installed_apps`.
- Added device-authenticated
  `POST /api/devices/{deviceId}/installed-apps/report`, which replaces the
  latest installed app snapshot rows for the reporting device.
- Added route/body/credential identity validation for installed apps reports:
  - route `deviceId` must match request `DeviceId`;
  - the supplied device credential must match the request organization, branch,
    and device.
- Added tests for:
  - installed app report contract serialization;
  - successful snapshot replacement;
  - missing credential rejection;
  - route/request device mismatch rejection;
  - credential-for-different-device rejection.
- Added shared device detail contract `DeviceDetailDto`.
- Added permission `devices.detail.view` and mapped it for owner, branch
  manager, shift supervisor, and technician roles.
- Added staff-protected `GET /api/devices/{deviceId}`, returning:
  - device identity and branch;
  - latest heartbeat online/locked state and component versions;
  - current assigned seat and zone;
  - active credential count;
  - recent command statuses;
  - installed app count.
- Added tests for:
  - device detail contract serialization;
  - unauthenticated device detail rejection;
  - branch staff without `devices.detail.view` rejection;
  - authorized technician detail read;
  - unknown device `404`.
- Extended the Operator App typed device API client with
  `GetDeviceDetailAsync`.
- Added technician ViewModel state and command for loading device detail:
  - machine name;
  - assigned seat and zone;
  - online/lock state;
  - Agent/Shell versions;
  - active credential count;
  - installed app count;
  - most recent command summary.
- Added the device detail block to the existing dense WPF technician panel.
- Added Operator tests for:
  - typed client `GET /api/devices/{deviceId}`;
  - ViewModel detail refresh state projection.
- Added Agent-side installed app inventory collection:
  - Windows registry uninstall-key collector for machine and current-user apps;
  - registry entry mapper with stable UTC date parsing;
  - installed app report factory;
  - authenticated HTTP reporter for
    `POST /api/devices/{deviceId}/installed-apps/report`;
  - Worker startup report attempt before the heartbeat loop, with failures
    logged without stopping heartbeat.
- Added Agent tests for:
  - static collector behavior;
  - registry entry mapping and date parsing;
  - installed app report factory;
  - authenticated HTTP reporting;
  - Worker collection/report wiring.
- Extended `docs/operations/local-postgres-smoke.md` with a Phase 3 local
  PostgreSQL smoke path for:
  - seeded zone and seat rows;
  - active device-seat assignment;
  - bearer-authenticated persisted floor-map read;
  - device-authenticated installed apps report;
  - staff-protected device detail read;
  - direct PostgreSQL inspection of layout and installed app snapshot rows.

## Phase 4 Session Lifecycle And Grace Mode Foundation

Started on `codex/phase4-session-lifecycle-grace-mode` after commit `bdbc8fd`:

- Added a focused implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase4-session-lifecycle-grace-mode.md`.
- Added shared session contracts in `AFK4.Shared.Contracts.Sessions` for:
  - session states;
  - session DTOs and command responses;
  - start, extend, transfer, and end requests;
  - signed session leases;
  - device session snapshots;
  - reconciliation responses;
  - canonical lease signing payloads.
- Extended heartbeat and realtime connection contracts with active lease
  snapshot fields:
  - `ActiveSessionId`;
  - `ActiveSessionLeaseExpiresAtUtc`;
  - `ActiveSessionLeaseSequence`.
- Added session permissions and role mappings:
  - owner, branch manager, shift supervisor, and cashier/operator can start,
    extend, transfer, end, and view sessions;
  - accountant/auditor can view sessions;
  - technician does not receive session operator actions by default.
- Added session audit action names for start, extend, transfer, and end.
- Added EF Core session entities and migration `AddSessions` for:
  - `sessions`;
  - `session_events`;
  - `session_leases`;
  - `session_command_idempotency`.
- Added explicit session state-machine tests and implementation.
- Added backend ECDSA P-256 lease signing from configured
  `Sessions:SigningPrivateKeyPem`.
- Added idempotent session command service behavior for:
  - starting guest sessions;
  - extending active or paused sessions;
  - transferring active sessions to another assigned seat/device;
  - ending active or paused sessions into `ending` state.
- Session commands dispatch through the existing device command path:
  - `unlock`;
  - `refresh-session-lease`;
  - `lock`.
- Added staff-protected session endpoints:
  - `POST /api/branches/{branchId}/sessions/start`;
  - `POST /api/sessions/{sessionId}/extend`;
  - `POST /api/sessions/{sessionId}/transfer`;
  - `POST /api/sessions/{sessionId}/end`.
- Added floor-map active session projection with `ActiveSessionId`,
  `RemainingSeconds`, and session-aware seat state.
- Added Agent-side signed lease validation using configured
  `Agent:LeaseSigningPublicKeyPem`.
- Added Agent-side current lease storage and command handling:
  - `unlock` and `refresh-session-lease` require and validate a signed lease;
  - `lock` clears the matching current lease.
- Added Agent heartbeat and reconnect snapshots from the current lease store.
- Added device-authenticated
  `POST /api/devices/{deviceId}/session-reconciliation` with actions:
  - `continue` for matching active cloud/local lease state;
  - `unlock` when the backend has an active session but the Agent has no
    current local lease;
  - `lock` when the local lease is unknown, ended, or the cloud session is
    ending.
- Added Agent `SessionReconciliationReporter` and Worker startup reconciliation
  before installed app reporting and heartbeat loop.
- Extended `docs/operations/local-postgres-smoke.md` with a Phase 4 local
  PostgreSQL smoke path for signed lease configuration, session start
  idempotency, extend, optional transfer, end, reconciliation, and direct
  PostgreSQL inspection of session tables.

## Known Deviations And Adaptations

### Solution Format

`dotnet new sln` on the installed .NET 10 SDK defaulted to `.slnx`. The project
requires `AFK4.sln`, so the solution was created with:

```powershell
dotnet new sln -n AFK4 --format sln
```

### Agent HTTP Package

`Microsoft.Extensions.Http` was added to `AFK4.Agent.Service` because the Worker
uses `AddHttpClient` and `IHttpClientFactory`.

### API Error Shape

The heartbeat route/body `DeviceId` mismatch response currently returns an
object with `Error` and message:

```text
Route deviceId must match request DeviceId.
```

The implementation plan used lowercase `error` and a slightly different
message. This is not blocking for the slice because no client depends on the
error contract yet, but it should be normalized before treating API contracts
as stable.

### Device Management Foundation Scope

The current device enrollment, credential, heartbeat state, and command status
implementation now has PostgreSQL-backed runtime persistence through EF Core.
It is still a foundation slice, not the complete production device management
module.

Known limitations:

- enrollment code creation is now protected by staff identity, branch-scoped
  permission checks, and audit;
- device command dispatch and command status endpoints are now protected by
  staff identity, branch-scoped permission checks, and audit;
- device credential rotation and revocation endpoints are now protected by
  staff identity, branch-scoped permission checks, and audit;
- Operator App now has initial technician workflows for enrollment-code
  creation, command dispatch/status inspection, and credential
  rotation/revocation, backed by staff bearer tokens loaded from protected
  token storage;
- the technician panel is still a focused Phase 2 surface, not a full device
  management/settings area or web admin replacement.

### Phase 2 Baseline Scope

The current Phase 2 work is still a baseline plus the first focused Operator
App technician workflows. It does not yet include:

- Operator App sign-in UI and role-aware navigation;
- staff management workflows;
- custom role editing;
- audit search or reports;
- automatic Agent-side consumption of rotated credentials;
- authorization coverage for every future operator-facing endpoint as it is
  added.

### Phase 3 Baseline Scope

The current Phase 3 work has started with persisted layout and backend floor-map
reads only. It does not yet include:

- Operator App layout management UI for creating or editing zones, seats, or
  device-seat assignments;
- full installed app list read path, if needed beyond the current device detail
  installed app count;

### Phase 4 Baseline Scope

The current Phase 4 work adds the backend-authoritative session lifecycle and
grace-mode foundation only. It intentionally does not include:

- Operator App session action UI;
- Operator sign-in UI or role-aware navigation;
- web admin;
- local club server;
- billing ledger charging, POS integration, or tariff calculation in Phase 4;
  billing ledger/tariff foundations were added later in Phase 5, while POS
  remains deferred;
- real Windows lock/unlock enforcement or Player Shell session UI;
- Agent-side lease creation or renewal.

Known limitations:

- session leases are issued by the backend and validated by Agent, but real
  Windows lock/unlock enforcement remains deferred;
- reconciliation dispatches corrective `unlock`/`lock` commands through the
  existing device command path, but offline local event replay is not yet
  modeled beyond `PendingLocalEventCount`;
- session lifecycle audit is written for staff commands, while reconciliation
  is captured in `session_events`.

## Latest Verified State

Full verification was run from `D:\afk4.net` on 2026-05-13 after the Phase 4
session lifecycle and grace-mode foundation:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 149 visible passing tests, 0 failed, 0 skipped.

Targeted TDD verification for the Phase 4 session lifecycle and grace-mode
foundation was also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter SessionContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "SessionStateMachineTests|SessionLeaseSignerTests|EfSessionCommandServiceTests|SessionEndpointTests|SessionReconciliationEndpointTests|EfFloorMapReadServiceTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "SessionLeaseValidatorTests|SessionCommandHandlerLeaseTests|HeartbeatPayloadFactoryTests|SessionReconciliationReporterTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `SessionContractSerializationTests` passed with 2 visible passing tests;
- targeted Platform API session tests passed with 34 visible passing tests;
- targeted Agent lease/reconciliation tests passed with 12 visible passing
  tests.

The Phase 4 local PostgreSQL smoke path was documented in
`docs/operations/local-postgres-smoke.md`, including signed lease key
configuration, idempotent start/extend/end, optional transfer, reconciliation,
and direct session table inspection.

The Phase 4 local PostgreSQL live smoke was run from `D:\afk4.net` on
2026-05-13 using a temporary PostgreSQL container on `localhost:55432` because
port `5432` was already occupied by another Docker project:

```powershell
docker run --rm -d --name afk4-phase4-smoke-postgres -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=afk4_dev -p 55432:5432 postgres:17-alpine
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --connection "Host=localhost;Port=55432;Database=afk4_dev;Username=postgres"
& 'C:\Program Files\dotnet\dotnet.exe' run --no-launch-profile --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- EF migrations applied through `20260513064045_AddSessions`.
- Platform API health returned `status = ok`.
- Local branch manager sign-in and refresh returned bearer tokens with session
  permissions.
- Device enrollment-code creation, device enrollment, and device-authenticated
  heartbeat succeeded.
- Seeded zone `Main Hall`, seat `PC-SMOKE-001`, and active device-seat
  assignment were returned by the persisted floor-map endpoint.
- `POST /api/branches/{branchId}/sessions/start` returned an active session
  with a signed lease.
- Repeating start with idempotency key `smoke-start-001` returned the same
  `sessionId`.
- `POST /api/sessions/{sessionId}/extend` returned a refreshed signed lease
  with sequence `2`.
- Active device reconciliation returned action `continue`.
- `POST /api/sessions/{sessionId}/end` moved the session to `ending`.
- Ending-state reconciliation returned action `lock`.
- Direct PostgreSQL inspection confirmed:
  - one `sessions` row in `ending` state;
  - two `session_leases` rows with non-empty signatures;
  - `session-started`, `session-extended`, `device-reconciled`, and
    `session-ending` rows in `session_events`;
  - `start`, `extend`, and `end` rows in `session_command_idempotency`;
  - `unlock`, `refresh-session-lease`, and `lock` rows in `device_commands`;
  - succeeded audit rows for session start, repeated start, extend, and end.
- The API process was stopped after smoke verification.
- The temporary PostgreSQL container was stopped and removed, and the temporary
  lease-signing private key file was deleted.

Full verification was run from `D:\afk4.net` on 2026-05-13 after the Phase 3
local PostgreSQL smoke path update:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 101 visible passing tests, 0 failed, 0 skipped.

The full local PostgreSQL live smoke was run from `D:\afk4.net` on 2026-05-13
for the Phase 3 path on `codex/phase3-installed-apps-reporting`:

```powershell
docker compose down -v
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- PostgreSQL healthcheck reached `healthy`.
- EF migrations applied through `AddDeviceInstalledApps`.
- `GET /api/health` returned status `ok`.
- local technician sign-in and refresh returned non-empty access tokens.
- bearer-authenticated device enrollment-code creation returned
  `AFK4-A70C-BB7607073B40`.
- device enrollment returned device
  `e14e78b8-b69d-4d52-a63e-64af11c6e0d7`.
- authenticated heartbeat returned heartbeat interval `10`.
- seeded zone `Main Hall`, seat `PC-SMOKE-001`, and active device-seat
  assignment were returned by the bearer-authenticated floor-map endpoint with
  state `Locked`.
- device-authenticated installed apps report persisted `Counter-Strike 2` and
  `Discord`.
- staff-protected device detail returned machine `PC-SMOKE-001`,
  installed app count `2`, active credential count `1`, assigned seat
  `PC-SMOKE-001`, and zone `Main Hall`.
- bearer-authenticated command dispatch/status returned command status
  `Pending`.
- credential rotation returned credential
  `af62be84-93fe-4197-9966-aa2552750380`.
- heartbeat with the rotated credential returned heartbeat interval `10`.
- credential revocation returned the rotated credential id.
- Direct PostgreSQL reads confirmed the seeded layout row with online/locked
  device state.
- Direct PostgreSQL reads confirmed installed app snapshot rows for
  `Counter-Strike 2` and `Discord`.
- Direct PostgreSQL reads confirmed succeeded audit rows for enrollment-code
  creation, command dispatch/status, credential rotation, and credential
  revocation.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

Targeted TDD verification for the Phase 3 Operator device detail workflow was
also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorDeviceApiClientTests|TechnicianDeviceWorkflowViewModelTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `OperatorDeviceApiClientTests` and
  `TechnicianDeviceWorkflowViewModelTests` failed first because the typed
  detail client method and ViewModel detail state did not exist, then passed
  after GREEN;
- targeted Operator tests passed with 12 visible passing tests.

Targeted TDD verification for the Phase 3 Agent installed app inventory
collection was also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "InstalledAppInventoryTests|WorkerTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `InstalledAppInventoryTests` failed first because the Agent inventory
  snapshot, report factory, reporter, and registry mapper did not exist, then
  passed after GREEN;
- `WorkerTests` failed first because Worker did not collect/report installed
  apps, then passed after GREEN;
- targeted Agent tests passed with 6 visible passing tests.

Targeted TDD verification for the Phase 3 device detail backend slice was also
run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DeviceDetailContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceDetailEndpointTests --no-restore -p:UseSharedCompilation=false
```

Results:

- `DeviceDetailContractSerializationTests` failed first because
  `DeviceDetailDto` did not exist, then passed after GREEN;
- `DeviceDetailEndpointTests` failed first because `GET /api/devices/{deviceId}`
  returned `404`, then passed after GREEN.

Targeted TDD verification for the Phase 3 installed apps reporting slice was
also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter InstalledAppReportContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter InstalledAppsEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Results:

- `InstalledAppReportContractSerializationTests` failed first because the
  installed app report contracts did not exist, then passed after GREEN;
- `InstalledAppsEndpointTests` failed first because `DeviceInstalledAppEntity`
  and `PlatformDbContext.DeviceInstalledApps` did not exist, then passed after
  GREEN;
- full Platform API tests passed with 44 visible passing tests, 0 failed,
  0 skipped.

Targeted TDD verification for the Phase 3 persisted floor-map slice was also
run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfFloorMapReadServiceTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FloorMapEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfFloorMapReadServiceTests|FloorMapEndpointTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `ContractSerializationTests` failed first because `SeatStatusDto` did not
  expose persisted layout/device attachment fields, then passed after GREEN;
- `EfFloorMapReadServiceTests` failed first because layout entities and the EF
  floor-map service did not exist, then passed after GREEN;
- `FloorMapEndpointTests` failed first because the endpoint was anonymous and
  returned in-memory demo seats, then passed after GREEN;
- targeted Platform API floor-map tests passed with 4 visible passing tests.

Full verification was run from `D:\afk4.net` on 2026-05-12 after the Operator
App technician workflow changes:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 81 visible passing tests, 0 failed, 0 skipped.

Targeted TDD verification for the Operator technician workflows was also run
for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DispatchDeviceCommandRequestSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorDeviceApiClientTests|TechnicianDeviceWorkflowViewModelTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Results:

- `DispatchDeviceCommandRequestSerializationTests` failed first because the
  shared command request contract did not exist, then passed after GREEN;
- `OperatorDeviceApiClientTests` and
  `TechnicianDeviceWorkflowViewModelTests` failed first because the Operator
  device client and technician ViewModel did not exist, then passed after
  GREEN;
- full Operator App tests passed with 19 visible passing tests.

Full verification was run from `D:\afk4.net` on 2026-05-12 after the Phase 2
device credential lifecycle changes:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 70 visible passing tests, 0 failed, 0 skipped.

Targeted TDD verification for device credential lifecycle was also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DeviceCredentialLifecycleContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfDeviceCredentialLifecycleServiceTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceCredentialLifecycleEndpointTests --no-restore -p:UseSharedCompilation=false
```

Results:

- credential lifecycle contract tests passed after the contract RED/GREEN
  cycle;
- EF credential lifecycle service tests passed after the service RED/GREEN
  cycle;
- credential rotation/revocation endpoint authorization and audit tests passed
  after the endpoint RED/GREEN cycle;
- the full Platform API test project now has 37 visible passing tests.

The local PostgreSQL live smoke was rerun from `D:\afk4.net` on 2026-05-12
after the device credential lifecycle changes:

```powershell
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- PostgreSQL healthcheck reached `healthy`.
- EF database update completed; no new migration was required for credential
  lifecycle because `device_credentials.RevokedAtUtc` already existed.
- `GET /api/health` returned status `ok`.
- local technician sign-in and refresh returned non-empty access tokens.
- bearer-authenticated
  `POST /api/branches/{branchId}/device-enrollment-codes` returned enrollment
  code `AFK4-F8B7-973E65CD9C33`.
- `POST /api/devices/enroll` returned device
  `d72f0084-4445-485e-8463-f3c2b4cefdd2`.
- authenticated `POST /api/devices/{deviceId}/heartbeat` returned heartbeat
  interval `10`.
- bearer-authenticated `POST /api/devices/{deviceId}/commands` followed by
  command status read returned `Pending`.
- bearer-authenticated
  `POST /api/devices/{deviceId}/credentials/rotate` returned credential
  `ba299c3c-c8c2-4a08-833e-d684ce6eb8a0`.
- heartbeat with the rotated credential returned heartbeat interval `10`.
- bearer-authenticated
  `POST /api/devices/{deviceId}/credentials/{credentialId}/revoke` returned
  the rotated credential id.
- Direct PostgreSQL reads confirmed succeeded audit rows for enrollment-code
  creation, device command dispatch/status, credential rotation, and credential
  revocation.
- Direct PostgreSQL reads confirmed two revoked credential rows for the smoke
  device after rotation plus revocation.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

The local PostgreSQL live smoke was rerun from `D:\afk4.net` on 2026-05-12
after the Phase 2 refresh-token and command authorization changes:

```powershell
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- PostgreSQL healthcheck reached `healthy`.
- EF migration `20260512092839_AddStaffRefreshTokens` applied to PostgreSQL.
- `GET /api/health` returned status `ok`.
- local technician sign-in returned non-empty access and refresh tokens.
- `POST /api/auth/staff/refresh` returned a rotated token pair, and replay of
  the old refresh token returned `401`.
- bearer-authenticated
  `POST /api/branches/{branchId}/device-enrollment-codes` returned enrollment
  code `AFK4-1D03-44405C36A3C9`.
- `POST /api/devices/enroll` returned device
  `6c70a84f-6246-4fe7-aa7b-e6d2f4d38f87` and credential
  `12f392fd-8eca-448d-a9fc-e9c95651e83c`.
- authenticated `POST /api/devices/{deviceId}/heartbeat` returned heartbeat
  interval `10`.
- bearer-authenticated `POST /api/devices/{deviceId}/commands` created command
  `b1d839b7-90c8-4158-8668-88e1488cbe5a`.
- bearer-authenticated
  `GET /api/devices/{deviceId}/commands/{commandId}/status` returned
  `Pending`.
- Direct PostgreSQL reads confirmed audit rows for
  `devices.enrollment_codes.create`, `devices.commands.dispatch`, and
  `devices.commands.status.view`, all with `Succeeded` outcome.
- Direct PostgreSQL reads confirmed the latest smoke device had
  `IsOnline = true`, `IsLocked = true`, and a non-null heartbeat timestamp.
- Direct PostgreSQL reads confirmed the latest smoke command stored
  `Type = lock`, `Status = Pending`, and payload
  `{"reason": "local-postgres-smoke"}`.
- Direct PostgreSQL reads confirmed `staff_refresh_tokens` contained revoked
  rows after refresh rotation.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

Local PostgreSQL live smoke was run from `D:\afk4.net` on 2026-05-12 after the
Docker Compose runbook was added:

```powershell
docker compose config
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- Docker Compose parsed successfully and PostgreSQL healthcheck reached
  `healthy`.
- EF migration `20260512060601_AddDevicePersistence` applied to PostgreSQL.
- PostgreSQL contained `__EFMigrationsHistory`, `devices`,
  `device_credentials`, `device_enrollment_codes`, and `device_commands`.
- `GET /api/health` returned status `ok`.
- `POST /api/branches/{branchId}/device-enrollment-codes` returned enrollment
  code `AFK4-0E77-3FA32297EE7E`.
- `POST /api/devices/enroll` returned device
  `f7f1066b-94b0-4578-be48-75d5512df551` and credential
  `7cf3a039-0c1b-47cf-a824-4deb0e781c8b`.
- Authenticated `POST /api/devices/{deviceId}/heartbeat` returned heartbeat
  interval `10`.
- `POST /api/devices/{deviceId}/commands` created command
  `d5b7c37a-5c01-4668-a9f8-e05f3211d4ec`.
- `GET /api/devices/{deviceId}/commands/{commandId}/status` returned
  `Pending`.
- Direct PostgreSQL reads confirmed the latest smoke device had
  `IsOnline = true`, `IsLocked = true`, and a non-null heartbeat timestamp.
- Direct PostgreSQL reads confirmed the latest smoke command stored
  `Type = lock`, `Status = Pending`, and payload
  `{"reason": "local-postgres-smoke"}`.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

Previous live smoke was run on `http://localhost:5074` for the realtime device
channel:

- `GET /api/health` returned status `ok`;
- `POST /api/devices/d76eff15-9cf9-4c30-a6d4-c05fd215793f/commands`
  returned `DeviceCommandDto` responses for `lock` commands;
- Agent Service was started with the requested organization, branch, device,
  machine, and platform URL environment variables;
- Agent logs showed realtime connection for
  `d76eff15-9cf9-4c30-a6d4-c05fd215793f`;
- after sending another command, Agent logs showed command acknowledgement as
  `Accepted`;
- backend and Agent smoke processes were stopped after verification.

WPF Operator App live smoke was not run in this environment. The automated
Operator App tests are the current proof for the dispatcher-safe realtime
status state path, startup failure handling, protected token storage, typed
device API client, and technician workflow ViewModel behavior.

## Recent Key Commits

- `62aec89 feat: integrate operator production layout`
- `bf6836f feat: add operator settings workspace`
- `ca1040b feat: add operator shift workflow`
- `d87f754 feat: add operator pos workflow`
- `05e4a82 feat: add operator player search workflow`
- `e3f5dec feat: add operator reference data endpoints`
- `ccf1ca1 feat: add operator session context actions`
- `c9be82e feat: load realtime operator floor map`
- `97f21c1 feat: add operator auth shell`
- `7db9525 docs: add phase 7 operator ux plan`
- `fc762ea fix: reconcile debt payments in shift close`
- `aa8ddb1 feat: add protected pos shift endpoints`
- `fbc7006 feat: add pos sale payment receipt service`
- `3daeba2 feat: add pos catalog inventory service`
- `5bb99ad feat: add shift foundation and ledger shift links`
- `4575813 feat: add pos shift inventory contracts`
- `9b9977f docs: add phase 6 pos inventory plan`
- `3400873 docs: record phase 5 live smoke`
- `81376af docs: clarify phase 5 progress scope`
- `50c1189 docs: record phase 5 verification`
- `5490b19 docs: add phase 5 migration and runbook`
- `bdbc8fd docs: add phase 4 session lifecycle plan`
- `f072f6d docs: add phase 3 club layout plan`
- `69a7f4c feat: persist device state and commands with ef core`
- `a176363 fix: harden operator realtime startup and dispatch`
- `8fe0a2e feat: add operator realtime floor map state`
- `ebcaa61 fix: keep agent heartbeat alive across realtime failures`
- `d329897 feat: add agent realtime device client`
- `0ab297f test: cover device command SignalR dispatch`
- `b35be64 feat: add backend device realtime command dispatch`
- `ab681d5 feat: add realtime device contracts`
- `003ab43 docs: add realtime device channel plan`
- `d176048 merge: integrate vertical slice foundation`
- `57b2763 docs: add mvp product requirements`

## Recommended Next Work

1. Add Operator App package/rollout management workflow when update package
   publishing needs to be run by operators instead of scripts.
2. Decide MSI/MSIX/WiX packaging per Windows client surface and add CI release
   jobs once provider choices are explicit.
3. Add diagnostics dashboards and backup/restore runbooks.
4. Keep web admin, local server, microservices, non-Windows agents, and kernel
   drivers out of MVP scope unless the PRD and architecture spec are updated
   first.
