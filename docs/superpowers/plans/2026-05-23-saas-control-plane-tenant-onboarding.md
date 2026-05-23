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

### Slice 2: Tenant Provisioning API — completed 2026-05-23 on `codex/saas-control-plane-slice-2`

Deliverables:

- Two new shared contracts: `CreateTenantResponse(Tenant, OwnerInvite)`
  bundles the freshly created tenant detail with its first owner invite;
  `AcceptOwnerInviteRequest(Code, UserName, DisplayName, Password)` is the
  public payload owners submit when claiming their invite. Round-trip tests
  added to the existing tenant + invite contract test files.
- New tenancy module under `src/AFK4.Platform.Api/Platform/Tenancy/`:
  `SlugValidator` (lowercase a-z 0-9, 3-64 chars, hyphen between alphanumeric
  segments, no leading/trailing hyphen), `IOwnerInviteCodeGenerator` +
  `RandomOwnerInviteCodeGenerator` (16 bytes → 32 lowercase hex chars / 128
  bits of entropy), `PlatformTenantOptions` (default 7-day invite lifetime,
  30-day cap), and the result envelope
  `PlatformTenantOperationResult<T>` (Succeeded / BadRequest / Conflict /
  NotFound).
- `EfPlatformTenantService` implements:
  - `CreateAsync` — validates slugs/name/city/plan/subscription/limits/
    owner-name/owner-display/lifetime, checks organization slug isn't taken,
    inserts organization + first branch + owner invite atomically (single
    `SaveChangesAsync`), and returns `CreateTenantResponse`.
  - `ListAsync` — projects `TenantSummaryDto` ordered by organization name
    with per-org branch count.
  - `GetAsync` — returns `TenantDetailDto` with parsed limits + branches.
  - `CreateOrRotateOwnerInviteAsync` — verifies tenant + branch belong
    together, revokes any pending invites for that branch
    (`status=revoked, reason="Rotated by platform admin."`), and issues a
    fresh pending invite.
  - `AcceptOwnerInviteAsync` — validates request, looks up invite by
    normalized code, marks expired invites and rejects them, rejects
    non-pending invites, rejects duplicate user-name within the organization,
    creates the staff user with hashed password + `owner` role assignment
    for the invite's branch, marks the invite accepted, and finally issues a
    `StaffSignInResponse` via `IStaffTokenService.IssueAsync` so the owner
    is signed in immediately.
- Endpoints in `Program.cs`, all under `/api/platform/...`:
  - `POST /api/platform/tenants` — `platform.tenants.create` permission.
  - `GET /api/platform/tenants` — `platform.tenants.view` permission.
  - `GET /api/platform/tenants/{organizationId:guid}` — same view permission,
    404 if unknown.
  - `POST /api/platform/tenants/{organizationId:guid}/owner-invites` —
    `platform.tenants.invites.manage` permission.
  - `POST /api/platform/owner-invites/accept` — public (the invite code is
    the credential).
- New `WritePlatformAuditAsync(...)` helper writes audit records with
  `ActorStaffUserId = null` and a `{ actorPlatformAdminUserId, payload }`
  details envelope. Each endpoint writes succeeded/denied records under
  `tenancy.tenant.create`, `tenancy.tenant.view`, `tenancy.owner_invite.create`,
  or `tenancy.owner_invite.accept` (action constants reserved in Slice 1).
- `IPlatformTenantService`, `IOwnerInviteCodeGenerator`, and
  `PlatformTenantOptions` registered in DI; tenant options bind from
  `PlatformTenant:` configuration section.
- `PlatformAdminTestHelper` gained `AuthorizeAsAsync` which seeds the admin,
  signs in, and attaches the Bearer header to a test client — mirrors the
  staff helper.
- Tests added under `tests/AFK4.Platform.Api.Tests/Platform/`:
  - `SlugValidatorTests` — normalization, accept list (`demo-org`, `dem`,
    `a-b-c-d`, `abc123def`, `ru1-club`, `123`), reject list (empty, too
    short, leading/trailing hyphen, uppercase, underscore, double hyphen,
    whitespace), and explicit min/max length boundaries.
  - `PlatformTenantEndpointTests` (18 cases):
    create happy path persists tenant + branch + invite + succeeded audit;
    no-auth → 401; support role only → 403 + denied audit; invalid slug →
    400; duplicate organization slug → 409 with no second row; unknown
    subscription status → 400; list ordered alphabetically with branch
    counts; detail returns parsed limits + branches; unknown id → 404;
    invite rotation revokes prior pending and returns fresh invite with new
    code + lifetime; unknown branch on rotation → 404; accept creates owner
    staff with `owner` role + sign-in token + accepted invite; unknown code
    → 404; expired invite marks expired and returns 400; revoked invite →
    400; duplicate user-name → 409; short password → 400; staff bearer
    rejected at `/api/platform/tenants` (401).
- Idempotency for tenant creation is slug-based for Slice 2: same
  `OrganizationSlug` returns 409 to prevent silent duplicates. Header-based
  `Idempotency-Key` support (cached responses, retried writes) is deferred
  to a follow-up hardening pass; the current `BillingCommandIdempotency` /
  `SessionCommandIdempotency` tables remain the template if/when we want to
  generalize.

Verification (WSL Linux):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 20 projects,
  0 errors, 0 warnings.
- `dotnet test tests/AFK4.Shared.Contracts.Tests/...`: 103 passed
  (Slice 1: 101 + Slice 2: +2 = 103).
- `dotnet test tests/AFK4.Platform.Api.Tests/...`: 425 passed
  (Slice 1: 386 + Slice 2: +39 = 425). 39 new tests = SlugValidatorTests
  (21 cases across theories/facts) + PlatformTenantEndpointTests (18).
