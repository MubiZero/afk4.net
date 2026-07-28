# Platform Control And Organization Admin Big Bang Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the ambiguous Platform Web / Operator App model with strictly separated Platform Control and Organization Admin products, auth domains, roles, permissions, API namespaces, packages, and operational release gates in one coordinated breaking release.

**Architecture:** Keep the .NET 10 modular-monolith backend, but make platform and organization authentication explicit endpoint metadata and reject tokens from the opposite domain. Rename both client products and all current artifacts, migrate stored organization roles atomically, invalidate every platform and organization staff token, and move staff-authorized organization routes under `/api/organizations/{organizationId}/*`; device, player, webhook, and public activation APIs remain in their own security domains. The final release is one compatibility unit with no aliases or partial rollout.

**Tech Stack:** .NET 10 / ASP.NET Core Minimal APIs, EF Core + PostgreSQL, React 19, TypeScript 6, Bun, Vite 8, WPF + WebView2, WiX 4, Docker/nginx, GitHub Actions, PowerShell.

## Global Constraints

- Canonical products are `Platform Control` and `Organization Admin`; ordinary product copy does not prefix either name with `AFK4`.
- Canonical actors are `PlatformAdmin`, `PlatformSupport`, `OrganizationOwner`, `BranchManager`, `ShiftSupervisor`, `Operator`, `Technician`, and `Accountant`.
- `Operator` is a staff role only and is never a current product name.
- Platform permissions use the `platform.` namespace; organization permissions use the `organization.` namespace.
- Platform Control accepts only platform-domain tokens; Organization Admin accepts only organization-domain tokens.
- Platform operations use `/api/platform/*`; staff-authorized organization operations use `/api/organizations/{organizationId}/*`.
- Device, Player Shell, public Account Activation, payment webhook, and health endpoints keep security-specific routes and must not be forced under the Organization Admin namespace.
- There are no old endpoint aliases, dual permission strings, dual project names, or compatibility adapters after cutover.
- All platform, organization staff, and player access/refresh tokens are invalidated by the migration; every human user signs in again.
- Device credentials are not interactive user sessions and remain valid; prove that exclusion explicitly in migration tests.
- Backend, Platform Control, Organization Admin, and the Organization Admin MSI release as one compatibility unit in a maintenance window.
- Keep the existing .NET 10 backend, WPF/WebView2 host, React frontend, multi-tenant model, and floor-map-first Organization Admin UX.
- Preserve unrelated `.claude/memory/*` edits and do not push, deploy, mutate live data, or decommission external resources without a separate explicit command.

---

### Task 1: Add executable vocabulary and route-boundary guards

**Files:**
- Create: `tests/AFK4.Platform.Api.Tests/Architecture/ProductBoundaryVocabularyTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs`
- Create: `scripts/Test-CurrentProductVocabulary.ps1`
- Modify: `.github/workflows/pr-verification.yml`

**Interfaces:**
- Consumes: compiled endpoint metadata from `WebApplicationFactory<Program>` and the repository root.
- Produces: `Test-CurrentProductVocabulary.ps1 -RepositoryRoot (Get-Location).Path` and failing architecture tests that prevent reintroducing current-use `Operator App`, `Control Plane`, unqualified role `Owner`, `/api/owner`, or staff-authorized routes outside the canonical organization prefix.

- [ ] **Step 1: Write a failing repository vocabulary test script**

```powershell
param([Parameter(Mandatory = $true)][string] $RepositoryRoot)

$activeRoots = @('README.md', 'src', 'tests', 'deploy', 'installers', 'scripts', '.github', 'docs/operations', 'docs/product', 'docs/roadmap')
$forbidden = @(
    @{ Pattern = '\bOperator App\b'; Label = 'Operator App' },
    @{ Pattern = '\bControl Plane\b'; Label = 'Control Plane' },
    @{ Pattern = '/api/owner(?:/|\b)'; Label = '/api/owner' }
)

$violations = foreach ($root in $activeRoots) {
    $path = Join-Path $RepositoryRoot $root
    if (-not (Test-Path -LiteralPath $path)) { continue }
    foreach ($rule in $forbidden) {
        & rg --line-number --glob '!docs/archive/**' --glob '!docs/superpowers/specs/2026-07-28-platform-organization-product-boundary-design.md' $rule.Pattern $path |
            ForEach-Object { "[$($rule.Label)] $_" }
    }
}

if ($violations) { $violations | Write-Error; exit 1 }
```

- [ ] **Step 2: Run the guard and verify RED on current product names**

Run from Windows PowerShell:

```powershell
& .\scripts\Test-CurrentProductVocabulary.ps1 -RepositoryRoot (Get-Location).Path
```

Expected: non-zero exit with current `Operator App`, `Control Plane`, and `/api/owner` locations.

- [ ] **Step 3: Write endpoint-domain tests against a new metadata contract**

```csharp
[Fact]
public void EveryStaffAuthorizedEndpointUsesOrganizationPrefixAndMetadata()
{
    var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
    var organization = endpoints.Where(e => e.Metadata.GetMetadata<AuthenticationDomainMetadata>()?.Domain == AuthenticationDomain.Organization);

    Assert.All(organization, endpoint =>
        Assert.StartsWith("/api/organizations/{organizationId}", ((RouteEndpoint)endpoint).RoutePattern.RawText));
}

[Fact]
public void PlatformAndOrganizationEndpointsNeverShareAuthenticationDomains()
{
    var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
    Assert.DoesNotContain(endpoints, endpoint =>
        endpoint.Metadata.OfType<AuthenticationDomainMetadata>().Select(m => m.Domain).Distinct().Count() > 1);
}
```

