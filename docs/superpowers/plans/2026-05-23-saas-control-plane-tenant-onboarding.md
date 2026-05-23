# AFK4 SaaS Control Plane And Tenant Onboarding Plan

Status: Draft for implementation
Date: 2026-05-23
Owner: AFK4 platform

## Goal

Add an internal SaaS Control Plane so AFK4 can onboard and manage clubs as
customers without direct database edits, Coolify-only manual steps, or raw GUID
copying in normal setup flows.

The Control Plane is for the AFK4 platform owner/support role. Day-to-day club
operations remain in the native Windows Operator App.

## Scope

The first implementation must provide:

- platform-admin authentication and authorization separate from branch staff;
- organization and first-branch provisioning with stable slugs;
- owner invite or setup-code creation, expiry, acceptance, and revocation;
- tenant lifecycle status: active, suspended, and deletion-pending;
- plan code, subscription status, basic limits, and support notes;
- suspend/reactivate with required reason and audit records;
- tenant health view with branch/device counts, latest operator sign-in,
  latest known migration version when available, and recent support-relevant
  backend errors;
- no-DB-edit staging smoke for creating a tenant, signing in as owner, and
  opening the native Operator App against the created branch.

## Non-Goals

- Customer-facing browser operations console for cashiers/operators.
- Local club server.
- Payment provider subscription billing automation.
- Full CRM, invoicing, or tax/fiscal integrations.
- Custom role editor beyond the current predefined branch-role model.
- Multi-region deployment automation.

## Product Decisions

- The MVP now includes an internal browser-based SaaS Control Plane.
- The native Operator App remains the primary and required club operations UI.
- Platform-admin identity is not a branch staff identity.
- Platform-admin cross-tenant actions are allowed only through explicit
  Control Plane permissions and audit.
- Tenant onboarding must not require writing directly to PostgreSQL in staging
  or production.
- Operator App setup should use tenant/branch slugs or setup codes, not copied
  organization/branch GUIDs.

## Architecture Shape

### Backend

Implement the first backend slice inside `src/AFK4.Platform.Api` as part of the
modular monolith. Keep endpoints under a platform-admin route boundary such as
`/api/platform/...`.

Recommended modules and responsibilities:

- Identity: platform-admin users, platform-admin sign-in, refresh rotation, and
  platform-admin permissions.
- Tenancy: organization/branch slugs, tenant lifecycle status, plan code,
  subscription status, limits, support notes, and tenant status enforcement.
- Platform Control Plane: orchestrates tenant provisioning, owner invites,
  suspend/reactivate, and tenant health projections.
- Audit: records platform-admin actions with actor, target tenant, reason, old
  state, new state, and source app.

Do not let Control Plane application services mutate another module's owned
data through table shortcuts. Use explicit services/contracts inside the
modular monolith.

### Web Surface

Add a small internal web app after the backend contract is stable. It should be
dense, operational, and support-focused:

- tenant list with status, plan, branch count, device count, and last activity;
- tenant detail with organization/branch slugs, subscription/status, limits,
  owner invites, support notes, health, and audit trail links;
- create tenant form;
- suspend/reactivate flow with required reason;
- owner invite create/revoke actions;
- no marketing layout or general landing page.

The web app can be deployed as separate static assets or alongside the backend,
but it must be logically separate from the Operator App WebView2 assets.

### Operator App Connection

The Operator App should stop requiring normal users to know organization and
branch GUIDs. Add a connection step that accepts one of:

- organization slug plus branch slug;
- owner invite/setup code that resolves the tenant and branch;
- support-provided environment defaults for developer/staging smoke only.

Resolved tenant/branch context is stored through the native protected storage
boundary. Suspended/disabled tenants must show actionable blocked-state copy
and prevent operational workflows.

## Implementation Slices

### Slice 1: Contracts, Data, And Platform-Admin Auth

- Add shared contracts for platform-admin auth, tenant summary/detail, tenant
  create/update, owner invite, tenant status, plan/status metadata, support
  notes, and tenant health.
- Add EF migration for tenant slugs/status/plan metadata/limits/support notes
  and owner invites.