- Pre-existing Linux env limits unchanged: 22/140 `Agent.Service.Tests`
  still fail on `powershell.exe`, `Operator.App.Tests` /
  `Player.Shell.Tests` still need Windows.

Scope still deferred to later slices:

- Slice 3 — tenant lifecycle endpoints (`PATCH .../status`,
  `PATCH .../plan`, `PATCH .../limits`) and enforcement that suspended
  tenants block money / session / POS / device / update mutations while
  preserving read-only support paths.
- Slice 4 — tenant health projection endpoint and support notes CRUD.
- Slice 5 — internal Control Plane web UI consuming Slice 2–4 endpoints.
- Slice 6 — Operator App slug / setup-code connection flow that resolves
  tenant/branch from the new slugs without raw GUID copy.

Operational handoff for Slice 3:

- The accept endpoint is intentionally public. Brute force is impractical
  (128 bits of entropy per invite code) but rate-limiting at the ingress
  is still recommended before commercial production.
- `WritePlatformAuditAsync` puts the platform admin id inside details JSON
  rather than its own column. If platform-admin actor reporting becomes
  important, a `ActorPlatformAdminUserId` column on `audit_records` is the
  cleanest follow-up; mention it in Slice 3 or 4 when those endpoints also
  write platform-scoped audits.
- Suspended tenants currently still allow invite acceptance and owner
  sign-in. Slice 3 must decide whether suspending a tenant should also
  block new owner sign-ins (likely yes, with explicit support override).

### Slice 3: Lifecycle, Limits, And Enforcement — completed 2026-05-23 on `codex/saas-control-plane-slice-3`

Deliverables:

- `IPlatformTenantService` gained `UpdateStatusAsync`,
  `UpdatePlanAsync`, and `UpdateLimitsAsync`. Each method validates input,
  loads the organization, applies the change with a fresh `UpdatedAtUtc`,
  and returns a `TenantDetailDto`. `UpdateStatusAsync` enforces the allowed
  set `{active, suspended, deletion_pending}`, requires a non-empty reason
  when transitioning to `suspended` or `deletion_pending`, caps the reason
  at 500 characters, and updates `StatusChangedAtUtc` only when the status
  or reason actually changes. `UpdatePlanAsync` reuses the existing plan
  and subscription validators. `UpdateLimitsAsync` reuses the limits
  validator and rejects negative values.
- New endpoints in `Program.cs` under `/api/platform/tenants/{organizationId:guid}`:
  - `PATCH .../status` — `platform.tenants.status.update` permission.
  - `PATCH .../plan` — `platform.tenants.plan.update` permission.
  - `PATCH .../limits` — `platform.tenants.limits.update` permission.
  Each endpoint reads the previous status/plan into memory before mutating
  so the succeeded audit record captures the old → new transition. Each
  endpoint writes succeeded/denied records under
  `tenancy.tenant.status.update`, `tenancy.tenant.plan.update`, or
  `tenancy.tenant.limits.update` (action constants reserved in Slice 1).
- New `ITenantStatusGuard` / `EfTenantStatusGuard` in
  `src/AFK4.Platform.Api/Platform/Tenancy/` projects an
  `OrganizationEntity` row to a `TenantStatusSnapshot(Status, Reason)`
  read-only record with `IsActive` / `IsSuspended` / `IsDeletionPending`
  helpers.
- New `TenantSuspensionMiddleware` in `src/AFK4.Platform.Api/Identity/`
  runs after the staff and platform-admin auth middlewares. When the
  current request carries a `StaffContext` AND the HTTP method is
  `POST/PUT/PATCH/DELETE`, the middleware looks up the tenant status via
  `ITenantStatusGuard` and, if the tenant is not active, short-circuits
  the request with `HTTP 403` and a JSON envelope
  `{ "error": "TenantSuspended", "status": "...", "reason": "..." }`.
  Read requests (GET/HEAD/OPTIONS) and platform-admin or
  device-credentialed requests (no `StaffContext`) pass through
  unchanged, so suspended tenants can still browse data, sign in to see
  the blocked-state UI, and the platform admin can still reactivate them.
- `EfPlatformTenantService.AcceptOwnerInviteAsync` now rejects acceptance
  with a `BadRequest` when the tenant's status is not `active`. The
  invite itself is left untouched (pending) so it can be replayed once
  the tenant is reactivated — no need to rotate. This matches the
  decision the Slice 2 handoff flagged: suspended tenants block new
  owner sign-ups, while existing owners can still sign in.
- Tests under `tests/AFK4.Platform.Api.Tests/Platform/`:
  - `PlatformTenantLifecycleEndpointTests` (14 cases) — suspend with
    reason (+ audit verification), reactivate without reason, missing
    reason 400, unknown status 400, unknown tenant 404, no auth 401,
    deletion-pending success, plan update success, plan update with
    unknown subscription 400, plan update with support-only role 403,
    limits update success, limits update with negative value 400,
    limits update on unknown tenant 404, accept-invite on suspended
    tenant 400.
  - `TenantSuspensionEnforcementTests` (7 cases) — staff POST on
    suspended tenant returns 403 with `TenantSuspended` envelope; staff
    GET on suspended tenant returns 200; staff sign-in on suspended
    tenant still succeeds; staff POST on `deletion_pending` tenant also
    returns 403; staff POST on active tenant still succeeds; platform
    admin PATCH on suspended tenant still works; staff POST regains
    success after reactivation.

Verification (WSL Linux):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 20 projects,
  0 errors, 0 warnings.
- `dotnet test tests/AFK4.Shared.Contracts.Tests/...`: 103 passed
  (unchanged — the `UpdateTenantStatusRequest` / `Plan` / `Limits`
  round-trip tests added in Slice 1 cover the contracts touched here).