- [ ] **Step 4: Run the architecture tests and verify RED because metadata does not exist**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~Architecture
```

Expected: compile failure for `AuthenticationDomainMetadata`.

- [ ] **Step 5: Add both guards to PR verification but allow them to stay red until their producing tasks land on the same branch**

Add the final commands to `.github/workflows/pr-verification.yml`; do not merge or push an intermediate commit where required CI is intentionally red.

- [ ] **Step 6: Commit the red contract tests**

```powershell
git add tests/AFK4.Platform.Api.Tests/Architecture scripts/Test-CurrentProductVocabulary.ps1 .github/workflows/pr-verification.yml
git commit -m "test(architecture): закрепить новые product boundaries"
```

### Task 2: Introduce explicit authentication domains

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/AuthenticationDomain.cs`
- Create: `src/AFK4.Platform.Api/Identity/AuthenticationDomainMetadata.cs`
- Create: `src/AFK4.Platform.Api/Identity/EndpointAuthenticationDomainExtensions.cs`
- Modify: `src/AFK4.Platform.Api/Identity/StaffContext.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminContext.cs`
- Modify: `src/AFK4.Platform.Api/Identity/StaffAuthenticationMiddleware.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminAuthenticationMiddleware.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/StaffContextTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminAuthenticationEndpointTests.cs`

**Interfaces:**
- Produces: `enum AuthenticationDomain { Platform, Organization }`, `AuthenticationDomainMetadata(AuthenticationDomain Domain)`, `RequirePlatformDomain()` and `RequireOrganizationDomain()` endpoint extensions.
- Produces: `StaffContext.Domain == Organization` and `PlatformAdminContext.Domain == Platform`.
- Consumes: existing opaque token tables and validation services; token domain remains authoritative from the table/service that validated it.

- [ ] **Step 1: Extend failing auth-context tests**

```csharp
Assert.Equal(AuthenticationDomain.Organization, staffContext.Domain);
Assert.Equal(AuthenticationDomain.Platform, platformContext.Domain);
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~StaffContextTests|FullyQualifiedName~PlatformAdminAuthenticationEndpointTests"
```

- [ ] **Step 3: Add the domain types and endpoint extensions**

```csharp
public enum AuthenticationDomain { Platform, Organization }

public sealed record AuthenticationDomainMetadata(AuthenticationDomain Domain);

public static class EndpointAuthenticationDomainExtensions
{
    public static TBuilder RequirePlatformDomain<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new AuthenticationDomainMetadata(AuthenticationDomain.Platform));

    public static TBuilder RequireOrganizationDomain<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        => builder.WithMetadata(new AuthenticationDomainMetadata(AuthenticationDomain.Organization));
}
```

- [ ] **Step 4: Populate domain on both contexts and reject opposite-domain endpoint metadata before the handler runs**

The organization middleware must never turn a platform token into a staff context, and the platform middleware must never turn a staff token into a platform context. Add a single post-routing enforcement middleware after authentication that returns `401` when endpoint metadata and the validated context domain disagree.

- [ ] **Step 5: Run focused and architecture tests GREEN**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AuthenticationDomain|FullyQualifiedName~StaffContextTests|FullyQualifiedName~PlatformAdminAuthenticationEndpointTests"
```

- [ ] **Step 6: Commit**

```powershell
git add src/AFK4.Platform.Api/Identity src/AFK4.Platform.Api/Platform/Identity src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests
git commit -m "feat(auth): разделить platform и organization домены"
```

### Task 3: Rename canonical roles and permissions

**Files:**
- Create: `src/AFK4.Shared.Contracts/Identity/OrganizationPermissionNames.cs`
- Create: `src/AFK4.Platform.Api/Identity/OrganizationRoleNames.cs`
- Rename: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` -> `OrganizationPermissionCatalog.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminRoleNames.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: all current references to `StaffPermissionNames`, `StaffRoleNames`, and `PermissionCatalog` under `src/` and `tests/`
- Delete: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`
- Delete: `src/AFK4.Platform.Api/Identity/StaffRoleNames.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/Identity/OrganizationPermissionNamesTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Identity/OrganizationPermissionCatalogTests.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/Platform/PlatformAdminAuthContractSerializationTests.cs`

**Interfaces:**
- Produces: `OrganizationPermissionNames` with every existing organization permission value prefixed once with `organization.`; for example `organization.devices.install`, `organization.billing.view`, and `organization.pos.sales.create`.
- Produces role values: `organization_owner`, `branch_manager`, `shift_supervisor`, `operator`, `technician`, `accountant`.
- Produces platform role values: `platform_admin`, `platform_support`.
- Produces platform permission names under `platform.organizations.*`, replacing `platform.tenants.*`, and keeps other already unambiguous `platform.billing.*` / `platform.audit.*` families.

- [ ] **Step 1: Write exhaustive reflection tests for names and uniqueness**

```csharp
[Fact]
public void EveryOrganizationPermissionUsesOrganizationNamespace()
{
    var values = typeof(OrganizationPermissionNames).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => Assert.IsType<string>(field.GetRawConstantValue())).ToArray();
    Assert.NotEmpty(values);
    Assert.All(values, value => Assert.StartsWith("organization.", value));
    Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
}
```