- Add platform-admin credential model and seeded local/staging development
  admin path.
- Add platform-admin sign-in/refresh/sign-out tests separate from staff auth.
- Add authorization policies for platform-admin permissions.

### Slice 2: Tenant Provisioning API

- `POST /api/platform/tenants` creates organization, first branch, slugs,
  default branch roles, and owner invite idempotently.
- `GET /api/platform/tenants` lists tenant summaries.
- `GET /api/platform/tenants/{organizationId}` returns detail.
- `POST /api/platform/tenants/{organizationId}/owner-invites` creates or
  rotates owner invites.
- Invite acceptance creates or activates the first owner staff account in the
  target tenant.
- Tests cover slug uniqueness, idempotency, audit, and cross-tenant isolation.

### Slice 3: Lifecycle, Limits, And Enforcement

- Add tenant status update endpoints for suspend/reactivate/deletion-pending.
- Require reason text for status changes.
- Enforce suspended tenant blocks on money, session, POS, device enrollment,
  update rollout, and mutable settings endpoints.
- Preserve read-only support/report access where the permission model allows it.
- Tests cover blocked mutations and allowed reads.

### Slice 4: Tenant Health And Support Notes

- Add health projection endpoint for branch/device counts, latest operator
  sign-in, latest known migration version, recent backend errors where logs are
  available, and latest support actions.
- Add support notes create/update/list with audit.
- Keep support notes out of customer-facing Operator App workflows.

### Slice 5: Internal Control Plane Web

- Add minimal frontend with tenant list, tenant detail, create tenant,
  owner-invite controls, status controls, support notes, and health view.
- Use platform-admin tokens only.
- Include empty/loading/error states and permission-denied states.
- Verify desktop and narrow viewport layout.

### Slice 6: Operator App Connection Without GUIDs

- Add backend endpoint to resolve organization/branch slugs or setup code into
  allowed connection metadata.
- Add Operator App connection screen before staff sign-in when no branch context
  is stored.
- Store resolved context through native protected storage.
- Keep environment-variable org/branch GUIDs as a developer/staging override.
- Add smoke tests for invalid slug, suspended tenant, revoked invite, and valid
  owner/staff sign-in.

## Required Tests

- Contract serialization tests for all new DTOs.
- EF migration test or integration coverage for new uniqueness constraints.
- Platform-admin auth unauthorized/forbidden/success tests.
- Staff token rejected from `/api/platform/...` tests.
- Platform-admin token rejected from branch staff endpoints unless an explicit
  support impersonation design is later approved.
- Organization slug uniqueness.
- Branch slug uniqueness scoped to organization.
- Tenant provisioning idempotency.
- Owner invite expiry, revocation, and acceptance.
- Tenant status transition audit.
- Suspended tenant mutation blocks for sessions, POS, billing, devices,
  settings, and update rollouts.
- Read-only support/report behavior for suspended tenant where explicitly
  allowed.
- Cross-tenant isolation and no accidental branch context fallback.
- Operator App connection resolution without raw GUIDs.

## Staging Smoke

Run this after backend and minimum connection path exist:

1. Deploy branch to staging with migrations and backup confirmation.
2. Confirm PostgreSQL remains non-public after migration/setup.
3. Sign in as platform admin.
4. Create a tenant and first branch through API or Control Plane UI.
5. Accept owner invite and sign in as owner.
6. Open native Operator App using slug/setup-code flow.
7. Verify floor map, dashboard, POS catalog, current shift, devices, audit, and
   branch profile with the new tenant.
8. Suspend tenant and verify new session/POS/device/update mutations are
   blocked.
9. Reactivate tenant and verify normal read/write flow returns.
10. Review app logs for errors after the smoke.

## Rollout Notes

- Do not expose the staging database publicly except for explicit migration or
  emergency maintenance windows, and close it immediately after the operation.
- Rotate any platform-admin bootstrap secret shared outside a secret manager.
- Keep staging smoke credentials out of committed files.
- If billing provider integration is not ready, store plan code and
  subscription status as platform-owned metadata.

## Completion Criteria

- A platform admin can create, inspect, suspend, reactivate, and support a
  tenant without direct database edits.