- `dotnet test tests/AFK4.Platform.Api.Tests/...`: 446 passed
  (Slice 2: 425 + Slice 3: +21 = 446). 21 new tests = 15
  `PlatformTenantLifecycleEndpointTests` cases (status/plan/limits +
  accept-invite-on-suspended) + 6 `TenantSuspensionEnforcementTests`.
- Pre-existing Linux env limits unchanged: 22/140 `Agent.Service.Tests`
  still fail on `powershell.exe`, `Operator.App.Tests` /
  `Player.Shell.Tests` still need Windows.

Scope still deferred to later slices:

- Slice 4 — tenant health projection endpoint and support notes CRUD.
- Slice 5 — internal Control Plane web UI consuming Slice 2–4 endpoints.
- Slice 6 — Operator App slug / setup-code connection flow that resolves
  tenant/branch from the new slugs without raw GUID copy.

Operational handoff for Slice 4:

- The `TenantSuspensionMiddleware` only enforces the block for requests
  that carry a `StaffContext`. Device-credentialed endpoints
  (`POST /api/devices/{deviceId}/heartbeat`,
  `POST /api/devices/{deviceId}/session-reconciliation`,
  `POST /api/devices/{deviceId}/updates/{check|status}`,
  `POST /api/devices/enroll`, etc.) are NOT yet gated by suspension —
  staff endpoints that create enrollment codes / publish rollouts /
  start sessions are already blocked, so a suspended tenant cannot
  introduce new devices, sessions, or update rings, but the existing
  fleet keeps polling and reporting telemetry. If Slice 4 (or a later
  hardening pass) needs full device-side shutoff, add a
  `ITenantStatusGuard.GetAsync(...)` call inside `IDeviceCredentialValidator`
  (or in each device-credentialed handler) and return 403 with the
  same `TenantSuspended` envelope.
- Status change audit (Succeeded) records both `PreviousStatus` /
  `PreviousReason` and `NewStatus` / `NewReason` inside the details
  payload, which gives Slice 4 / 5 a ready-made audit trail to render
  in the Control Plane UI without joining old/new history tables.
- The suspended-tenant response shape is intentionally simple
  (`error`, `status`, `reason`); when Slice 5 builds the operator
  blocked-state UI it can use the `status` field to choose copy
  (`Suspended` vs `Tenant is being deleted`).
- `UpdateStatusAsync` is idempotent: re-applying the same `Status` +
  `Reason` does not update `StatusChangedAtUtc` and does not write a
  new "the value actually changed" audit, but the succeeded audit
  record is still written from the endpoint (the request itself
  succeeded). If Slice 4 needs a strict "audit only on real change"
  guarantee, it can compare the previous/new pair in the endpoint
  details before writing.

### Slice 4: Tenant Health And Support Notes — completed 2026-05-23 on `codex/saas-control-plane-slice-4`

Deliverables:

- New shared contract `UpdateTenantSupportNoteRequest(Body)` lets the
  Control Plane UI edit existing notes; round-trip serialization is
  covered alongside the existing `TenantSupportNoteDto` /
  `CreateTenantSupportNoteRequest` tests.
- New audit action constants
  `tenancy.support_note.update` and `tenancy.support_note.view` in
  `AuditActionNames` (the existing `tenancy.support_note.create` and
  `tenancy.tenant.health.view` were already reserved in Slice 1).
- `IPlatformSupportNoteService` / `EfPlatformSupportNoteService` under
  `src/AFK4.Platform.Api/Platform/Tenancy/` implements:
  - `ListAsync(organizationId)` — returns
    `IReadOnlyList<TenantSupportNoteDto>` ordered by `CreatedAtUtc`
    descending, joined with platform admin display names; 404 when the
    tenant doesn't exist.
  - `CreateAsync(organizationId, request, platformAdminUserId)` —
    validates non-empty body and a 4000-char cap, verifies tenant
    exists, persists the note authored by the calling platform admin.
  - `UpdateAsync(organizationId, tenantSupportNoteId, request,
    platformAdminUserId)` — validates body, requires the note to
    belong to the requested tenant (cross-tenant edits return 404),
    rewrites the body. The original author is preserved; the editing
    admin is captured in the endpoint-layer audit instead of mutating
    the row.
- `IPlatformTenantHealthService` /
  `EfPlatformTenantHealthService` under the same namespace projects:
  - Tenant status (from `OrganizationEntity.Status`).
  - Branch / device / active-staff counts (each scoped by
    `OrganizationId`).
  - Latest staff sign-in via the most recent
    `StaffAccessTokenEntity.CreatedAtUtc` for the tenant.
  - Latest applied migration via
    `dbContext.Database.GetAppliedMigrationsAsync(...)` (returns the
    lexically last migration name, which matches the timestamp prefix;
    the call is wrapped in a try/catch so the EF in-memory provider
    returns `null` instead of throwing).
  - Recent error window: count + top 10 of `AuditRecords` with
    `Outcome == Denied` in the last 7 days, mapped to
    `TenantHealthErrorDto`. Each error's `Message` is a truncated
    (240-char) preview of `DetailsJson` so the support UI gets enough
    context to triage without joining other tables.
- Endpoints in `Program.cs`:
  - `GET /api/platform/tenants/{organizationId:guid}/health` —
    `platform.tenants.health.view` permission. Writes succeeded /
    denied `tenancy.tenant.health.view` audit records with counts in
    the succeeded payload.
  - `GET /api/platform/tenants/{organizationId:guid}/support-notes` —
    `platform.tenants.support_notes.view` permission. Writes
    `tenancy.support_note.view` audit records (succeeded / denied);
    the succeeded payload includes the note count.
  - `POST /api/platform/tenants/{organizationId:guid}/support-notes` —
    `platform.tenants.support_notes.manage` permission. Writes
    `tenancy.support_note.create` audit records keyed by the new
    note id.
  - `PATCH /api/platform/tenants/{organizationId:guid}/support-notes/{tenantSupportNoteId:guid}` —
    same manage permission. Writes
    `tenancy.support_note.update` audit records keyed by the note id;
    cross-tenant edit attempts return 404 (the audit reflects that).