- [ ] **Step 2: Run contracts tests RED because canonical classes do not exist**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter "FullyQualifiedName~OrganizationPermission|FullyQualifiedName~PlatformAdminAuth"
```

- [ ] **Step 3: Create canonical constants and catalogs, then mechanically update all call sites**

Do not retain forwarding classes or old string aliases. Preserve permission meaning and role-to-permission coverage while changing the canonical names.

- [ ] **Step 4: Add catalog coverage for every canonical role**

```csharp
Assert.True(OrganizationPermissionCatalog.IsKnownRole(OrganizationRoleNames.OrganizationOwner));
Assert.True(OrganizationPermissionCatalog.IsKnownRole(OrganizationRoleNames.Operator));
Assert.False(OrganizationPermissionCatalog.IsKnownRole("owner"));
Assert.False(PlatformAdminPermissionCatalog.IsKnownRole("platform_owner"));
```

- [ ] **Step 5: Run shared contracts and identity tests GREEN**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PermissionCatalog|FullyQualifiedName~Authorization|FullyQualifiedName~Authentication"
```

- [ ] **Step 6: Commit**

```powershell
git add src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Shared.Contracts.Tests tests/AFK4.Platform.Api.Tests
git commit -m "refactor(identity): ввести organization роли и permissions"
```

### Task 4: Add bounded and audited Platform Support access

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PlatformSupportAccessGrantEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Platform/Support/PlatformSupportAccessGrantService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Support/PlatformSupportContext.cs`
- Create: `src/AFK4.Platform.Api/Identity/PlatformSupportAccessMetadata.cs`
- Modify: `src/AFK4.Platform.Api/Identity/EndpointAuthenticationDomainExtensions.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformSupportAccessEndpoints.cs`
- Modify: only read-only organization diagnostics/audit endpoints explicitly approved for support access
- Create: `src/AFK4.Shared.Contracts/Platform/Support/PlatformSupportAccessContracts.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportAccessEndpointTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs`

**Interfaces:**
- Produces `POST /api/platform/support-access-grants` and `DELETE /api/platform/support-access-grants/{grantId}` for a platform principal holding `platform.support.access`.
- Produces `PlatformSupportAccessGrantEntity(GrantId, PlatformAdminUserId, OrganizationId, Reason, IssuedAtUtc, ExpiresAtUtc, RevokedAtUtc)` with a maximum lifetime of 30 minutes.
- Produces endpoint metadata `AllowPlatformSupportAccess(string permission)` only on an explicit read-only allowlist.
- Consumes a platform token plus `X-AFK4-Support-Access-Grant: {grantId}`; it never mints an organization staff token or impersonates `OrganizationOwner`.

- [ ] **Step 1: Write RED tests for reason, lifetime, organization binding, endpoint allowlist, audit, expiry, and revocation**

```csharp
[Fact]
public async Task SupportGrant_CannotCrossOrganizationOrOutliveThirtyMinutes()
{
    var grant = await CreateGrantAsync(TestIds.OrganizationId, "Investigate failed device enrollment", TimeSpan.FromMinutes(30));
    Assert.Equal(HttpStatusCode.OK, await GetAllowedDiagnosticsAsync(TestIds.OrganizationId, grant.GrantId));
    Assert.Equal(HttpStatusCode.Forbidden, await GetAllowedDiagnosticsAsync(TestIds.OtherOrganizationId, grant.GrantId));
    timeProvider.Advance(TimeSpan.FromMinutes(31));
    Assert.Equal(HttpStatusCode.Forbidden, await GetAllowedDiagnosticsAsync(TestIds.OrganizationId, grant.GrantId));
}
```

Also assert blank/short reasons are rejected, mutating organization endpoints remain forbidden, grant creation/use/revocation writes audit records, and the response/context contains no organization staff token or Organization Owner identity.

- [ ] **Step 2: Run focused tests RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformSupportAccess
```

- [ ] **Step 3: Implement grants with server-side expiry/revocation and an explicit endpoint allowlist**

Use `TimeProvider` for deterministic expiry. Store only the grant id in the request header; validate the current platform user, organization, expiry, revocation, requested permission, and endpoint metadata on every use. A grant cannot elevate beyond the current Platform Support role.

- [ ] **Step 4: Add platform permissions and audit actions**

Add `platform.support.access` and the narrow read permissions required by the allowlisted diagnostics. Record actor, organization, reason, grant id, endpoint/action, issued/used/revoked timestamps, and result without logging platform tokens.

- [ ] **Step 5: Mark only organization health, diagnostics, and organization-audit reads as support-accessible**

Do not mark money mutations, sessions, POS, staff/roles, credentials, subscription changes, or organization settings. Platform Support continues using platform endpoints for platform-owned customer metadata.

- [ ] **Step 6: Run support and endpoint-domain tests GREEN**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlatformSupportAccess|FullyQualifiedName~AuthenticationDomainEndpoint"
```

- [ ] **Step 7: Commit**

```powershell
git add src/AFK4.Platform.Api src/AFK4.Shared.Contracts tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform-support): добавить аудитируемый доступ"
```

### Task 5: Add the atomic role migration and global token invalidation

**Files:**
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260728120000_SeparatePlatformAndOrganizationIdentity.cs`
- Create: generated matching migration designer
- Modify: `src/AFK4.Platform.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Migrations/SeparatePlatformAndOrganizationIdentityMigrationTests.cs`
- Create: `scripts/rehearse-platform-organization-cutover.ps1`

