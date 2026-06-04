# Auth & Onboarding UX Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the seven gross convenience violations in the sign-in/onboarding userflow — chiefly the club "Club key" requirement — by resolving the club from the entered login (model B), localizing auth screens, landing on the dashboard, simplifying onboarding, auto-generating tenant slugs, and making the forgot-password page honest.

**Architecture:** One new backend endpoint `POST /api/auth/staff/sign-in-by-login` resolves the club from a globally-searched login with a password-verified disambiguation fallback (no DB migration — the existing per-org unique index stays). The native Operator App and existing endpoints are untouched. The rest is frontend: a login-only club sign-in with a club picker, localized auth screens (RU/EN), a slugify helper with ru→latin transliteration, and routing tweaks.

**Tech Stack:** ASP.NET Core / .NET 10 minimal APIs, EF Core (Npgsql), xUnit (`Assert.*`, `PlatformApiFactory` in-memory). React 19 + TypeScript + Vite, Vitest (`globals:false` — import `{describe,it,expect,vi}` from `'vitest'`), flat-key i18n in `src/i18n/messages.ts` (ru block then en block, parity test enforced).

**Gates (run for every task that touches that side):**
- Backend: `& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal`
- Frontend (from `src/AFK4.Platform.Web`): `npm run build` (tsc -b — vitest does NOT type-check) **and** `npm test`

**Money/i18n reminders:** not relevant to this plan (no currency). i18n parity test fails if a key exists in `ru` but not `en` (or vice-versa) — always add to BOTH blocks.

---

## File Structure

**Backend (`src/AFK4.Platform.Api`, `src/AFK4.Shared.Contracts`)**
- Create `src/AFK4.Shared.Contracts/Identity/StaffSignInByLoginRequest.cs` — `{ Login, Password }`.
- Create `src/AFK4.Shared.Contracts/Identity/StaffSignInClubChoice.cs` — `{ OrganizationId, Name }`.
- Create `src/AFK4.Shared.Contracts/Identity/StaffSignInChooseClubResponse.cs` — `{ Clubs }`.
- Modify `src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs` — add `SignInByLoginAsync` + `StaffLoginResolution`.
- Modify `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs` — implement it.
- Modify `src/AFK4.Platform.Api/Program.cs` — map the new endpoint (next to the existing staff sign-in maps ~line 504).
- Modify `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs` — derive display name from login when blank.
- Test `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs` — add login-resolution tests.
- Test `tests/AFK4.Platform.Api.Tests/Platform/PlatformTenantEndpointTests.cs` — add accept-invite blank-display-name test (or co-locate with existing accept tests; see Task 2).

**Frontend (`src/AFK4.Platform.Web/src`)**
- Modify `api/types.ts` — add `StaffSignInClubChoice`.
- Modify `api/staffAuthApi.ts` — `signInByLogin` + `signInToClub` + `StaffSignInChooseClubError`; drop tenant-key `signIn`.
- Modify `api/staffAuthApi.test.ts` — cover the new methods.
- Rewrite `components/StaffSignIn.tsx` — login-only + club picker + cross-link + i18n.
- Modify `components/SignIn.tsx` — i18n.
- Modify `components/AcceptInvite.tsx` — i18n, "Логин" label, drop the display-name field.
- Modify `App.tsx` — localize `ReservedAuthPage`, add `navigateToClubDashboard`/`navigateToAcceptInvite`, land club sign-in on dashboard, wire the accept-invite cross-link.
- Modify `i18n/messages.ts` — add the `auth.*` key group (ru + en).
- Create `lib/slugify.ts` — ru→latin slug helper.
- Create `lib/slugify.test.ts`.
- Modify `platform/tenants/NewTenantScreen.tsx` — auto-slug from name (editable).

---

## Task 1: Backend — `sign-in-by-login` endpoint (model B resolution)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Identity/StaffSignInByLoginRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Identity/StaffSignInClubChoice.cs`
- Create: `src/AFK4.Shared.Contracts/Identity/StaffSignInChooseClubResponse.cs`
- Modify: `src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (near line 504)
- Test: `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs`

- [ ] **Step 1: Add the contracts**

Create `src/AFK4.Shared.Contracts/Identity/StaffSignInByLoginRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInByLoginRequest(
    string Login,
    string Password);
```

Create `src/AFK4.Shared.Contracts/Identity/StaffSignInClubChoice.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInClubChoice(
    Guid OrganizationId,
    string Name);
```

Create `src/AFK4.Shared.Contracts/Identity/StaffSignInChooseClubResponse.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInChooseClubResponse(
    IReadOnlyList<StaffSignInClubChoice> Clubs);
