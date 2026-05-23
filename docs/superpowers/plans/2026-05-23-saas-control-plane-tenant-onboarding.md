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