**Interfaces:**
- Consumes exact role mappings from Task 3.
- Produces a migration that creates the support-grant table, aborts on unknown stored roles, maps all organization and platform roles, updates role-bearing CSV/configuration fields, and revokes every active token in all six human token tables.
- Produces a rehearsal script accepting `-ConnectionString` and never printing it.

- [ ] **Step 1: Write a PostgreSQL migration test with all old roles, one unknown role, and live tokens**

Seed `owner`, `branch_manager`, `shift_supervisor`, `cashier_operator`, `technician`, `accountant_auditor`, `platform_owner`, and `platform_support`. Assert the unknown role causes the migration preflight to fail and no partial mapping occurs. Verify by schema inspection that permissions are catalog-derived rather than persisted assignments; if a persisted permission-bearing field is found, add it to the same guarded mapping instead of ignoring it.

- [ ] **Step 2: Run the migration test RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~SeparatePlatformAndOrganizationIdentityMigrationTests
```

- [ ] **Step 3: Generate the migration and replace generated data operations with guarded SQL**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add SeparatePlatformAndOrganizationIdentity --project .\src\AFK4.Platform.Api\AFK4.Platform.Api.csproj --startup-project .\src\AFK4.Platform.Api\AFK4.Platform.Api.csproj
```

The `Up` migration must check unknown distinct values before updates and then map:

```text
owner              -> organization_owner
branch_manager     -> branch_manager
shift_supervisor   -> shift_supervisor
cashier_operator   -> operator
technician         -> technician
accountant_auditor -> accountant
platform_owner     -> platform_admin
platform_support   -> platform_support
```

Set `RevokedAtUtc` on every active row in `staff_access_tokens`, `staff_refresh_tokens`, `platform_admin_access_tokens`, `platform_admin_refresh_tokens`, `player_access_tokens`, and `player_refresh_tokens` in the same transaction. Update every stored role-bearing field found by the preflight, including staff invitations and money-cap policies; do not silently skip comma-separated role lists. Assert that device credentials are unchanged.

- [ ] **Step 4: Make `Down` explicitly unsupported after cutover**

Throw from `Down` with a message requiring restoration of the pre-cutover database snapshot. This prevents a misleading partial rollback that cannot restore invalidated sessions or old clients.

- [ ] **Step 5: Add a rehearsal script that clones schema/data into an isolated database, applies migrations, prints only counts, and verifies zero old/unknown roles and zero interactive tokens**