- A new owner can accept an invite and sign in.
- The native Operator App can connect to the new tenant without raw GUID copy.
- Staging smoke proves DB remains in safe network mode and the new tenant can
  execute read-only and core writable operator flows after activation.
- Progress and roadmap docs record the verified state and remaining gaps.

## Slice Status

### Slice 1: Contracts, Data, And Platform-Admin Auth — completed 2026-05-23 on `codex/saas-control-plane-foundation`

Deliverables:

- Shared contracts under `src/AFK4.Shared.Contracts/Platform/` for platform-admin
  auth (`PlatformAdminSignInRequest/Response`, refresh, sign-out, role/permission
  name constants), tenant summary/detail, tenant create/update/status/plan/limits
  requests, tenant status/plan/subscription/owner-invite-status name constants,
  owner invite DTO, support note DTO, and tenant health DTO. Round-trip
  serialization is covered by 14 new tests in `tests/AFK4.Shared.Contracts.Tests/Platform/`.
- `OrganizationEntity` extended with `Slug`, `Status`, `StatusReason`,
  `StatusChangedAtUtc`, `PlanCode`, `SubscriptionStatus`, `LimitsJson`,
  `UpdatedAtUtc`. `BranchEntity` extended with `Slug`. New entities
  `PlatformAdminUserEntity`, `PlatformAdminAccessTokenEntity`,
  `PlatformAdminRefreshTokenEntity`, `OwnerInviteEntity`,
  `TenantSupportNoteEntity` plus DbSets and EF model config in
  `PlatformDbContext`. Unique indexes: organization slug (global), branch slug
  (per organization), platform-admin normalized user name, owner invite
  normalized code.
- EF migration `20260523103547_AddSaasControlPlaneFoundation` adds the columns,
  tables, and indexes. Existing rows are backfilled to deterministic globally
  unique slugs (`org-<12 hex>` / `branch-<12 hex>`) and `UpdatedAtUtc` is set to
  `CreatedAtUtc`. Transient backfill defaults on `Slug` and `UpdatedAtUtc` are
  dropped after backfill so application code must set them on inserts. `LimitsJson`
  uses `'{}'::jsonb` as default to satisfy the not-null jsonb constraint.
- Platform-admin auth pipeline under `src/AFK4.Platform.Api/Platform/Identity/`:
  `PlatformAdminContext`, `IPlatformAdminContextAccessor` /
  `PlatformAdminContextAccessor`, `IPlatformAdminTokenService` /
  `OpaquePlatformAdminTokenService` (8h access / 30d refresh opaque tokens,
  SHA-256 hashed at rest, refresh rotation, sign-out revokes refresh + sibling
  access tokens), `IPlatformAdminCredentialService` /
  `PasswordHashingPlatformAdminCredentialService` (ASP.NET `PasswordHasher`),
  `PlatformAdminAuthenticationMiddleware`, `PlatformAdminAuthorizationResult`,
  `PlatformAdminAuthorizationService.RequirePermission(...)`, and
  `PlatformAdminPermissionCatalog` mapping `platform_owner` /
  `platform_support` roles to permission sets.
- `PlatformAdminBootstrapHostedService` reads
  `PlatformAdmin:Bootstrap:{UserName,DisplayName,Password,Roles}` from
  configuration and creates the first platform admin if the
  `platform_admin_users` table is empty. Empty/partial configuration is a no-op.
  Unknown role names fall back to `platform_owner`. The seed writes an
  `identity.platform_admin.bootstrap` audit record. `appsettings.Development.json`
  ships a local-only `admin@afk4.local / ChangeMe!Local-1` admin; staging /
  production must override the password through environment variables or a
  secret manager — never commit real credentials.
- Endpoints in `Program.cs`: `POST /api/platform/auth/sign-in`,
  `/api/platform/auth/refresh`, and `/api/platform/auth/sign-out`. Each writes
  `identity.platform_admin.*` audit records (Succeeded / Denied). Sign-out
  requires an authenticated platform admin; the staff auth middleware and the
  new platform-admin middleware run in parallel and populate independent
  contexts so cross-token misuse is rejected at the endpoint authorization
  layer rather than via path-based filters.