- Tests under `tests/AFK4.Platform.Api.Tests/Platform/`:
  - `PlatformTenantHealthEndpointTests` (7 cases) — happy path (counts
    + status + non-null latest sign-in), recent denied audit shows up,
    audit outside the 7-day window is excluded, unknown tenant 404,
    no auth 401, no permission 403 + denied audit, succeeded audit
    summary fields.
  - `PlatformSupportNoteEndpointTests` (11 cases) — create persists +
    audits, create with empty body 400, create on unknown tenant 404,
    create no auth 401, create no permission 403, list returns notes
    in descending `CreatedAtUtc`, list on unknown tenant 404, update
    rewrites body + audits, update on unknown note 404, update
    rejects cross-tenant id 404, update with whitespace body 400.

Verification (WSL Linux):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 20 projects,
  0 errors, 0 warnings.
- `dotnet test tests/AFK4.Shared.Contracts.Tests/...`: 104 passed
  (Slice 3: 103 + Slice 4: +1 = 104 — only one new contract).
- `dotnet test tests/AFK4.Platform.Api.Tests/...`: 464 passed
  (Slice 3: 446 + Slice 4: +18 = 464). 18 new tests = 7 health + 11
  support note cases.
- Pre-existing Linux env limits unchanged: 22/140
  `Agent.Service.Tests` still fail on `powershell.exe`,
  `Operator.App.Tests` / `Player.Shell.Tests` still need Windows.

Scope still deferred to later slices:

- Slice 5 — internal Control Plane web UI consuming Slice 2–4
  endpoints (tenant list, detail, create, status controls, owner
  invite controls, support note editor, tenant health view).
- Slice 6 — Operator App slug / setup-code connection flow that
  resolves tenant/branch from the new slugs without raw GUID copy.

Operational handoff for Slice 5:

- The health endpoint returns a snapshot — there's no caching layer in
  front of it yet. Each call hits PostgreSQL for several scoped counts
  and a top-10 audit pull. That's fine while the tenant count is small;
  if Slice 5 starts polling health every few seconds in the UI, add a
  short (10–30 s) per-tenant cache before commercial production.
- `TenantHealthDto.Message` is a 240-char preview of the raw
  `DetailsJson`. The Control Plane UI should render it as a
  truncatable / expandable string and offer a link to the full
  `audit_records` row by `(OrganizationId, CreatedAtUtc, Action)`. If
  Slice 4 / 5 introduce a dedicated structured error log table later,
  swap the audit source for that table — the `TenantHealthErrorDto`
  shape already matches.
- Support notes are intentionally NOT exposed to staff/operator
  endpoints; only `/api/platform/...` reads them. Keep that boundary
  if Slice 5 introduces shared DTOs — the operator app should never
  serialize `TenantSupportNoteDto`.
- The accept-invite + sign-in flow remains writable for tenants in the
  `suspended` state for sign-in (so existing owners see the blocked-state
  UI) and blocks invite-accept (so no new owners materialize). Slice 5
  needs to render the right copy for these two states; reuse the
  `TenantSuspended` envelope's `status` field already returned by the
  middleware.
- Adding a dedicated `ActorPlatformAdminUserId` column to
  `audit_records` is still the cleanest follow-up if support-action
  reporting needs to join platform admins efficiently; Slice 4 still
  encodes that field inside `DetailsJson` via
  `WritePlatformAuditAsync`.

### Slice 5: Internal Control Plane Web — completed 2026-05-23 on `codex/saas-control-plane-slice-5`

Deliverables:

- New `POST /api/platform/owner-invites/{ownerInviteId:guid}/revoke`
  backend endpoint with the `platform.tenants.invites.manage`
  permission. `EfPlatformTenantService.RevokeOwnerInviteAsync`
  validates the reason (required, ≤ 500 chars), rejects non-pending
  invites (400), and stamps the existing
  `OwnerInviteEntity.Revoked{AtUtc, ByPlatformAdminUserId, Reason}`
  columns shipped in Slice 1. The endpoint writes succeeded / denied
  `tenancy.owner_invite.revoke` audit records with the reason in the
  payload. 5 new endpoint tests cover happy path + audit, unknown id
  404, already-revoked 400, missing reason 400, and no-auth 401.
- New CORS policy `platform-web` allowing
  `https://platform.afk4.local`, `http://localhost:5175`,
  `http://127.0.0.1:5175`, `http://localhost:4175`,
  `http://127.0.0.1:4175` (Vite dev + preview ports). Existing
  `operator-web` policy is unchanged. Both policies are registered;
  `UseCors` is called once per policy so the SPA can request the API
  during local development.
- New Vite + React 19 + TypeScript SPA at `src/AFK4.Platform.Web/`
  (separate from the Operator App WebView2 assets). Stack mirrors
  `src/AFK4.Operator.App.Web/` (Vite 8, React 19, vitest 4, jsdom 29,
  `@testing-library/react`, `@testing-library/jest-dom`,
  `@vitejs/plugin-react`, TypeScript 6). Scripts: `dev` (port 5175),
  `build` (`tsc -b && vite build`), `test` (vitest run),
  `preview` (port 4175).