```

- [ ] **Step 2: Extend the service interface**

In `src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs`, add the method and a result type:

```csharp
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public interface IStaffCredentialService
{
    Task<StaffSignInResponse?> SignInAsync(StaffSignInRequest request, CancellationToken cancellationToken);

    Task<StaffSignInResponse?> SignInByTenantKeyAsync(
        StaffSignInByTenantKeyRequest request,
        CancellationToken cancellationToken);

    Task<StaffLoginResolution> SignInByLoginAsync(
        StaffSignInByLoginRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of resolving a club from a bare login. Exactly one of the cases holds:
/// <list type="bullet">
/// <item>0 password-verified matches: <see cref="SignedIn"/> null and <see cref="Clubs"/> empty.</item>
/// <item>1 match: <see cref="SignedIn"/> set.</item>
/// <item>2+ matches: <see cref="Clubs"/> populated for a disambiguation picker.</item>
/// </list>
/// </summary>
public sealed record StaffLoginResolution(
    StaffSignInResponse? SignedIn,
    IReadOnlyList<StaffSignInClubChoice> Clubs)
{
    public static readonly StaffLoginResolution None =
        new(null, Array.Empty<StaffSignInClubChoice>());
}
```

- [ ] **Step 3: Write the failing endpoint tests**

In `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs`, add these tests (the `SeedTechnicianAsync` helper already seeds org `demo-club` with `tech@afk4.test` / `Passw0rd!`). Add a second-org seeder inline in the collision test.

```csharp
    [Fact]
    public async Task PostStaffSignInByLogin_SingleClub_ReturnsAccessToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("tech@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.Contains(StaffPermissionNames.CreateDeviceEnrollmentCode, body.Permissions);
    }

    [Fact]
    public async Task PostStaffSignInByLogin_WrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("tech@afk4.test", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostStaffSignInByLogin_UnknownLogin_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("nobody@afk4.test", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostStaffSignInByLogin_SameLoginDifferentPasswords_SignsIntoCorrectClub()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory); // org A: tech@afk4.test / Passw0rd!
        await SeedSecondClubAsync(factory, "shared@afk4.test", "OrgA-pass"); // also adds shared@ to org A
        await SeedSharedLoginInSecondOrgAsync(factory, "shared@afk4.test", "OrgB-pass");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("shared@afk4.test", "OrgB-pass"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(SecondOrgId, body.OrganizationId);
    }

    [Fact]
    public async Task PostStaffSignInByLogin_SameLoginSamePasswordTwoClubs_ReturnsChooseClub()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        await SeedSecondClubAsync(factory, "shared@afk4.test", "Same-pass"); // org A
        await SeedSharedLoginInSecondOrgAsync(factory, "shared@afk4.test", "Same-pass"); // org B
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("shared@afk4.test", "Same-pass"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInChooseClubResponse>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Clubs.Count);
        Assert.Contains(body.Clubs, c => c.OrganizationId == TestIds.OrganizationId);
        Assert.Contains(body.Clubs, c => c.OrganizationId == SecondOrgId);
    }
```

Add these helpers + id to the test class (place after `SeedTechnicianAsync`):

```csharp
    private static readonly Guid SecondOrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f09");
    private static readonly Guid SecondBranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c3");

    // Adds `login` to the EXISTING org A with the given password.
    private static async Task SeedSecondClubAsync(PlatformApiFactory factory, string login, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var user = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            UserName = login,
            NormalizedUserName = login.ToUpperInvariant(),
            DisplayName = "Shared A",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        dbContext.StaffUsers.Add(user);
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            RoleName = StaffRoleNames.Owner
        });
        await dbContext.SaveChangesAsync();
    }

    // Creates org B and adds the same login there with its own password.
    private static async Task SeedSharedLoginInSecondOrgAsync(PlatformApiFactory factory, string login, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var createdAt = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = SecondOrgId,
            Slug = "second-club",
            Name = "Second Org",
            CreatedAtUtc = createdAt
        });
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = SecondBranchId,
            OrganizationId = SecondOrgId,
            Slug = "main",
            Name = "Second Branch",
            CreatedAtUtc = createdAt
        });
        var user = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = SecondOrgId,
            UserName = login,
            NormalizedUserName = login.ToUpperInvariant(),
            DisplayName = "Shared B",
            IsActive = true,
            CreatedAtUtc = createdAt
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        dbContext.StaffUsers.Add(user);
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = SecondOrgId,
            BranchId = SecondBranchId,
            RoleName = StaffRoleNames.Owner
        });
        await dbContext.SaveChangesAsync();
    }
```

- [ ] **Step 4: Run the tests — verify they fail**

Run: `& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PostStaffSignInByLogin" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: compile error / 404 → tests FAIL (endpoint not mapped yet).

- [ ] **Step 5: Implement `SignInByLoginAsync`**

In `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs`, add this method to the class (after `SignInByTenantKeyAsync`):

```csharp
    public async Task<StaffLoginResolution> SignInByLoginAsync(
        StaffSignInByLoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return StaffLoginResolution.None;
        }

        var normalizedLogin = request.Login.Trim().ToUpperInvariant();
        var candidates = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(candidate => candidate.NormalizedUserName == normalizedLogin && candidate.IsActive)
            .Select(candidate => new { candidate.OrganizationId, candidate.StaffUserId, candidate.PasswordHash })
            .ToListAsync(cancellationToken);

        var matchedOrgIds = new List<Guid>();
        foreach (var candidate in candidates)
        {
            // VerifyHashedPassword only reads the hash; the entity is a placeholder.
            var placeholder = new StaffUserEntity
            {
                StaffUserId = candidate.StaffUserId,
                OrganizationId = candidate.OrganizationId
            };
            var result = passwordHasher.VerifyHashedPassword(placeholder, candidate.PasswordHash, request.Password);
            if (result != PasswordVerificationResult.Failed)
            {
                matchedOrgIds.Add(candidate.OrganizationId);
            }
        }

        if (matchedOrgIds.Count == 0)
        {
            return StaffLoginResolution.None;
        }

        if (matchedOrgIds.Count == 1)
        {
            var signedIn = await SignInAsync(
                new StaffSignInRequest(matchedOrgIds[0], request.Login, request.Password),
                cancellationToken);
            return new StaffLoginResolution(signedIn, Array.Empty<StaffSignInClubChoice>());
        }

        var clubs = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => matchedOrgIds.Contains(organization.OrganizationId))
            .Select(organization => new StaffSignInClubChoice(organization.OrganizationId, organization.Name))
            .ToListAsync(cancellationToken);
        return new StaffLoginResolution(null, clubs);
    }
```