- Audit action constants in `AuditActionNames`:
  `identity.platform_admin.sign_in/refresh/sign_out/bootstrap` and
  `tenancy.tenant.create/status.update/plan.update/limits.update/view`,
  `tenancy.owner_invite.create/accept/revoke`,
  `tenancy.support_note.create`, `tenancy.tenant.health.view` (the
  `tenancy.*` actions are placeholders for Slice 2–4 endpoints).
- API tests under `tests/AFK4.Platform.Api.Tests/Platform/`:
  sign-in success / wrong password / unknown user / inactive admin / audit
  outcomes; refresh rotation + replay rejection + expired-token rejection;
  sign-out revokes and prevents subsequent refresh; staff refresh token rejected
  at `/api/platform/auth/refresh`; platform-admin access token rejected at
  `/api/branches/{branchId}/floor-map`. Bootstrap hosted-service tests cover
  empty-config skip, empty-table seed + audit, populated-table skip, and
  unknown-role fallback. `PlatformApiFactory` clears bootstrap configuration
  via `PostConfigure` so tests control admin seeding explicitly.
- `HealthEndpointTests` switched to `PlatformApiFactory` because the bootstrap
  hosted service now requires a configured DbContext at startup. Raw
  `WebApplicationFactory<Program>` would otherwise try to query Npgsql against
  a non-existent local PostgreSQL.

Verification (WSL Linux, `D:\projects\afk4.net` mounted at
`/mnt/d/projects/afk4.net`):

- `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`: 3 projects,
  0 errors, 0 warnings.
- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 19 projects,
  0 errors, 0 warnings.
- `dotnet test tests/AFK4.Shared.Contracts.Tests/...`: 101 passed, 0 failed.
- `dotnet test tests/AFK4.Platform.Api.Tests/...`: 386 passed, 0 failed.
- `dotnet test AFK4.sln`: full Linux-runnable suites green
  (`BuildingBlocks` 3/3, `GamingPc.Setup` 10/10, `Update.Publisher` 8/8,
  `Shared.Contracts` 101/101, `Platform.Api` 386/386). Pre-existing Linux
  environment limitations apply: 22 of 140 `Agent.Service.Tests` fail because
  `ClientReleaseAutomationTests` shell out to `powershell.exe` (not present in
  WSL), and `Operator.App.Tests` / `Player.Shell.Tests` target `net10.0-windows`
  and require Windows tooling. These are not Slice 1 regressions and need
  Windows verification before merge.

Scope explicitly deferred to later slices:

- Slice 2 — tenant provisioning APIs (`POST /api/platform/tenants`,
  `GET .../tenants`, `GET .../tenants/{id}`, `POST .../owner-invites`),
  organization/branch slug uniqueness enforcement at the service layer, owner
  invite acceptance flow.
- Slice 3 — tenant lifecycle endpoints (status update, suspension enforcement
  across money / session / POS / device / update mutations, allowed read paths).
- Slice 4 — tenant health endpoint and support notes CRUD.
- Slice 5 — internal Control Plane web UI.
- Slice 6 — Operator App slug / setup-code connection flow.

Operational handoff for Slice 2:

- Migrations include a Slug backfill for any existing organizations/branches.
  Verify staging by applying the migration to the Coolify-managed PostgreSQL
  with a backup window first, then confirm slugs look like `org-<hex>` /
  `branch-<hex>` for the pre-existing tenant; the platform admin can rename
  them through Slice 2 endpoints once they ship.
- Bootstrap admin password in `appsettings.Development.json` is a known local
  credential. Staging / production must override via
  `PlatformAdmin__Bootstrap__Password` (and the rest of the section) through
  Coolify environment variables and rotate it before the first non-developer
  platform admin sign-in.
- Local Linux WSL editors have been silently rewriting some text files with
  CRLF line endings, producing a large background diff in unrelated files.
  Slice 1 normalized only the files it actually edits; the repo should consider
  adding a `.gitattributes` (`* text=auto eol=lf`) and running
  `git add --renormalize .` as a separate cleanup commit before Slice 2.