- `src/api/types.ts` mirrors the shared C# contracts in TypeScript:
  `TenantSummary`, `TenantDetail`, `TenantBranch`, `TenantLimits`,
  `CreateTenantRequest/Response`, `OwnerInvite`, `TenantSupportNote`,
  `TenantHealth`, `TenantHealthError`, plus `TenantStatus`,
  `TenantPlanCode`, `SubscriptionStatus` constants.
- `src/auth/tokenStore.ts` persists the platform-admin session in
  `sessionStorage` under the `afk4.platform.session` key (tab-scope,
  cleared on tab close — safer than `localStorage` for a support tool).
  Helpers: `readSession`, `writeSession`, `clearSession`,
  `sessionFromSignInResponse`, `isAccessTokenExpired`.
- `src/api/platformApi.ts` exposes `PlatformApiClient` with methods
  for sign-in / sign-out, list/get tenants, create tenant, PATCH
  status / plan / limits, owner invite create + revoke, support note
  list / create / update, and tenant health. The client transparently
  refreshes the access token once on 401 (then retries the original
  call) and clears the session if the refresh attempt fails. Errors
  bubble up as `PlatformApiError(status, message, code)` with the
  backend's `{ error }` envelope.
- React components under `src/components/`:
  - `SignIn` — username + password form, surfaces 401 as
    "Wrong user name or password" copy.
  - `TenantList` — sortable table of tenants (name, slug, status,
    plan, subscription, branch count, updated), "Refresh" + "New
    tenant" actions, empty + loading + error states.
  - `NewTenant` — full create-tenant form with limits + owner
    invite fields. On success, navigates to the new tenant's detail
    view and shows the just-issued owner invite code prominently.
  - `TenantDetail` — overview + status/plan/limits controls + owner
    invites + support notes + health. Each child section owns its
    own loading / error state.
  - `StatusControl` — drop-down + required reason textarea
    (validates that suspended / deletion_pending must have a reason);
    submits PATCH status.
  - `PlanControl` — plan + subscription drop-downs; submits PATCH
    plan.
  - `LimitsControl` — numeric inputs for the four caps; submits
    PATCH limits.
  - `OwnerInvitesSection` — create form per branch, table of issued
    invites (status, code, owner, expires), revoke action that
    prompts for a reason then calls the new revoke endpoint.
  - `SupportNotesSection` — list (newest first), create new, inline
    edit existing notes via PATCH.
  - `HealthSection` — counts, latest staff sign-in, latest applied
    migration, recent denied audit preview with the truncated
    `message` column.
  - `ui.tsx` — `Loading`, `ErrorBanner` (dismissable), `EmptyState`,
    `Field` (label + hint + input wrapper), `StatusBadge`,
    `formatDate`.
- `src/App.tsx` is the top-level shell with sign-in / sign-out
  header, a simple `View = 'list' | 'new' | 'detail'` switch (no
  router dependency), and re-creates `PlatformApiClient` per
  base-URL change with a session callback that mirrors the in-memory
  client state back to React state + `sessionStorage`.
- `src/styles.css` ships a minimal responsive layout with system
  colour scheme (light + dark), badges per tenant status, mobile
  fallback at ≤ 700 px.
- Tests under `src/auth/tokenStore.test.ts` (7 cases) and
  `src/api/platformApi.test.ts` (5 cases) cover round-trip,
  malformed JSON, expired-token detection, and the client's sign-in,
  error projection, 401-refresh-retry, sign-out-on-refresh-failure,
  and Bearer-token-on-call paths. Total: 12 vitest cases.

Verification (WSL Linux):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 20
  projects, 0 errors, 0 warnings.
- `dotnet test tests/AFK4.Shared.Contracts.Tests/...`: 104 passed
  (unchanged; no new contracts in this slice — Slice 1's
  `RevokeOwnerInviteRequest` already had a round-trip test).
- `dotnet test tests/AFK4.Platform.Api.Tests/...`: 469 passed
  (Slice 4: 464 + Slice 5: +5 = 469). 5 new tests = revoke endpoint
  happy path + audit, unknown id, already-revoked, missing reason,
  no auth.
- `npm run build` in `src/AFK4.Platform.Web/`: tsc + vite build,
  ~221 kB JS bundle / ~66 kB gzip.
- `npm test` in `src/AFK4.Platform.Web/`: 12 passed, 0 failed
  (7 `tokenStore.test.ts` + 5 `platformApi.test.ts`).
- Pre-existing Linux env limits unchanged: 22/140
  `Agent.Service.Tests` still fail on `powershell.exe`,
  `Operator.App.Tests` / `Player.Shell.Tests` still need Windows.
- The Vite project is not added to `AFK4.sln` (the solution is a
  .NET-only sln; the SPA is built via `npm run build` and deployed
  as static assets, like the existing Operator App Web project).

Scope still deferred to later slices:

- Slice 6 — Operator App slug / setup-code connection flow that
  resolves tenant/branch from the new slugs without raw GUID copy.

Operational handoff for Slice 6:

- The SPA assumes the platform API is reachable at
  `window.location.origin` by default. Override with the
  `VITE_PLATFORM_API_BASE_URL` env var at build time (or via
  `vite dev` `--mode`) when serving the SPA from a different origin
  than the API.
- The SPA stores the session in `sessionStorage`, so closing the
  tab signs the admin out. If we move to `localStorage` later for
  comfort, audit reviewers should be told — right now the audit log
  shows one platform admin id per browser tab.
- The owner invite section keeps revoked / accepted invites in its
  local view-model only between page mounts. There is no
  `GET .../tenants/{id}/owner-invites` endpoint yet — Slice 6 (or a
  Slice 5.1 hardening pass) should add one so the list survives a
  page refresh. The SPA is structured to consume that endpoint
  without other changes (`OwnerInvitesSection` already calls
  `client.getTenant` on mount; switching it to a dedicated invites
  fetch is a one-liner once the endpoint exists).