- [ ] **Step 6: Map the endpoint**

In `src/AFK4.Platform.Api/Program.cs`, immediately after the existing `app.MapPost("/api/auth/staff/sign-in-by-tenant-key", ...)` block (ends ~line 526), add:

```csharp
app.MapPost("/api/auth/staff/sign-in-by-login", async (
    StaffSignInByLoginRequest request,
    IStaffCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var resolution = await credentialService.SignInByLoginAsync(request, cancellationToken);

    if (resolution.SignedIn is not null)
    {
        return Results.Ok(resolution.SignedIn);
    }

    return resolution.Clubs.Count > 0
        ? Results.Json(
            new StaffSignInChooseClubResponse(resolution.Clubs),
            statusCode: StatusCodes.Status409Conflict)
        : Results.Unauthorized();
});
```

(`StaffSignInByLoginRequest`/`StaffSignInChooseClubResponse` are in `AFK4.Shared.Contracts.Identity`, already imported at the top of `Program.cs` via the existing `using AFK4.Shared.Contracts.Identity;`.)

- [ ] **Step 7: Run the tests — verify they pass**

Run: `& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PostStaffSignInByLogin" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: PASS (5 tests).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs src/AFK4.Platform.Api/Identity src/AFK4.Platform.Api/Program.cs
git commit -m "feat(platform-api): staff sign-in-by-login resolves club from login (model B)"
```

---

## Task 2: Backend — accept-invite derives display name from login when blank

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs:341-345,416`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformTenantEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Find the existing accept-owner-invite test in `tests/AFK4.Platform.Api.Tests/Platform/PlatformTenantEndpointTests.cs` (search for `owner-invites/accept`) and mirror its setup. Add:

```csharp
    [Fact]
    public async Task AcceptOwnerInvite_WithBlankDisplayName_DerivesDisplayNameFromLogin()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Reuse the helper the other accept-invite tests use to create a tenant + pending invite,
        // capturing the invite code. (Same arrangement as AcceptOwnerInvite_WithValidCode_*.)
        var code = await CreateTenantWithPendingInviteAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/platform/owner-invites/accept",
            new AcceptOwnerInviteRequest(
                Code: code,
                UserName: "owner@club.test",
                DisplayName: "",
                Password: "Passw0rd!Real"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("owner", body.DisplayName);
    }
```

If no shared `CreateTenantWithPendingInviteAsync` helper exists, inline the same arrangement the neighbouring valid-code test uses (create tenant via `/api/platform/tenants` as a platform admin, read `ownerInvite.code` from the response). Match the existing test's auth setup exactly.

- [ ] **Step 2: Run it — verify it fails**

Run: `& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AcceptOwnerInvite_WithBlankDisplayName" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: FAIL with 400 (current code rejects blank DisplayName).

- [ ] **Step 3: Replace the blank-rejection with a length-only guard**

In `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs`, replace the current block (lines 341-345):

```csharp
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > MaxDisplayNameLength)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"DisplayName is required and must contain {MaxDisplayNameLength} characters or fewer.");
        }
```

with:

```csharp
        if (request.DisplayName is { } providedDisplayName && providedDisplayName.Trim().Length > MaxDisplayNameLength)
        {
            return PlatformTenantOperationResult<StaffSignInResponse>.BadRequest(
                $"DisplayName must contain {MaxDisplayNameLength} characters or fewer.");
        }
```

- [ ] **Step 4: Derive the display name at staff creation**

In the same file, change line 416 from:

```csharp
            DisplayName = request.DisplayName.Trim(),
```

to:

```csharp
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? DeriveDisplayNameFromLogin(requestedUserName)
                : request.DisplayName.Trim(),
```

Add this private static helper to the class (place it near the other private helpers, e.g. just below `AcceptOwnerInviteAsync`):

```csharp
    private static string DeriveDisplayNameFromLogin(string login)
    {
        var atIndex = login.IndexOf('@');
        var localPart = atIndex > 0 ? login[..atIndex] : login;
        return localPart.Trim();
    }
```

- [ ] **Step 5: Run it — verify it passes**

Run: `& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AcceptOwnerInvite" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: PASS (new test + existing accept-invite tests stay green).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs tests/AFK4.Platform.Api.Tests/Platform/PlatformTenantEndpointTests.cs
git commit -m "feat(platform-api): accept-invite derives owner display name from login when blank"
```

---