- [ ] **Step 6: Run migration tests GREEN and generate an idempotent SQL artifact for review**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~SeparatePlatformAndOrganizationIdentityMigrationTests
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations script --idempotent --project .\src\AFK4.Platform.Api\AFK4.Platform.Api.csproj --startup-project .\src\AFK4.Platform.Api\AFK4.Platform.Api.csproj --output .\artifacts\platform-organization-cutover.sql
```

- [ ] **Step 7: Commit source migration and tests; do not commit the generated `artifacts/` SQL**

```powershell
git add src/AFK4.Platform.Api/Data/Migrations tests/AFK4.Platform.Api.Tests/Migrations scripts/rehearse-platform-organization-cutover.ps1
git commit -m "feat(identity): мигрировать роли и аннулировать сессии"
```

### Task 6: Move organization staff APIs to the organization namespace

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/*.cs` for every endpoint protected by organization staff authorization
- Modify: `src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: organization API clients under the future `src/AFK4.OrganizationAdmin.Web/src/api/clients/**`
- Modify: native clients under the future `src/AFK4.OrganizationAdmin.App/**/HttpOrganization*ApiClient.cs`
- Modify: organization endpoint tests under `tests/AFK4.Platform.Api.Tests/**`
- Modify: web API tests under the future `src/AFK4.OrganizationAdmin.Web/src/**/*.test.ts*`
- Delete: old `/api/owner/*` and old staff-authorized route mappings

**Interfaces:**
- Consumes: `RequireOrganizationDomain()` and `OrganizationPermissionNames`.
- Produces: staff sign-in/refresh/sign-out and all Organization Admin business operations below `/api/organizations/{organizationId}/*`.
- Preserves: device credential/heartbeat/hub routes, player routes, public Account Activation, webhooks, and health endpoints in their existing non-staff domains.

- [ ] **Step 1: Generate a reviewed route inventory from endpoint metadata and classify every route as platform, organization, device, player, public, webhook, or health**

Store the expected organization route set directly in `AuthenticationDomainEndpointTests`; do not keep an unverified prose inventory.

- [ ] **Step 2: Add RED tests proving old routes return 404 and canonical routes enforce the path organization**

```csharp
var oldResponse = await client.GetAsync("/api/owner/branches");
Assert.Equal(HttpStatusCode.NotFound, oldResponse.StatusCode);

var crossOrganization = await organizationClient.GetAsync($"/api/organizations/{otherOrganizationId:D}/branches");
Assert.Equal(HttpStatusCode.Forbidden, crossOrganization.StatusCode);
```

- [ ] **Step 3: Introduce one organization route group and attach domain metadata once**

```csharp
var organizations = app.MapGroup("/api/organizations/{organizationId:guid}")
    .RequireOrganizationDomain();
```

Each endpoint continues to require its existing branch/organization permission in addition to domain and organization-id matching.

- [ ] **Step 4: Move endpoints feature by feature and update corresponding clients in the same slice**

Order: authentication/context, branches/settings, floor map/devices/install, staff/roles, tariffs/packages, POS/inventory, sessions/players/shifts, reports/audit, payments/loyalty/news/updates. Run each feature's focused endpoint and client tests before moving to the next.

- [ ] **Step 5: Run endpoint architecture and old-route guards GREEN**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AuthenticationDomainEndpointTests|FullyQualifiedName~LegacyEndpointGuard|FullyQualifiedName~StaffAuthentication"
```

- [ ] **Step 6: Run all backend and Organization Admin web tests**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj
Push-Location .\src\AFK4.OrganizationAdmin.Web; bun test; Pop-Location
```

- [ ] **Step 7: Commit**

```powershell
git add src/AFK4.Platform.Api/Endpoints src/AFK4.Platform.Api/Program.cs src/AFK4.OrganizationAdmin.App src/AFK4.OrganizationAdmin.Web tests/AFK4.Platform.Api.Tests
git commit -m "refactor(api): разделить organization endpoint namespace"
```

### Task 7: Make Account Activation a named public boundary

**Files:**
- Rename: `src/AFK4.Platform.Api/Data/OwnerInviteEntity.cs` -> `OrganizationOwnerInviteEntity.cs`
- Rename: corresponding contracts/services/endpoints/tests containing `OwnerInvite` to `OrganizationOwnerInvite`
- Create: `src/AFK4.Shared.Contracts/Identity/AccountActivation/**`
- Modify: `src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs`
- Move: `src/AFK4.Platform.Web/src/components/AcceptOwnerInvite.tsx` -> future `src/AFK4.PlatformControl.Web/src/account-activation/AccountActivation.tsx`
- Move: `src/AFK4.Platform.Web/src/api/ownerInviteAcceptanceApi.ts` -> future `src/AFK4.PlatformControl.Web/src/account-activation/accountActivationApi.ts`
- Test: matching backend contract/endpoint and frontend component tests

**Interfaces:**
- Produces public `POST /api/account-activation/organization-owner`.
- Consumes: one-time activation code plus password/profile input.
- Returns: activation result and Organization Admin download/launch guidance; it does not return access or refresh tokens.

- [ ] **Step 1: Change tests first to assert the activation response has no token fields**

```csharp
var json = await response.Content.ReadFromJsonAsync<JsonElement>();
Assert.False(json.TryGetProperty("accessToken", out _));
Assert.False(json.TryGetProperty("refreshToken", out _));
```

- [ ] **Step 2: Run backend and frontend activation tests RED**

- [ ] **Step 3: Rename the domain/contracts and move the public route without retaining the old acceptance endpoint**

Keep the existing short-lived, one-time, revocable, audited semantics. Remove token issuance from the activation response and require a normal Organization Admin sign-in afterward.

- [ ] **Step 4: Rename Platform Control UI from owner-invite wording to organization-owner invitation wording and show the canonical Account Activation URL**

- [ ] **Step 5: Run activation tests GREEN**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OrganizationOwnerInvite|FullyQualifiedName~AccountActivation"
Push-Location .\src\AFK4.PlatformControl.Web; bun test src/account-activation; Pop-Location
```

- [ ] **Step 6: Commit**

```powershell
git add src/AFK4.Platform.Api src/AFK4.Shared.Contracts src/AFK4.PlatformControl.Web tests
git commit -m "refactor(onboarding): выделить Account Activation"
```

### Task 8: Rename Platform Web to Platform Control

**Files:**
- Rename directory: `src/AFK4.Platform.Web` -> `src/AFK4.PlatformControl.Web`
- Rename current `PlatformTenant*` contracts/services/endpoints/tests to `PlatformOrganization*`
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/**`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformTenantEndpoints.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Tenancy/**`
- Modify: `tests/AFK4.Platform.Api.Tests/Platform/PlatformTenant*Tests.cs`
- Modify: root `package.json` and `bun.lock`
- Rename: `deploy/coolify/platform-web.Dockerfile` -> `platform-control.Dockerfile`
- Rename: `deploy/coolify/platform-web.nginx.conf` -> `platform-control.nginx.conf`
- Modify: `.github/workflows/coolify-staging-deploy.yml`
- Modify: Platform Control environment templates and Docker references under `deploy/coolify/**`
- Modify: Platform Control package name, page title, shell copy, i18n keys, tests, and routes

**Interfaces:**
- Produces workspace package `afk4-platform-control-web` and Docker target `platform-control`.
- Preserves Platform Control route root `/admin` unless a separate URL decision is approved; product naming does not require route churn.

- [ ] **Step 1: Change package/build tests to expect canonical paths and names, then verify RED**

```ts
expect(document.title).toBe('Platform Control');
```

- [ ] **Step 2: Rename the directory with `git mv` and update workspace/build references**

```powershell
git mv src/AFK4.Platform.Web src/AFK4.PlatformControl.Web
git mv deploy/coolify/platform-web.Dockerfile deploy/coolify/platform-control.Dockerfile
git mv deploy/coolify/platform-web.nginx.conf deploy/coolify/platform-control.nginx.conf
bun install
```

- [ ] **Step 3: Replace active product copy and code identifiers; retain `platform` when it describes the domain rather than the old product**

Rename current platform-facing `tenant` types, routes, DTO fields, and UI copy to `organization`. Platform routes become `/api/platform/organizations/*`; do not retain `/api/platform/tenants/*`. Keep `tenant` only where it describes the general multi-tenancy architecture rather than a user-visible resource or API contract.

- [ ] **Step 4: Run Platform Control tests/build and Docker smoke**

```powershell
Push-Location .\src\AFK4.PlatformControl.Web; bun test; bun run build; Pop-Location
docker build -f .\deploy\coolify\platform-control.Dockerfile -t afk4-platform-control:smoke .
docker run --rm -d --name afk4-platform-control-smoke -p 18085:80 afk4-platform-control:smoke
try { (Invoke-WebRequest http://127.0.0.1:18085/healthz).Content | Should -Be 'ok' } finally { docker stop afk4-platform-control-smoke }
```

- [ ] **Step 5: Commit**

```powershell
git add package.json bun.lock src/AFK4.PlatformControl.Web deploy/coolify .github/workflows/coolify-staging-deploy.yml
git commit -m "refactor(platform-control): переименовать web-продукт"
```

### Task 9: Rename Operator App to Organization Admin across native and web projects

**Files:**
- Rename directory/project: `src/AFK4.Operator.App` -> `src/AFK4.OrganizationAdmin.App`
- Rename directory/package: `src/AFK4.Operator.App.Web` -> `src/AFK4.OrganizationAdmin.Web`
- Rename directory/project: `tests/AFK4.Operator.App.Tests` -> `tests/AFK4.OrganizationAdmin.App.Tests`
- Modify: `AFK4.sln`
- Modify: all C# namespaces/types/files whose `Operator` prefix identifies the product rather than the staff role
- Modify: root `package.json`, `bun.lock`, project `package.json`, Vite config, WPF XAML, assembly metadata, assets, bootstrap bridge, protected-storage names, cache paths, and logs
- Test: renamed native and frontend test projects

**Interfaces:**
- Produces assemblies `AFK4.OrganizationAdmin.App` and `AFK4.OrganizationAdmin.App.Tests`, package `afk4-organization-admin-web`, and executable `AFK4.OrganizationAdmin.App.exe`.
- Preserves domain terms such as `Operator` role, operator display name, and operator-specific workflow where they describe the person rather than the product.

- [ ] **Step 1: Add RED assertions for assembly, executable, app title, protected-storage namespace, and frontend package name**

- [ ] **Step 2: Rename directories and projects using `git mv`**

```powershell
git mv src/AFK4.Operator.App src/AFK4.OrganizationAdmin.App
git mv src/AFK4.Operator.App.Web src/AFK4.OrganizationAdmin.Web
git mv tests/AFK4.Operator.App.Tests tests/AFK4.OrganizationAdmin.App.Tests
git mv src/AFK4.OrganizationAdmin.App/AFK4.Operator.App.csproj src/AFK4.OrganizationAdmin.App/AFK4.OrganizationAdmin.App.csproj
git mv tests/AFK4.OrganizationAdmin.App.Tests/AFK4.Operator.App.Tests.csproj tests/AFK4.OrganizationAdmin.App.Tests/AFK4.OrganizationAdmin.App.Tests.csproj
```

- [ ] **Step 3: Update solution/project references and product-scoped namespaces/types**

Rename `OperatorAppOptions`, `OperatorTokenStore`, `OperatorConnectionStore`, `OperatorWeb*`, and product-wide shell/bootstrap types to `OrganizationAdmin*`. Do not rename genuine `Operator` role copy or audit actor concepts.

- [ ] **Step 4: Deliberately invalidate old local sessions and caches**

Use new protected-storage, WebView2 profile, connection-store, and cache identifiers under `AFK4.OrganizationAdmin`. Do not migrate old token blobs; the server migration invalidates them anyway.

- [ ] **Step 5: Run frontend tests/build and native tests/build GREEN**

```powershell
Push-Location .\src\AFK4.OrganizationAdmin.Web; bun test; bun run build; Pop-Location
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.OrganizationAdmin.App.Tests\AFK4.OrganizationAdmin.App.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' build .\src\AFK4.OrganizationAdmin.App\AFK4.OrganizationAdmin.App.csproj -c Release
```

- [ ] **Step 6: Commit**

```powershell
git add AFK4.sln package.json bun.lock src/AFK4.OrganizationAdmin.App src/AFK4.OrganizationAdmin.Web tests/AFK4.OrganizationAdmin.App.Tests
git commit -m "refactor(organization-admin): переименовать desktop-продукт"
```

### Task 10: Block incompatible Organization Admin clients

**Files:**
- Create: `src/AFK4.Platform.Api/Configuration/OrganizationAdminCompatibilityOptions.cs`
- Create: `src/AFK4.Platform.Api/Identity/OrganizationAdminCompatibilityMiddleware.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `src/AFK4.Platform.Api/appsettings.json`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/platformApi.ts`
- Modify: `src/AFK4.OrganizationAdmin.App/Configuration/OrganizationAdminOptions.cs`
- Create: `tests/AFK4.Platform.Api.Tests/OrganizationAdminCompatibilityTests.cs`
- Modify: Organization Admin web/native API client tests

**Interfaces:**
- Produces required headers `X-AFK4-Product: organization-admin`, `X-AFK4-Compatibility-Epoch: 2`, and `X-AFK4-Client-Version` populated from the running assembly version on every Organization Admin request.
- Produces server option `OrganizationAdminCompatibility:RequiredEpoch = 2`.
- Returns `426 Upgrade Required` with stable code `organization_admin_upgrade_required`, required epoch, and configured download URL when an organization-domain route receives a missing or incompatible epoch.
- Exempts Platform Control, Account Activation, device, player, webhook, and health endpoints by applying only to `AuthenticationDomain.Organization` metadata.

- [ ] **Step 1: Write RED integration tests for missing, old, current, and unrelated-client headers**

```csharp
[Theory]
[InlineData(null)]
[InlineData("1")]
public async Task OrganizationRoute_WithIncompatibleEpoch_RequiresUpgrade(string? epoch)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{TestIds.OrganizationId:D}/branches");
    request.Headers.TryAddWithoutValidation("X-AFK4-Product", "organization-admin");
    if (epoch is not null) request.Headers.TryAddWithoutValidation("X-AFK4-Compatibility-Epoch", epoch);
    var response = await organizationClient.SendAsync(request);
    Assert.Equal(HttpStatusCode.UpgradeRequired, response.StatusCode);
}
```

Also prove epoch `2` reaches normal authorization, while Platform Control, player, device, public activation, webhook, and health calls do not require the header.

- [ ] **Step 2: Run focused tests RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~OrganizationAdminCompatibilityTests
```

- [ ] **Step 3: Implement the metadata-scoped compatibility middleware and stable error contract**

Reject malformed/non-integer epochs and wrong product names with the same safe `426` response. Do not expose internal version policy or accept `operator-app` as an alias.

- [ ] **Step 4: Send canonical headers from the web and native clients**

The native host injects product, compatibility epoch, and assembly version into the WebView bootstrap; the shared frontend transport adds them to every request. Tests must prove browser preview and native host produce the same headers.

- [ ] **Step 5: Run compatibility, Organization Admin API-client, and endpoint-domain tests GREEN**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OrganizationAdminCompatibility|FullyQualifiedName~AuthenticationDomainEndpoint"
Push-Location .\src\AFK4.OrganizationAdmin.Web; bun test; Pop-Location
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.OrganizationAdmin.App.Tests\AFK4.OrganizationAdmin.App.Tests.csproj --filter FullyQualifiedName~Bootstrap
```

- [ ] **Step 6: Commit**

```powershell
git add src/AFK4.Platform.Api src/AFK4.OrganizationAdmin.App src/AFK4.OrganizationAdmin.Web tests
git commit -m "feat(organization-admin): блокировать несовместимые клиенты"
```

### Task 11: Rename installer, update, CI, and runtime identities

**Files:**
- Rename: `installers/operator-app` -> `installers/organization-admin`
- Modify: `installers/organization-admin/Package.wxs`
- Modify: `installers/bundle/Bundle.wxs`
- Modify: `scripts/build-client-packages.ps1`
- Modify: `scripts/sign-client-packages.ps1`
- Modify: `scripts/publish-client-msi-updates.ps1`
- Modify: `scripts/register-update-package-requests.ps1`
- Modify: `scripts/install-afk4-update-msi.ps1`
- Modify: `scripts/rollback-afk4-update-msi.ps1`
- Modify: `.github/workflows/client-packages.yml`
- Modify: `.github/workflows/package-smoke.yml`
- Modify: `.github/workflows/pr-verification.yml`
- Modify: update component constants/contracts/backend tests that currently use `operator-app`
- Test: package script tests and Windows MSI smoke

**Interfaces:**
- Produces update component `organization-admin`, artifact `AFK4.OrganizationAdmin.msi`, executable `AFK4.OrganizationAdmin.App.exe`, installation folder `AFK4\Organization Admin`, and registry root `Software\AFK4\OrganizationAdmin`.
- Keeps the existing MSI `UpgradeCode` so installed Operator App upgrades in place instead of installing side-by-side; change product/display/file/component identifiers, not upgrade lineage.

- [ ] **Step 1: Change package tests to require Organization Admin identities and reject old artifact names**

- [ ] **Step 2: Rename installer directory and update every package/build/publish reference**

```powershell
git mv installers/operator-app installers/organization-admin
```

- [ ] **Step 3: Update WiX identity without changing `UpgradeCode`**

Use `Name="Organization Admin by AFK4"`, target the new executable, update shortcuts/descriptions/registry keys, and verify MajorUpgrade removes the predecessor product.

- [ ] **Step 4: Update update-component serialization with no `operator-app` alias**

The update payload identifies only `organization-admin`; Task 10's compatibility epoch is the authoritative server-side block for incompatible predecessors.

- [ ] **Step 5: Run Windows package build and MSI inspection**

```powershell
& .\scripts\build-client-packages.ps1 -Version '0.2.0' -Channel internal
& 'C:\Program Files\dotnet\dotnet.exe' test .\tests\AFK4.Update.Publisher.Tests\AFK4.Update.Publisher.Tests.csproj
```

Verify install over the current Operator App package, Start Menu/Desktop shortcuts, executable launch, uninstall, and no side-by-side old product entry.

- [ ] **Step 6: Commit**

```powershell
git add installers scripts .github src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests
git commit -m "build(organization-admin): переименовать package и update identity"
```

### Task 12: Update source-of-truth documentation and operational runbooks

**Files:**
- Modify: `README.md`
- Modify: `docs/product/AFK4-MVP-PRD.md`
- Modify: `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/roadmap/production-readiness.md`
- Modify: `docs/operations/client-packaging.md`
- Modify: `docs/operations/client-update-rollout.md`
- Modify: `docs/operations/coolify-staging-deploy.md`
- Modify: `docs/operations/postgres-backup-restore.md`
- Modify: `docs/operations/pilot-branch-setup.md`
- Modify: `docs/operations/real-device-windows-pc-smoke.md`
- Modify: `docs/operations/agent-installer-enrollment.md`
- Create: `docs/operations/platform-organization-big-bang-cutover.md`
- Modify: `docs/superpowers/specs/README.md` and `docs/superpowers/plans/README.md`

**Interfaces:**
- Produces current documentation with canonical vocabulary and a cutover runbook containing maintenance entry, backup, preflight, migration, coordinated release, smoke, no-return point, and complete snapshot rollback.
- Preserves historical terminology only in `docs/archive/**` and explicitly historical specs/notes.

- [ ] **Step 1: Update PRD and architecture first because they own durable product decisions**

- [ ] **Step 2: Update README, progress, roadmap, and all directly affected runbooks**

- [ ] **Step 3: Write the cutover runbook with exact commands and decision points**

The runbook must include: stop mutating traffic; verify backup and restore rehearsal; run unknown-role preflight; record release SHAs and artifact hashes; apply migration; deploy backend + both products; enforce minimum Organization Admin version; smoke Account Activation and all four representative roles; declare no-return point; restore the entire snapshot and previous release together on failure.

- [ ] **Step 4: Run vocabulary and link/path guards GREEN**

```powershell
& .\scripts\Test-CurrentProductVocabulary.ps1 -RepositoryRoot (Get-Location).Path
git diff --check
```

- [ ] **Step 5: Commit**

```powershell
git add README.md docs
git commit -m "docs(architecture): применить новую product terminology"
```

### Task 13: Prove the coordinated release candidate

**Files:**
- Modify only defects discovered by verification in the owning task's files
- Update: `docs/progress/2026-05-12-vertical-slice-progress.md` with fresh evidence
- Update: `docs/operations/platform-organization-big-bang-cutover.md` only when rehearsal changes durable commands or gates

**Interfaces:**
- Consumes all prior tasks.
- Produces a single release candidate with backend, Platform Control, Organization Admin, migration, MSI, Docker image, and runbook evidence tied to one Git SHA.

- [ ] **Step 1: Search for stale current-use identifiers**

```powershell
& .\scripts\Test-CurrentProductVocabulary.ps1 -RepositoryRoot (Get-Location).Path
rg --line-number --glob '!docs/archive/**' --glob '!docs/superpowers/specs/2026-07-28-platform-organization-product-boundary-design.md' 'AFK4\.Operator\.App|AFK4\.Platform\.Web|operator-app|platform-web|StaffPermissionNames|StaffRoleNames|platform_owner|cashier_operator|accountant_auditor|/api/owner'
```

Expected: no active-code/config/build hits. Review every documentation hit as historical evidence or fix it.

- [ ] **Step 2: Run the full solution verification on Windows**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build .\AFK4.sln -c Release
& 'C:\Program Files\dotnet\dotnet.exe' test .\AFK4.sln -c Release --no-build
```

- [ ] **Step 3: Run all web tests and production builds**

```powershell
bun install --frozen-lockfile
Push-Location .\src\AFK4.PlatformControl.Web; bun test; bun run build; Pop-Location
Push-Location .\src\AFK4.OrganizationAdmin.Web; bun test; bun run build; Pop-Location
Push-Location .\packages\i18n; bun test; Pop-Location
```

- [ ] **Step 4: Rehearse the database cutover on a recent sanitized production-shaped snapshot**

```powershell
& .\scripts\rehearse-platform-organization-cutover.ps1 -ConnectionString $env:AFK4_CUTOVER_REHEARSAL_CONNECTION
```

Expected: every old role mapped exactly once, no unknown roles, all six human token tables have no active rows, device credentials are unchanged, audit rows are readable, and migration duration is recorded without printing the connection string.

- [ ] **Step 5: Build and smoke the deployable artifacts**

```powershell
docker build -f .\deploy\coolify\platform-control.Dockerfile -t afk4-platform-control:rc .
& .\scripts\build-client-packages.ps1 -Version '0.2.0' -Channel internal
```

Run Docker `/healthz`, MSI upgrade/install/launch/uninstall, WebView2 production bundle, and update/minimum-version smoke on a clean Windows VM at 100% and 125% scale.

- [ ] **Step 6: Run auth and product smoke against the release candidate**

Prove: old platform, organization, and player access/refresh tokens fail; each human audience can sign in again; Platform Admin signs in to Platform Control; Platform Support receives only bounded permissions; Organization Owner signs in to Organization Admin and sees organization-wide areas; Operator sees only operational areas; cross-organization route substitution is forbidden; Account Activation returns no browser staff session.

- [ ] **Step 7: Self-review final diff and record evidence**

Check for unintended scope, stale docs, secrets, missing migration coverage, old external names, changed MSI UpgradeCode, and partial rollback instructions. Update progress with exact test/build/package counts and the verified RC SHA.

- [ ] **Step 8: Commit verification-only corrections and evidence**

```powershell
git add docs/progress/2026-05-12-vertical-slice-progress.md docs/operations/platform-organization-big-bang-cutover.md
git diff --cached --check
git commit -m "test(release): подтвердить platform organization cutover"
```

Any source correction discovered here must first be staged and committed as a focused fix using the exact owning files from Tasks 2-11; do not mix it into the evidence commit or use a broad `git add`.

## External Cutover Gate

Implementation, commits, and local/staging rehearsal do not authorize production deployment. Before the maintenance window, obtain explicit approval for the exact backend SHA, Platform Control image digest, Organization Admin MSI SHA-256, database snapshot identifier, expected downtime, forced re-login, and rollback point. Do not push, deploy, change DNS, revoke live sessions, or apply the production migration merely because this plan is complete.