- The `revoke owner invite` endpoint requires a non-empty reason;
  the SPA collects it via `window.prompt`. Slice 5.1 / 6 should
  replace the prompt with an inline form so the input flow matches
  the rest of the UI.
- `OwnerInviteDto.Code` is shown verbatim in the table. That is the
  bearer credential. The Control Plane is internal-only (platform
  admin auth in front of the page), so this is acceptable for MVP;
  for commercial rollout, mask the code after first display and
  surface it again only via an explicit "reveal" action.
- Suspended / deletion-pending tenants surface in the tenant list
  with a coloured badge driven by `StatusBadge`. The status update
  control + plan control + limits control will all return 403 with
  `TenantSuspended` when blocked by the Slice 3 middleware — but
  these endpoints are platform-admin-scoped (no `StaffContext`), so
  they always go through and the SPA never has to render the
  `TenantSuspended` envelope itself; the staff-side blocked UI is
  Slice 6 / Operator App work.
- The SPA is deployed by serving the contents of
  `src/AFK4.Platform.Web/dist/` from any static host (S3, Coolify,
  nginx, etc.). For Coolify staging, a follow-up commit can add a
  Caddyfile / nginx config snippet under `deploy/coolify/` to wire
  it to `platform.afk4.local`.

### Slice 6: Operator App Connection Without GUIDs — completed 2026-05-23 on `codex/saas-control-plane-slice-6`

Deliverables:

- New shared contracts in `src/AFK4.Shared.Contracts/Platform/Operator/`:
  - `ResolveOperatorConnectionRequest(OrganizationSlug?, BranchSlug?, SetupCode?)` —
    discriminated payload (either slug pair OR setup code, never both).
  - `ResolveOperatorConnectionResponse(OrganizationId, OrganizationSlug,
    OrganizationName, OrganizationStatus, OrganizationStatusReason,
    BranchId, BranchSlug, BranchName, BranchCity, Source)` —
    Source is `"slug"` or `"setup_code"` via the new
    `OperatorConnectionResolutionSources` constants class. Round-trip
    serialization is covered by 4 new tests under
    `tests/AFK4.Shared.Contracts.Tests/Platform/OperatorConnectionContractSerializationTests.cs`.
- New audit action constant
  `tenancy.operator_connection.resolve` in `AuditActionNames` (the
  endpoint writes succeeded + denied records under this action so
  support can see failed resolutions during onboarding).
- New `IOperatorConnectionResolver` /
  `EfOperatorConnectionResolver` under
  `src/AFK4.Platform.Api/Platform/Tenancy/`:
  - Slug-pair path: validates both slugs via the existing
    `SlugValidator`, looks up the tenant by normalized
    `OrganizationEntity.Slug`, then the branch by
    `(OrganizationId, Slug)`; returns 404 with a clear error string
    when either misses; returns the metadata for any tenant status
    (active, suspended, deletion_pending) so the operator app can
    show blocked-state copy.
  - Setup-code path: looks up `OwnerInviteEntity` by
    `NormalizedCode`, returns 400 when the invite is not pending
    (revoked / accepted / expired), 404 when the invite or its
    tenant / branch no longer exists.
  - Mutual-exclusion: rejects requests that provide both slug pair
    and setup code with `400 Provide either ... not both.`, and
    rejects requests with neither field with `400 Provide either ...`.
- New endpoint `POST /api/operator-connections/resolve` in
  `Program.cs`. Public (the slug pair or setup code is the
  credential). Writes succeeded / denied
  `tenancy.operator_connection.resolve` audit records; the denied
  payload encodes `{ HasSlugPair, HasSetupCode, Error }` so support
  can spot brute-force / typo patterns without leaking raw slugs.
- React resolver client in
  `src/AFK4.Operator.App.Web/src/connectionResolver.ts`:
  - `ConnectionResolver` with `resolveBySlugPair(orgSlug, branchSlug)`
    and `resolveBySetupCode(code)` methods, plus
    `ConnectionResolutionError(status, message)` that mirrors the
    backend's `{ error }` envelope.
  - `OperatorTenantStatus` constants
    (`active` / `suspended` / `deletion_pending`).
  - `readStoredConnection`, `writeStoredConnection`,
    `clearStoredConnection` persist the resolved tenant + branch in
    `localStorage` under the `afk4.operator.connection` key so the
    Operator App skips the connection screen on subsequent launches.
    `writeStoredConnection` stamps `storedAtUtc` so the WPF host can
    decide when to invalidate.
- `ConnectionResolutionScreen` React component in
  `src/AFK4.Operator.App.Web/src/ConnectionResolutionScreen.tsx`:
  - Mode toggle (slug pair vs setup code).
  - Renders error copy specialised for HTTP 404 (`No tenant matched
    the slugs / setup code`), HTTP 400 (passes through backend
    message), and generic fallback.
  - Exposes `isOperatorTenantBlocked(resolution)` helper so the
    caller can short-circuit straight to a blocked-state UI when a
    suspended / deletion-pending tenant is resolved.
- Tests:
  - Backend `OperatorConnectionResolutionEndpointTests` (11 cases):
    slug pair happy path + succeeded audit; setup-code happy path;
    suspended tenant resolves with `status="suspended"` + reason;
    revoked invite 400; expired invite 400; unknown slug 404 + denied
    audit; known org but unknown branch 404; both fields 400; no
    fields 400; invalid slug format 400; case-insensitive setup
    code accepted.
  - Frontend `connectionResolver.test.ts` (10 cases): slug-pair
    resolve, setup-code resolve, error projection from JSON body,
    fallback when body isn't JSON, write/read/clear stored
    connection round-trip, malformed payload returns null, empty
    organizationId returns null.