## Task 3: Frontend — staffAuthApi `signInByLogin` + `signInToClub`

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/types.ts`
- Modify: `src/AFK4.Platform.Web/src/api/staffAuthApi.ts`
- Test: `src/AFK4.Platform.Web/src/api/staffAuthApi.test.ts`

- [ ] **Step 1: Add the club-choice type**

In `src/AFK4.Platform.Web/src/api/types.ts`, add after `StaffSignInResponse` (line 23):

```typescript
export interface StaffSignInClubChoice {
  organizationId: string;
  name: string;
}
```

- [ ] **Step 2: Rewrite the failing tests**

Replace the two tenant-key tests in `src/AFK4.Platform.Web/src/api/staffAuthApi.test.ts` (the `'signs in staff users through the tenant-key staff auth endpoint'` test and the `'throws a parsed PlatformApiError when staff sign-in fails'` test) with:

```typescript
  it('signs in by login through the sign-in-by-login endpoint', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, buildResponse()));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.signInByLogin('owner@demo.test', 'Passw0rd!Real');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/sign-in-by-login');
    expect(JSON.parse(call[1].body as string)).toEqual({
      login: 'owner@demo.test',
      password: 'Passw0rd!Real'
    });
  });

  it('throws StaffSignInChooseClubError on a 409 with clubs', async () => {
    const clubs = [
      { organizationId: 'org-a', name: 'Club A' },
      { organizationId: 'org-b', name: 'Club B' }
    ];
    const fetchImpl = vi.fn(async () => jsonResponse(409, { clubs }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(client.signInByLogin('shared@demo.test', 'pw'))
      .rejects.toMatchObject({ clubs });
  });

  it('signs in to a chosen club through the org-scoped endpoint', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, buildResponse()));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.signInToClub('org-b', 'shared@demo.test', 'pw');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/sign-in');
    expect(JSON.parse(call[1].body as string)).toEqual({
      organizationId: 'org-b',
      userName: 'shared@demo.test',
      password: 'pw'
    });
  });

  it('throws a parsed PlatformApiError when login sign-in fails', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(401, { error: 'Bad credentials' }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(client.signInByLogin('owner', 'wrong')).rejects.toMatchObject({
      status: 401,
      message: 'Bad credentials'
    });
    await expect(client.signInByLogin('owner', 'wrong')).rejects.toBeInstanceOf(PlatformApiError);
  });
```

- [ ] **Step 3: Run the tests — verify they fail**

Run (from `src/AFK4.Platform.Web`): `npm test -- staffAuthApi`
Expected: FAIL (`signInByLogin`/`signInToClub`/`StaffSignInChooseClubError` undefined).

- [ ] **Step 4: Implement the client methods**

In `src/AFK4.Platform.Web/src/api/staffAuthApi.ts`:

Add the import of the new type at the top:

```typescript
import type { AcceptOwnerInviteRequest, StaffSignInClubChoice, StaffSignInResponse } from './types';
```

Add the error class after the imports (above `export interface StaffAuthApiClientOptions`):

```typescript
export class StaffSignInChooseClubError extends Error {
  public readonly clubs: StaffSignInClubChoice[];

  public constructor(clubs: StaffSignInClubChoice[]) {
    super('Multiple clubs match this login.');
    this.name = 'StaffSignInChooseClubError';
    this.clubs = clubs;
  }
}
```

Replace the existing `signIn(tenantKey, userName, password)` method with:

```typescript
  public async signInByLogin(login: string, password: string): Promise<StaffSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/sign-in-by-login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ login, password })
    });
    if (response.status === 409) {
      const body = (await response.json()) as { clubs: StaffSignInClubChoice[] };
      throw new StaffSignInChooseClubError(body.clubs);
    }
    return this.readAndApplySession(response, 'Sign-in failed.');
  }

  public async signInToClub(organizationId: string, login: string, password: string): Promise<StaffSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/sign-in`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ organizationId, userName: login, password })
    });
    return this.readAndApplySession(response, 'Sign-in failed.');
  }
```

- [ ] **Step 5: Run the tests — verify they pass**

Run (from `src/AFK4.Platform.Web`): `npm test -- staffAuthApi`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Web/src/api/types.ts src/AFK4.Platform.Web/src/api/staffAuthApi.ts src/AFK4.Platform.Web/src/api/staffAuthApi.test.ts
git commit -m "feat(platform-web): staffAuthApi signInByLogin + signInToClub (drop tenant-key signIn)"
```

---

## Task 4: Frontend — i18n keys + login-only StaffSignIn (picker, cross-link) + land on dashboard

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts` (ru + en blocks)
- Rewrite: `src/AFK4.Platform.Web/src/components/StaffSignIn.tsx`
- Modify: `src/AFK4.Platform.Web/src/App.tsx` (StaffSignIn render sites + nav helpers)

- [ ] **Step 1: Add the full `auth.*` key group (ru + en)**

In `src/AFK4.Platform.Web/src/i18n/messages.ts`, add the following keys to the **`ru`** block (anywhere inside it, grouped together):