Verification (WSL Linux):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 20
  projects, 0 errors, 0 warnings.
- `dotnet test tests/AFK4.Shared.Contracts.Tests/...`: 108 passed
  (Slice 5: 104 + Slice 6: +4 new = 108).
- `dotnet test tests/AFK4.Platform.Api.Tests/...`: 480 passed
  (Slice 5: 469 + Slice 6: +11 = 480).
- `npm test` in `src/AFK4.Operator.App.Web/`: 112 passed
  (Slice 5: 102 + Slice 6: +10 = 112).
- `npm run build` in `src/AFK4.Operator.App.Web/`: typecheck +
  vite build green (~489 kB JS / ~131 kB gzip).
- `npm test` in `src/AFK4.Platform.Web/`: 12 passed (unchanged).
- Pre-existing Linux env limits unchanged: 22/140
  `Agent.Service.Tests` still fail on `powershell.exe`,
  `Operator.App.Tests` / `Player.Shell.Tests` still need Windows.

Scope explicitly deferred to Windows-machine follow-up:

- Wiring `ConnectionResolutionScreen` into `Operator.App.Web/App.tsx`
  as the pre-sign-in step when no `organizationId` / `branchId` is
  present in `operatorConfig`. The component + resolver client are
  ready to be slotted in; this is a small App.tsx edit that needs
  Windows + WebView2 to test end-to-end.
- WPF-side persistence via DPAPI in the existing
  `OperatorTokenStore` pattern. The React side already persists via
  `localStorage`, which is good enough for browser dev but should be
  backed by the WPF protected-storage bridge for production. Mirror
  the `OperatorTokenStore` pattern under
  `src/AFK4.Operator.App/Connection/` once on a Windows machine.
- Environment-variable fallback for developer / staging override is
  still wired through `OperatorAppOptions` / `operatorConfig`; no
  changes needed there. Slice 6 does NOT remove that fallback.

### Slice 6 Windows-handoff follow-up — completed 2026-05-23 on `main`

Deliverables:

- `ConnectionResolutionScreen` wired into `App()` in
  `src/AFK4.Operator.App.Web/src/App.tsx`:
  - `App()` now derives an effective `OperatorConfig` by merging
    `getOperatorConfig()` with the result of `readStoredConnection()`
    (host-side `__AFK4_OPERATOR_CONFIG__` overrides win over the
    stored connection when both are present).
  - New render gate fires when `authStatus === 'signed-out'` and the
    effective config still has no `organizationId` / `branchId` — in
    that case the connection screen renders instead of the sign-in
    screen. During `authStatus === 'checking'` the existing
    `SignInScreen` "checking" state still renders, so successful
    native session restore via the WebView2 bridge skips the gate
    entirely.
  - On `onResolved`: active resolutions are persisted via
    `writeStoredConnection` and unblock the sign-in screen; blocked
    resolutions (suspended / deletion-pending) are NOT persisted,
    surface a new in-file `BlockedTenantScreen` with status copy +
    reason, and offer a "Сменить подключение" button that calls
    `clearStoredConnection()` and returns to the connection screen.
- 4 new vitest cases in `src/AFK4.Operator.App.Web/src/App.test.tsx`:
  shows the connection screen when no config / storage; skips the
  connection screen when storage seeded; persists active resolution
  and proceeds to sign-in (asserts `localStorage` write + key fields);
  surfaces blocked-state copy without persisting when the resolved
  tenant is `suspended`, and returns to the connection screen on
  "Сменить подключение".
- 3 existing auth tests updated so they bypass the new connection
  gate without changing their original concern: the bridge-only test
  now uses `__AFK4_OPERATOR_CONFIG__` for organisation / branch (so
  `localStorage` stays empty); the refresh-rejected and WebView2
  bridge-diagnostics tests seed a stored connection via the new
  `seedStoredOperatorConnection()` helper.
- WPF DPAPI store mirroring `ProtectedDataOperatorTokenStore` under
  `src/AFK4.Operator.App/Connection/`:
  - `OperatorConnectionSnapshot` (record) — fields match the React
    `ResolvedOperatorConnection` shape: organisation + branch ids /
    slugs / names + branch city + `StoredAtUtc`.
  - `IOperatorConnectionStore` + `ProtectedDataOperatorConnectionStore`
    persist a JSON-serialised snapshot under
    `%LocalAppData%/AFK4/Operator/connection.bin`, encrypted with
    `ProtectedData.Protect` (`DataProtectionScope.CurrentUser`),
    identical pattern to the existing token store.
- 3 new xUnit cases in
  `tests/AFK4.Operator.App.Tests/OperatorConnectionStoreTests.cs`:
  save → load round-trip (asserts every field plus that the
  organisation name and branch slug never appear in the on-disk
  ciphertext, then ClearAsync zeroes the file); `LoadAsync` returns
  null when the file is missing; `ClearAsync` is idempotent.

Verification (Windows 11):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 0 errors,
  0 warnings.
- `dotnet test`: Shared.Contracts 108/108, Platform.Api 480/480,
  Operator.App 199/199 (+3 new), Player.Shell 11/11,
  Agent.Service 140/140 (all pass on Windows — the 22
  `powershell.exe` failures only happen on WSL),
  GamingPc.Setup 10/10, Update.Publisher 8/8.
- `npm test` in `src/AFK4.Operator.App.Web/`: 116/116 (+4 new).
- `npm test` in `src/AFK4.Platform.Web/`: 12/12 (unchanged).

Scope still deferred (not part of this follow-up):

- E2E run through the connection screen inside the packaged WebView2
  shell (this verification was scoped to vitest + xUnit). Needs a
  staging Platform API with the Slice 6 resolver endpoint live before
  it makes sense to drive.

### Slice 6 host-bridge wiring — completed 2026-05-23 on `main`

Closes the gap flagged in the previous follow-up: React connection
storage now actually flows through the WPF DPAPI store when running
in packaged WebView2.

Deliverables:

- New host-bridge family on `OperatorWebHostBridge.cs`:
  - `connection:loadConnection` returns the persisted snapshot (or
    explicit JSON `null`).
  - `connection:saveConnection` accepts the
    `ResolvedOperatorConnection` shape, validates that the org / branch
    GUIDs parse and slugs / names are present, trims the strings, then
    persists via `IOperatorConnectionStore.SaveAsync`.
  - `connection:clearConnection` deletes the protected snapshot.
  - The bridge ctor takes an `IOperatorConnectionStore` (third
    dependency). `WebViewOperatorWindow.CreateDefaultHostBridge`
    instantiates `ProtectedDataOperatorConnectionStore` next to the
    existing token store and passes it in.
  - Bridge errors for `connection:*` requests surface a
    `connection_failed` error code (vs the existing `auth_failed`) so
    the React side can distinguish host-bridge problems from auth.
- React storage abstraction in `connectionResolver.ts`:
  - `OperatorConnectionStorage` interface with `loadSync` / `load` /
    `save` / `clear`.
  - `LocalStorageOperatorConnectionStorage` wraps the existing sync
    helpers (browser-dev path); `BridgeOperatorConnectionStorage`
    proxies through a generic `BridgeRequestSender` (= `postHostRequest`
    in production) and normalises the bridge payload back into
    `ResolvedOperatorConnection`.
  - `App.tsx` auto-selects the bridge storage when
    `window.chrome?.webview` is present and falls back to localStorage
    in browser-dev. A new `isConnectionLoading` flag suppresses the
    `ConnectionResolutionScreen` during the bridge round-trip so
    packaged WebView2 boots don't flash the connection screen when a
    snapshot is persisted on disk. `handleConnectionResolved` /
    "Сменить подключение" / blocked-state clear all go through the
    async storage.
- Tests:
  - 5 new xUnit cases in `OperatorWebHostBridgeTests.cs` covering the
    new handlers (load with stored snapshot, load empty, save success
    with trimming, save with invalid GUID returning `connection_failed`,
    clear) + a `RecordingOperatorConnectionStore` fake. Existing bridge
    tests updated to pass the new dependency.
  - 6 new vitest cases in `connectionResolver.test.ts` covering the
    `LocalStorageOperatorConnectionStorage` round-trip and the
    `BridgeOperatorConnectionStorage` load / load-empty / save / clear
    flows against a recording sender.
  - Existing App.test.tsx tests updated to the bridge contract:
    `installSessionBridge` now handles `connection:*` messages with an
    optional `loadConnection` override, captures `connectionSaves`, and
    echoes / clears state; the "skips the connection resolution screen
    when a stored connection is present" test moved to
    `installSessionBridge(null, null, { loadConnection })`; the
    "persists ... and proceeds to sign-in" test asserts against
    `bridge.connectionSaves` instead of localStorage; the
    blocked-state test additionally asserts that
    `connection:clearConnection` was sent both on suspended resolution
    and on "Сменить подключение".

Verification (Windows 11):

- `dotnet build AFK4.sln -p:EnableWindowsTargeting=true`: 0 errors,
  0 warnings.
- `dotnet test`: Shared.Contracts 108/108, Platform.Api 480/480,
  Operator.App 204/204 (+5 new bridge tests), Player.Shell 11/11,
  Agent.Service 140/140, GamingPc.Setup 10/10,
  Update.Publisher 8/8.
- `npm test` in `src/AFK4.Operator.App.Web/`: 122/122 (+6 new
  storage-abstraction tests).
- `npm test` in `src/AFK4.Platform.Web/`: 12/12.

Scope still deferred (not part of this follow-up):

- E2E run through the connection screen inside the packaged WebView2
  shell with a live Platform API. The WPF-side bridge contract is
  unit-tested end-to-end and the React-side adapter has its own
  recording-sender coverage; the integration verification still needs
  staging.
- Optional UX polish: a "checking persisted connection…" splash during
  the bridge load round-trip (current behaviour leaves the existing
  `SignInScreen` "Проверяем защищённый вход" copy in place during the
  combined auth + connection boot, which is fine but generic).

Operational handoff for staging smoke (from the original plan):

- The full staging smoke is now executable:
  1. Apply Slice 1 migration to staging PostgreSQL via the backup +
     restore runbook.
  2. Sign in as platform admin (Slice 1 bootstrap) — via the Slice 5
     Control Plane SPA.
  3. Create a tenant + first branch (Slice 2 / Slice 5 form).
  4. Resolve the new branch from the Operator App using the
     organisation slug + branch slug (Slice 6 endpoint).
  5. Accept the owner invite via the Slice 2 endpoint (Slice 5
     surfaces the code; the operator pastes it into the Operator App
     sign-in flow).
  6. Suspend tenant via the Slice 3 PATCH endpoint; verify the
     Operator App's slug resolution still works but returns
     `status="suspended"`, and that staff sign-in still works while
     mutations are blocked by the Slice 3 middleware.
  7. Inspect tenant health via the Slice 4 / Slice 5 health view.
  8. Reactivate tenant; verify writes resume.
- Open follow-up before commercial rollout: replace the localStorage
  setup-code persistence with the WPF DPAPI-backed protected store
  (see "Scope explicitly deferred" above) and add ingress-level
  rate-limiting to `/api/operator-connections/resolve` and
  `/api/platform/owner-invites/accept` (128 bits of invite entropy
  makes brute force impractical, but rate-limiting is still a
  sensible defence-in-depth measure).