```typescript
    'auth.club.title': 'Вход в клуб',
    'auth.club.subtitle': 'Войдите под учётной записью сотрудника клуба.',
    'auth.admin.title': 'Панель управления платформой',
    'auth.admin.subtitle': 'Войдите под учётной записью администратора платформы.',
    'auth.field.login': 'Логин',
    'auth.field.password': 'Пароль',
    'auth.action.signIn': 'Войти',
    'auth.action.signingIn': 'Вход…',
    'auth.error.required': 'Введите логин и пароль.',
    'auth.error.invalid': 'Неверный логин или пароль.',
    'auth.error.generic': 'Не удалось войти.',
    'auth.chooseClub.title': 'Выберите клуб',
    'auth.chooseClub.subtitle': 'Этот логин найден в нескольких клубах. Выберите нужный.',
    'auth.chooseClub.back': 'Назад',
    'auth.haveCode': 'Впервые здесь? У меня есть код приглашения',
    'auth.accept.title': 'Активация по коду',
    'auth.accept.subtitle': 'Создайте вход владельца для этого клуба.',
    'auth.accept.field.code': 'Код приглашения',
    'auth.accept.field.confirmPassword': 'Повторите пароль',
    'auth.accept.action.submit': 'Активировать и открыть клуб',
    'auth.accept.action.submitting': 'Активация…',
    'auth.accept.action.signInInstead': 'Уже есть аккаунт? Войти',
    'auth.accept.error.codeRequired': 'Введите код приглашения.',
    'auth.accept.error.loginRequired': 'Введите логин.',
    'auth.accept.error.passwordLength': 'Пароль должен быть не короче 8 символов.',
    'auth.accept.error.passwordMismatch': 'Пароли не совпадают.',
    'auth.accept.error.codeNotFound': 'Код приглашения не найден.',
    'auth.accept.error.loginTaken': 'Этот логин уже занят.',
    'auth.accept.error.generic': 'Не удалось активировать код.',
    'auth.reset.title': 'Сброс пароля',
    'auth.reset.message': 'Сброс пароля скоро будет доступен. Обратитесь к администратору клуба.',
    'auth.reset.back': 'Вернуться ко входу',
```

Add the identical keys with English values to the **`en`** block:

```typescript
    'auth.club.title': 'Club sign in',
    'auth.club.subtitle': 'Sign in with your club staff account.',
    'auth.admin.title': 'Platform Control Plane',
    'auth.admin.subtitle': 'Sign in with your platform admin credentials.',
    'auth.field.login': 'Login',
    'auth.field.password': 'Password',
    'auth.action.signIn': 'Sign in',
    'auth.action.signingIn': 'Signing in…',
    'auth.error.required': 'Login and password are required.',
    'auth.error.invalid': 'Wrong login or password.',
    'auth.error.generic': 'Sign-in failed.',
    'auth.chooseClub.title': 'Choose a club',
    'auth.chooseClub.subtitle': 'This login was found in several clubs. Pick the one you want.',
    'auth.chooseClub.back': 'Back',
    'auth.haveCode': 'First time here? I have a setup code',
    'auth.accept.title': 'Accept setup code',
    'auth.accept.subtitle': 'Create the owner sign-in for this club.',
    'auth.accept.field.code': 'Setup code',
    'auth.accept.field.confirmPassword': 'Confirm password',
    'auth.accept.action.submit': 'Accept and open club',
    'auth.accept.action.submitting': 'Accepting…',
    'auth.accept.action.signInInstead': 'Already have an account? Sign in',
    'auth.accept.error.codeRequired': 'Setup code is required.',
    'auth.accept.error.loginRequired': 'Login is required.',
    'auth.accept.error.passwordLength': 'Password must be at least 8 characters.',
    'auth.accept.error.passwordMismatch': 'Passwords do not match.',
    'auth.accept.error.codeNotFound': 'Setup code was not found.',
    'auth.accept.error.loginTaken': 'That login is already in use.',
    'auth.accept.error.generic': 'Setup code acceptance failed.',
    'auth.reset.title': 'Password reset',
    'auth.reset.message': 'Password reset is coming soon. Please contact your club administrator.',
    'auth.reset.back': 'Back to sign in',
```

- [ ] **Step 2: Verify i18n parity stays green**

Run (from `src/AFK4.Platform.Web`): `npm test -- messages`
Expected: PASS (ru/en parity test).

- [ ] **Step 3: Rewrite `StaffSignIn.tsx` (login-only + picker + cross-link)**

Replace the entire contents of `src/AFK4.Platform.Web/src/components/StaffSignIn.tsx` with:

```typescript
import { useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import { StaffSignInChooseClubError, type StaffAuthApiClient } from '../api/staffAuthApi';
import type { StaffSignInClubChoice } from '../api/types';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

export interface StaffSignInProps {
  client: StaffAuthApiClient;
  onSignedIn: () => void;
  onOpenAcceptInvite: () => void;
}

export function StaffSignIn({ client, onSignedIn, onOpenAcceptInvite }: StaffSignInProps) {
  const { t } = useI18n();
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [clubChoices, setClubChoices] = useState<StaffSignInClubChoice[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedLogin = login.trim();
    if (normalizedLogin.length === 0 || password.length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.signInByLogin(normalizedLogin, password);
      onSignedIn();
    } catch (cause) {
      if (cause instanceof StaffSignInChooseClubError) {
        setClubChoices(cause.clubs);
      } else {
        setError(projectSignInError(cause, t));
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleChooseClub(organizationId: string) {
    setSubmitting(true);
    setError(null);
    try {
      await client.signInToClub(organizationId, login.trim(), password);
      onSignedIn();
    } catch (cause) {
      setClubChoices(null);
      setError(projectSignInError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  if (clubChoices !== null) {
    return (
      <div className="page page-narrow">
        <h1>{t('auth.chooseClub.title')}</h1>
        <p className="muted">{t('auth.chooseClub.subtitle')}</p>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <div className="actions actions-stack">
          {clubChoices.map(choice => (
            <button
              key={choice.organizationId}
              type="button"
              className="primary"
              disabled={isSubmitting}
              onClick={() => void handleChooseClub(choice.organizationId)}
            >
              {choice.name}
            </button>
          ))}
          <button type="button" disabled={isSubmitting} onClick={() => setClubChoices(null)}>
            {t('auth.chooseClub.back')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.club.title')}</h1>
      <p className="muted">{t('auth.club.subtitle')}</p>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label={t('auth.field.login')} htmlFor="staff-login">
          <input
            id="staff-login"
            name="login"
            type="text"
            autoComplete="username"
            value={login}
            onChange={event => setLogin(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.field.password')} htmlFor="staff-password">
          <input
            id="staff-password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <button type="submit" className="primary" disabled={isSubmitting}>
          {isSubmitting ? t('auth.action.signingIn') : t('auth.action.signIn')}
        </button>
      </form>
      <button type="button" className="linklike" onClick={onOpenAcceptInvite}>
        {t('auth.haveCode')}
      </button>
    </div>
  );
}

function projectSignInError(cause: unknown, t: (key: 'auth.error.invalid' | 'auth.error.generic') => string): string {
  if (cause instanceof PlatformApiError) {
    return cause.status === 401 ? t('auth.error.invalid') : cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return t('auth.error.generic');
}
```

(Note: `t`'s parameter type is `MessageKey`; the narrowed union in `projectSignInError` is only for readability — if tsc objects, type the param as `(key: MessageKey) => string` and import `MessageKey` from `../i18n/messages`.)

- [ ] **Step 4: Wire the two StaffSignIn render sites + nav helpers in `App.tsx`**

In `src/AFK4.Platform.Web/src/App.tsx`, add two nav helpers next to `navigateToClubInstall` (~line 195):

```typescript
  const navigateToClubDashboard = useCallback(
    () => navigate({ kind: 'clubDashboard' }, '/club'),
    [navigate]
  );

  const navigateToAcceptInvite = useCallback(
    () => navigate({ kind: 'acceptInvite', code: null }, '/auth/accept-invite'),
    [navigate]
  );
```

Update the explicit `staffSignIn` route render (currently lines 223-231) to:

```typescript
  if (route.kind === 'staffSignIn') {
    return (
      <StaffSignIn
        client={staffClient}
        onSignedIn={navigateToClubDashboard}
        onOpenAcceptInvite={navigateToAcceptInvite}
      />
    );
  }
```

Update the unauthenticated club-route render (currently lines 238-246) to:

```typescript
    if (staffSession === null) {
      return (
        <StaffSignIn
          client={staffClient}
          onSignedIn={navigateToClubDashboard}
          onOpenAcceptInvite={navigateToAcceptInvite}
        />
      );
    }
```

(The `staffSignIn` route still carries an unused `tenantKey` field — leave the `AuthRoute` type and `resolvePlatformRoute` as-is; `StaffSignIn` simply no longer reads it. No test references the prop.)

- [ ] **Step 5: Add the `linklike` and `actions-stack` styles (if absent)**

Check `src/AFK4.Platform.Web/src/styles.css` for `.linklike` and `.actions-stack`. If missing, append:

```css
.linklike {
  background: none;
  border: none;
  padding: 0;
  margin-top: 1rem;
  color: var(--accent, #4f46e5);
  cursor: pointer;
  text-decoration: underline;
  font: inherit;
}

.actions-stack {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  align-items: stretch;
}
```

- [ ] **Step 6: Run build + tests**

Run (from `src/AFK4.Platform.Web`): `npm run build` then `npm test`
Expected: tsc PASS; all tests PASS. If `App.test.tsx` references `StaffSignIn` with `initialTenantKey`, update those references to the new props (`onOpenAcceptInvite`).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Web/src/i18n/messages.ts src/AFK4.Platform.Web/src/components/StaffSignIn.tsx src/AFK4.Platform.Web/src/App.tsx src/AFK4.Platform.Web/src/styles.css
git commit -m "feat(platform-web): login-only club sign-in with club picker, land on dashboard, localized"
```

---

## Task 5: Frontend — localize SignIn + AcceptInvite (drop display-name) + ReservedAuthPage

**Files:**
- Modify: `src/AFK4.Platform.Web/src/components/SignIn.tsx`
- Modify: `src/AFK4.Platform.Web/src/components/AcceptInvite.tsx`
- Modify: `src/AFK4.Platform.Web/src/App.tsx` (`ReservedAuthPage`)

- [ ] **Step 1: Localize the admin `SignIn.tsx`**

In `src/AFK4.Platform.Web/src/components/SignIn.tsx`: add `import { useI18n } from '../i18n/I18nProvider';`, call `const { t } = useI18n();` at the top of the component, and replace the hardcoded strings:
- `<h1>Platform Control Plane</h1>` → `<h1>{t('auth.admin.title')}</h1>`
- `<p className="muted">Sign in with your platform admin credentials.</p>` → `{t('auth.admin.subtitle')}`
- `Field label="User name"` → `label={t('auth.field.login')}`
- `Field label="Password"` → `label={t('auth.field.password')}`
- button text `'Signing in…' : 'Sign in'` → `t('auth.action.signingIn') : t('auth.action.signIn')`
- error `'Wrong user name or password.'` → `t('auth.error.invalid')`; the non-401 `cause.message` stays; the final fallback `'Sign-in failed.'` → `t('auth.error.generic')`

- [ ] **Step 2: Localize + simplify `AcceptInvite.tsx` (drop display-name field)**

In `src/AFK4.Platform.Web/src/components/AcceptInvite.tsx`:
- Add `import { useI18n } from '../i18n/I18nProvider';` and `const { t } = useI18n();`.
- **Remove** the `displayName` state (`const [displayName, setDisplayName] = useState('');`) and its `<Field label="Display name">` block entirely.
- In `handleSubmit`, drop the `displayName` checks; send `displayName: ''` to the client (backend derives it):

```typescript
    if (normalizedUserName.length === 0) {
      setError(t('auth.accept.error.loginRequired'));
      return;
    }
    if (password.length < 8) {
      setError(t('auth.accept.error.passwordLength'));
      return;
    }
    if (password !== confirmPassword) {
      setError(t('auth.accept.error.passwordMismatch'));
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await client.acceptInvite({
        code: normalizedCode,
        userName: normalizedUserName,
        displayName: '',
        password
      });
      onAccepted();
    } catch (cause) {
      setError(projectAcceptInviteError(cause, t));
    } finally {
      setSubmitting(false);
    }
```

  (Remove the `normalizedDisplayName` const too.)
- Replace the remaining hardcoded strings with keys: title `auth.accept.title`, subtitle `auth.accept.subtitle`, code label `auth.accept.field.code`, username `<Field label>` → `auth.field.login` (also change the `<input>` `id`/`name` and types as you like — keep `autoComplete="username"`), password `auth.field.password`, confirm `auth.accept.field.confirmPassword`, submit `auth.accept.action.submit`/`auth.accept.action.submitting`, the "Sign in instead" button → `auth.accept.action.signInInstead`, and the empty-code guard → `auth.accept.error.codeRequired`.
- Update `projectAcceptInviteError` to take `t` and return localized strings:

```typescript
function projectAcceptInviteError(
  cause: unknown,
  t: (key: 'auth.accept.error.codeNotFound' | 'auth.accept.error.loginTaken' | 'auth.accept.error.generic') => string
): string {
  if (cause instanceof PlatformApiError) {
    if (cause.status === 404) {
      return t('auth.accept.error.codeNotFound');
    }
    if (cause.status === 409) {
      return t('auth.accept.error.loginTaken');
    }
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return t('auth.accept.error.generic');
}
```

  (If tsc complains about the narrowed `t` signature, type the param as `(key: MessageKey) => string` importing `MessageKey` from `../i18n/messages`.)

- [ ] **Step 3: Localize `ReservedAuthPage` (honest "coming soon")**

In `src/AFK4.Platform.Web/src/App.tsx`, replace the `ReservedAuthPage` component (lines 819-829) with:

```typescript
function ReservedAuthPage({ onSignIn }: { onSignIn: () => void }) {
  const { t } = useI18n();
  return (
    <div className="page page-narrow">
      <h1>{t('auth.reset.title')}</h1>
      <section className="section">
        <p className="muted">{t('auth.reset.message')}</p>
        <button type="button" className="primary" onClick={onSignIn}>{t('auth.reset.back')}</button>
      </section>
    </div>
  );
}
```

(`useI18n` is already imported in `App.tsx`.)

- [ ] **Step 4: Run build + tests**

Run (from `src/AFK4.Platform.Web`): `npm run build` then `npm test`
Expected: tsc PASS; tests PASS. If `staffAuthApi.test.ts`'s `acceptInvite` test sent a `displayName`, that still works (client passes through whatever the caller sends — only the component drops the field).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/components/SignIn.tsx src/AFK4.Platform.Web/src/components/AcceptInvite.tsx src/AFK4.Platform.Web/src/App.tsx
git commit -m "feat(platform-web): localize admin/accept-invite/reset auth screens, drop display-name field"
```

---

## Task 6: Frontend — slugify util + auto-slug in NewTenantScreen

**Files:**
- Create: `src/AFK4.Platform.Web/src/lib/slugify.ts`
- Create: `src/AFK4.Platform.Web/src/lib/slugify.test.ts`
- Modify: `src/AFK4.Platform.Web/src/platform/tenants/NewTenantScreen.tsx`

- [ ] **Step 1: Write the failing slugify tests**

Create `src/AFK4.Platform.Web/src/lib/slugify.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { slugify } from './slugify';

describe('slugify', () => {
  it('lowercases and hyphenates latin input', () => {
    expect(slugify('AFK4 Demo Club')).toBe('afk4-demo-club');
  });

  it('transliterates cyrillic to latin', () => {
    expect(slugify('AFK4 Душанбе')).toBe('afk4-dushanbe');
  });

  it('collapses repeats and trims edge hyphens', () => {
    expect(slugify('  Привет,  Мир!! ')).toBe('privet-mir');
  });

  it('returns empty string when nothing maps', () => {
    expect(slugify('——')).toBe('');
  });
});
```

- [ ] **Step 2: Run — verify fail**

Run (from `src/AFK4.Platform.Web`): `npm test -- slugify`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement `slugify.ts`**

Create `src/AFK4.Platform.Web/src/lib/slugify.ts`:

```typescript
const CYRILLIC_TO_LATIN: Record<string, string> = {
  а: 'a', б: 'b', в: 'v', г: 'g', д: 'd', е: 'e', ё: 'e', ж: 'zh',
  з: 'z', и: 'i', й: 'y', к: 'k', л: 'l', м: 'm', н: 'n', о: 'o',
  п: 'p', р: 'r', с: 's', т: 't', у: 'u', ф: 'f', х: 'h', ц: 'ts',
  ч: 'ch', ш: 'sh', щ: 'sch', ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu',
  я: 'ya'
};

/**
 * Produces a `[a-z0-9]` slug with single hyphens between segments, matching the
 * backend SlugValidator pattern. Cyrillic is transliterated to latin; any other
 * non-alphanumeric run becomes a single hyphen. May return '' (caller validates).
 */
export function slugify(input: string): string {
  const lower = input.trim().toLowerCase();
  let out = '';
  for (const char of lower) {
    if (Object.prototype.hasOwnProperty.call(CYRILLIC_TO_LATIN, char)) {
      out += CYRILLIC_TO_LATIN[char];
    } else if (/[a-z0-9]/.test(char)) {
      out += char;
    } else {
      out += '-';
    }
  }
  return out.replace(/-+/g, '-').replace(/^-+|-+$/g, '');
}
```

- [ ] **Step 4: Run — verify pass**

Run (from `src/AFK4.Platform.Web`): `npm test -- slugify`
Expected: PASS (4 tests).

- [ ] **Step 5: Auto-slug in `NewTenantScreen.tsx`**

In `src/AFK4.Platform.Web/src/platform/tenants/NewTenantScreen.tsx`:

Add the import:

```typescript
import { slugify } from '@/lib/slugify';
```

Add a `slugTouched` state next to the existing `form` state (after line 56):

```typescript
  const [slugTouched, setSlugTouched] = useState(false);
```

Replace the organization-name `LabeledInput` (line 102-103) so typing the name auto-fills the slug until the admin edits the slug manually:

```typescript
          <LabeledInput label={t('platform.newTenant.field.orgName')}
            value={form.organizationName} onChange={v => setForm(current => ({
              ...current,
              organizationName: v,
              organizationSlug: slugTouched ? current.organizationSlug : slugify(v)
            }))} required />
```

Replace the organization-slug `LabeledInput` (line 100-101) so manual edits set `slugTouched`:

```typescript
          <LabeledInput label={t('platform.newTenant.field.orgSlug')} hint={t('platform.newTenant.field.orgSlugHint')}
            value={form.organizationSlug} onChange={v => { setSlugTouched(true); update('organizationSlug', v); }} required />
```

(The slug field keeps the org-name auto-fill as a live, editable preview; once the admin edits it, auto-fill stops.)

- [ ] **Step 6: Add a NewTenant auto-slug test**

Find the existing NewTenant test (`src/AFK4.Platform.Web/src/platform/tenants/NewTenantScreen.test.tsx` if present; otherwise create it mirroring another screen test's render harness). Add:

```typescript
  it('auto-fills the organization slug from the name until the slug is edited', async () => {
    // render NewTenantScreen with a stub client, type into the org-name input,
    // assert the org-slug input value === 'afk4-dushanbe', then edit the slug
    // directly and assert further name edits no longer overwrite it.
  });
```

Implement it concretely using the same Testing Library helpers the neighbouring tests use (query the inputs by their `aria-label` — `t('platform.newTenant.field.orgName')` / `t('platform.newTenant.field.orgSlug')`; `fireEvent.change`). If no test harness exists for this screen, it is acceptable to rely on the `slugify` unit tests plus a manual check and skip this step — note that explicitly in the commit body.

- [ ] **Step 7: Run build + tests**

Run (from `src/AFK4.Platform.Web`): `npm run build` then `npm test`
Expected: tsc PASS; tests PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Web/src/lib src/AFK4.Platform.Web/src/platform/tenants/NewTenantScreen.tsx
git commit -m "feat(platform-web): auto-generate tenant slug from name (ru->latin), editable"
```

---

## Final verification

- [ ] **Backend full suite**

Run: `& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal`
Expected: all green (existing + new tests).

- [ ] **Frontend gates**

Run (from `src/AFK4.Platform.Web`): `npm run build` then `npm test`
Expected: tsc clean; all green.

- [ ] **Manual seam check (optional, with the Docker smoke stack)**

Club sign-in shows only Логин + Пароль (no Club key), lands on the dashboard; "Впервые? У меня есть код" opens the accept-invite screen; admin sign-in and accept-invite render in Russian; `/auth/forgot-password` shows the localized "Скоро" page; creating a tenant auto-fills the slug from the name.

---

## Notes for the executor

- **No DB migration** is introduced. The `(OrganizationId, NormalizedUserName)` unique index stays; model B resolves the club without it.
- The native Operator App and the existing `/api/auth/staff/sign-in` + `/sign-in-by-tenant-key` endpoints are unchanged.
- i18n: every key added to `ru` MUST also be added to `en` (parity test).
- Frontend tests use Vitest with `globals:false` — always `import { describe, it, expect, vi } from 'vitest'`.
- If `t()`'s narrowed parameter unions cause tsc friction, widen them to `MessageKey` (`import type { MessageKey } from '../i18n/messages'`).
