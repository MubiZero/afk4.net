# AFK4 Phase 2 Identity, Tenancy, RBAC, And Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the first production-shaped identity, tenancy, permission, and audit baseline so privileged backend actions are no longer anonymous or cross-tenant by default.

**Architecture:** Keep the backend as the existing ASP.NET Core modular monolith. Add identity, tenancy, and audit as explicit backend modules with EF Core persistence, opaque staff access tokens, predefined role-to-permission mapping, branch-scoped authorization helpers, and immutable audit records. Protect the existing device enrollment-code creation endpoint first because it is the current production-risky operation.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, EF Core InMemory for tests, xUnit, WPF + MVVM for Operator token storage.

---

## Scope

This plan implements the Phase 2 baseline required before device enrollment can be treated as production-safe:

- organizations and branches persisted in PostgreSQL;
- staff users persisted with password hashes;
- predefined MVP roles mapped to explicit permissions;
- staff sign-in returning opaque bearer tokens;
- refresh token rotation for long-lived Operator App sessions;
- request-time staff context resolution from bearer tokens;
- tenant and branch checks for privileged branch operations;
- audit records for successful and denied privileged actions;
- Operator App token storage abstraction with Windows-protected storage.

This plan intentionally does not implement a web admin panel, custom role CRUD,
organization onboarding UI, production SSO, external identity providers, or
full audit search/report UX.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\superpowers\plans\2026-05-12-afk4-phase2-identity-tenancy-rbac-audit.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  src\AFK4.Shared.Contracts\
    Identity\StaffSignInRequest.cs
    Identity\StaffSignInResponse.cs
    Identity\StaffPermissionNames.cs
  src\AFK4.Platform.Api\
    Audit\AuditActionNames.cs
    Audit\AuditOutcome.cs
    Audit\AuditRecordWriter.cs
    Audit\IAuditRecordWriter.cs
    Data\AuditRecordEntity.cs
    Data\BranchEntity.cs
    Data\OrganizationEntity.cs
    Data\StaffAccessTokenEntity.cs
    Data\StaffRoleAssignmentEntity.cs
    Data\StaffUserEntity.cs
    Data\Migrations\<timestamp>_AddIdentityTenancyAndAudit.cs
    Identity\OpaqueStaffTokenService.cs
    Identity\PasswordHashingStaffCredentialService.cs
    Identity\PermissionCatalog.cs
    Identity\StaffAuthenticationMiddleware.cs
    Identity\StaffAuthorizationResult.cs
    Identity\StaffAuthorizationService.cs
    Identity\StaffContext.cs
    Identity\StaffContextAccessor.cs
    Identity\StaffPermissionNames.cs
    Identity\StaffRoleNames.cs
    Identity\IStaffCredentialService.cs
    Identity\IStaffTokenService.cs
    Tenancy\BranchResolver.cs
    Tenancy\IBranchResolver.cs
  src\AFK4.Operator.App\
    Auth\IOperatorTokenStore.cs
    Auth\ProtectedDataOperatorTokenStore.cs
    Auth\OperatorTokenSnapshot.cs
  tests\AFK4.Shared.Contracts.Tests\
    StaffAuthContractSerializationTests.cs
  tests\AFK4.Platform.Api.Tests\
    StaffAuthenticationEndpointTests.cs
    DeviceEnrollmentAuthorizationTests.cs
    AuditRecordWriterTests.cs
  tests\AFK4.Operator.App.Tests\
    OperatorTokenStoreTests.cs
```

Responsibilities:

- `AFK4.Shared.Contracts.Identity`: transport DTOs for staff sign-in and permission names consumed by backend and Operator App.
- `AFK4.Platform.Api.Identity`: password verification, token issuance/validation, staff request context, predefined roles, and permission checks.
- `AFK4.Platform.Api.Tenancy`: organization/branch lookup and branch ownership validation.
- `AFK4.Platform.Api.Audit`: append-only audit writer used by modules through an explicit contract.
- `AFK4.Platform.Api.Data`: EF Core entities and model configuration for Phase 2 tables.
- `AFK4.Operator.App.Auth`: local token persistence only. The Operator App remains a client and is not the authority for permissions.

## Task 1: Shared Staff Auth Contracts

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffSignInRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffSignInResponse.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\StaffAuthContractSerializationTests.cs`

- [ ] **Step 1: Write the failing contract serialization test**

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Shared.Contracts.Tests;

public sealed class StaffAuthContractSerializationTests
{
    [Fact]
    public void StaffSignInResponse_RoundTripsPermissionsAndBranches()
    {
        var response = new StaffSignInResponse(
            StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            DisplayName: "Tech One",
            AccessToken: "token",
            AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
            BranchIds: [Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2")],
            Permissions: [StaffPermissionNames.CreateDeviceEnrollmentCode]);

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<StaffSignInResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.StaffUserId, copy.StaffUserId);
        Assert.Equal(response.OrganizationId, copy.OrganizationId);
        Assert.Contains(StaffPermissionNames.CreateDeviceEnrollmentCode, copy.Permissions);
        Assert.Single(copy.BranchIds);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter StaffAuthContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
error CS0234: The type or namespace name 'Identity' does not exist
```

- [ ] **Step 3: Implement the shared contracts**

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInRequest(
    Guid OrganizationId,
    string UserName,
    string Password);
```

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInResponse(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> Permissions);
```

```csharp
namespace AFK4.Shared.Contracts.Identity;

public static class StaffPermissionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";
    public const string DispatchDeviceCommand = "devices.commands.dispatch";
    public const string ViewDeviceCommandStatus = "devices.commands.status.view";
    public const string ViewFloorMap = "floor_map.view";
    public const string ManageRoles = "identity.roles.manage";
    public const string ViewAudit = "audit.view";
}
```

- [ ] **Step 4: Run the contract test and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter StaffAuthContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed!  - Failed:     0
```

## Task 2: Identity, Tenancy, And Audit Persistence Model

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\OrganizationEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\BranchEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\StaffUserEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\StaffRoleAssignmentEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\StaffAccessTokenEntity.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\AuditRecordEntity.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\AuditRecordWriterTests.cs`

- [ ] **Step 1: Write the failing audit persistence test**

```csharp
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class AuditRecordWriterTests
{
    [Fact]
    public async Task WriteAsync_AppendsAuditRecordWithoutMutatingExistingRows()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var now = DateTimeOffset.Parse("2026-05-12T00:00:00Z");

        await using var dbContext = new PlatformDbContext(options);
        var writer = new AuditRecordWriter(dbContext, TimeProvider.System);

        await writer.WriteAsync(new AuditRecordWriteRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            ActorStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            Action: AuditActionNames.CreateDeviceEnrollmentCode,
            TargetType: "DeviceEnrollmentCode",
            TargetId: "AFK4-TEST-CODE",
            Outcome: AuditOutcome.Succeeded,
            SourceApp: "PlatformApi",
            DetailsJson: """{"expiresInSeconds":300}"""),
            CancellationToken.None);

        var record = await dbContext.AuditRecords.SingleAsync();

        Assert.Equal(AuditActionNames.CreateDeviceEnrollmentCode, record.Action);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
        Assert.Equal("AFK4-TEST-CODE", record.TargetId);
        Assert.Equal("""{"expiresInSeconds":300}""", record.DetailsJson);
        Assert.True(record.CreatedAtUtc > now);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter AuditRecordWriterTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
error CS0234: The type or namespace name 'Audit' does not exist
```

- [ ] **Step 3: Implement EF entities and model configuration**

Create the entities with these required properties:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class OrganizationEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class BranchEntity
{
    public Guid BranchId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class StaffUserEntity
{
    public Guid StaffUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string NormalizedUserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class StaffRoleAssignmentEntity
{
    public Guid StaffRoleAssignmentId { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class StaffAccessTokenEntity
{
    public Guid StaffAccessTokenId { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class AuditRecordEntity
{
    public Guid AuditRecordId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ActorStaffUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? TargetId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string SourceApp { get; set; } = string.Empty;
    public string DetailsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

Add DbSets and model configuration in `PlatformDbContext`:

```csharp
public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
public DbSet<BranchEntity> Branches => Set<BranchEntity>();
public DbSet<StaffUserEntity> StaffUsers => Set<StaffUserEntity>();
public DbSet<StaffRoleAssignmentEntity> StaffRoleAssignments => Set<StaffRoleAssignmentEntity>();
public DbSet<StaffAccessTokenEntity> StaffAccessTokens => Set<StaffAccessTokenEntity>();
public DbSet<AuditRecordEntity> AuditRecords => Set<AuditRecordEntity>();
```

Use these table names: `organizations`, `branches`, `staff_users`, `staff_role_assignments`, `staff_access_tokens`, `audit_records`. Add indexes for organization/branch lookups, staff username uniqueness per organization, active token hash lookup, and audit organization/branch/time queries.

- [ ] **Step 4: Implement audit writer**

```csharp
namespace AFK4.Platform.Api.Audit;

public static class AuditActionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";
}
```

```csharp
namespace AFK4.Platform.Api.Audit;

public static class AuditOutcome
{
    public const string Succeeded = "Succeeded";
    public const string Denied = "Denied";
}
```

```csharp
namespace AFK4.Platform.Api.Audit;

public sealed record AuditRecordWriteRequest(
    Guid OrganizationId,
    Guid? BranchId,
    Guid? ActorStaffUserId,
    string Action,
    string TargetType,
    string? TargetId,
    string Outcome,
    string SourceApp,
    string DetailsJson);
```

```csharp
namespace AFK4.Platform.Api.Audit;

public interface IAuditRecordWriter
{
    Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken);
}
```

```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Audit;

public sealed class AuditRecordWriter(PlatformDbContext dbContext, TimeProvider timeProvider) : IAuditRecordWriter
{
    public async Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
    {
        dbContext.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            ActorStaffUserId = request.ActorStaffUserId,
            Action = request.Action,
            TargetType = request.TargetType,
            TargetId = request.TargetId,
            Outcome = request.Outcome,
            SourceApp = request.SourceApp,
            DetailsJson = request.DetailsJson,
            CreatedAtUtc = timeProvider.GetUtcNow()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Run the audit persistence test and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter AuditRecordWriterTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed!  - Failed:     0
```

## Task 3: Staff Sign-In And Opaque Access Tokens

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\IStaffCredentialService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\IStaffTokenService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\OpaqueStaffTokenService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PasswordHashingStaffCredentialService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\StaffRoleNames.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\StaffAuthenticationEndpointTests.cs`

- [ ] **Step 1: Write failing staff sign-in endpoint test**

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffAuthenticationEndpointTests
{
    [Fact]
    public async Task PostStaffSignIn_WithValidCredentials_ReturnsAccessTokenAndPermissions()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(
                OrganizationId: TestIds.OrganizationId,
                UserName: "tech@afk4.test",
                Password: "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.Equal("Tech One", body.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Contains(TestIds.BranchId, body.BranchIds);
        Assert.Contains(StaffPermissionNames.CreateDeviceEnrollmentCode, body.Permissions);
    }

    private static async Task SeedTechnicianAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var user = new StaffUserEntity
        {
            StaffUserId = TestIds.TechnicianStaffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = "tech@afk4.test",
            NormalizedUserName = "TECH@AFK4.TEST",
            DisplayName = "Tech One",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        };
        user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");

        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Org",
            CreatedAtUtc = user.CreatedAtUtc
        });
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = user.CreatedAtUtc
        });
        dbContext.StaffUsers.Add(user);
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = user.OrganizationId,
            BranchId = TestIds.BranchId,
            RoleName = StaffRoleNames.Technician
        });
        await dbContext.SaveChangesAsync();
    }
}
```

Add this helper once in `tests\AFK4.Platform.Api.Tests\TestIds.cs`:

```csharp
namespace AFK4.Platform.Api.Tests;

internal static class TestIds
{
    public static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    public static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    public static readonly Guid TechnicianStaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffAuthenticationEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Assert.Equal() Failure
Expected: OK
Actual:   NotFound
```

- [ ] **Step 3: Implement sign-in services**

Use `PasswordHasher<StaffUserEntity>` to verify passwords. On valid sign-in, create a cryptographically random opaque token shaped as `<tokenId:N>.<secret>`, store a SHA-256 hash of the full token in `staff_access_tokens`, and return the full token only once in `StaffSignInResponse`.

Required service signatures:

```csharp
namespace AFK4.Platform.Api.Identity;

public interface IStaffCredentialService
{
    Task<StaffSignInResponse?> SignInAsync(StaffSignInRequest request, CancellationToken cancellationToken);
}
```

```csharp
namespace AFK4.Platform.Api.Identity;

public interface IStaffTokenService
{
    Task<StaffContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);
    Task<StaffSignInResponse> IssueAsync(StaffUserEntity user, CancellationToken cancellationToken);
}
```

Define the MVP role constants:

```csharp
namespace AFK4.Platform.Api.Identity;

public static class StaffRoleNames
{
    public const string Owner = "owner";
    public const string BranchManager = "branch_manager";
    public const string ShiftSupervisor = "shift_supervisor";
    public const string CashierOperator = "cashier_operator";
    public const string Technician = "technician";
    public const string AccountantAuditor = "accountant_auditor";
}
```

Define explicit role-to-permission mapping in `PermissionCatalog` and make `Technician`, `BranchManager`, and `Owner` include `devices.enrollment_codes.create`.

- [ ] **Step 4: Wire sign-in endpoint and services in `Program.cs`**

Add scoped services:

```csharp
builder.Services.AddScoped<IStaffTokenService, OpaqueStaffTokenService>();
builder.Services.AddScoped<IStaffCredentialService, PasswordHashingStaffCredentialService>();
```

Map:

```csharp
app.MapPost("/api/auth/staff/sign-in", async (
    StaffSignInRequest request,
    IStaffCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var response = await credentialService.SignInAsync(request, cancellationToken);

    return response is null
        ? Results.Unauthorized()
        : Results.Ok(response);
});
```

- [ ] **Step 5: Run sign-in tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffAuthenticationEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed!  - Failed:     0
```

## Task 4: Staff Context, Branch Authorization, And Protected Enrollment Code Creation

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\StaffAuthenticationMiddleware.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\StaffAuthorizationResult.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\StaffAuthorizationService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\StaffContext.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Identity\StaffContextAccessor.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Tenancy\BranchResolver.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Tenancy\IBranchResolver.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\DeviceEnrollmentAuthorizationTests.cs`

- [ ] **Step 1: Write failing endpoint authorization tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceEnrollmentAuthorizationTests
{
    [Fact]
    public async Task PostDeviceEnrollmentCode_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceEnrollmentCode_WithTechnicianPermission_CreatesCodeAndAuditRecord()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, StaffRoleNames.Technician);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client));

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));
        var code = await response.Content.ReadFromJsonAsync<DeviceEnrollmentCodeDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(code);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.CreateDeviceEnrollmentCode, audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
        Assert.Equal(code.Code, audit.TargetId);
    }

    [Fact]
    public async Task PostDeviceEnrollmentCode_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffAsync(factory, StaffRoleNames.CashierOperator);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SignInAsync(client));

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(TestIds.OrganizationId, ExpiresInSeconds: 300));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
        Assert.Equal(TestIds.TechnicianStaffUserId, audit.ActorStaffUserId);
    }

    private static async Task<string> SignInAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        return body.AccessToken;
    }

    private static async Task SeedStaffAsync(PlatformApiFactory factory, string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var user = new StaffUserEntity
        {
            StaffUserId = TestIds.TechnicianStaffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = "tech@afk4.test",
            NormalizedUserName = "TECH@AFK4.TEST",
            DisplayName = "Tech One",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        };
        user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");

        dbContext.Organizations.Add(new OrganizationEntity { OrganizationId = TestIds.OrganizationId, Name = "Demo Org", CreatedAtUtc = user.CreatedAtUtc });
        dbContext.Branches.Add(new BranchEntity { BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Name = "Demo Branch", CreatedAtUtc = user.CreatedAtUtc });
        dbContext.StaffUsers.Add(user);
        dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = user.StaffUserId,
            OrganizationId = user.OrganizationId,
            BranchId = TestIds.BranchId,
            RoleName = roleName
        });
        await dbContext.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceEnrollmentAuthorizationTests --no-restore -p:UseSharedCompilation=false
```

Expected before protection exists:

```text
Assert.Equal() Failure
Expected: Unauthorized
Actual:   OK
```

- [ ] **Step 3: Implement staff context and authorization service**

Required types:

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed record StaffContext(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<string> Permissions);
```

```csharp
namespace AFK4.Platform.Api.Identity;

public interface IStaffContextAccessor
{
    StaffContext? Current { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed class StaffContextAccessor : IStaffContextAccessor
{
    public StaffContext? Current { get; set; }
}
```

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed record StaffAuthorizationResult(
    bool IsAllowed,
    bool IsAuthenticated,
    StaffContext? StaffContext,
    string? DenialReason)
{
    public static StaffAuthorizationResult Unauthenticated() => new(false, false, null, "Authentication is required.");
    public static StaffAuthorizationResult Denied(StaffContext context, string reason) => new(false, true, context, reason);
    public static StaffAuthorizationResult Allowed(StaffContext context) => new(true, true, context, null);
}
```

The authorization service must allow only when:

- a staff context exists;
- the staff context organization matches the branch organization;
- the staff context includes the route branch;
- the staff context includes the required permission.

- [ ] **Step 4: Implement authentication middleware**

The middleware reads `Authorization: Bearer <token>`, calls `IStaffTokenService.ValidateAsync`, and stores a `StaffContext` in `IStaffContextAccessor`. Missing or invalid tokens leave `Current` as `null`; endpoints decide whether authentication is required.

- [ ] **Step 5: Protect device enrollment-code creation endpoint**

In `Program.cs`, register:

```csharp
builder.Services.AddScoped<IStaffContextAccessor, StaffContextAccessor>();
builder.Services.AddScoped<StaffAuthorizationService>();
builder.Services.AddScoped<IBranchResolver, BranchResolver>();
builder.Services.AddScoped<IAuditRecordWriter, AuditRecordWriter>();
```

Add middleware before endpoint mapping:

```csharp
app.UseMiddleware<StaffAuthenticationMiddleware>();
```

Update `POST /api/branches/{branchId}/device-enrollment-codes` to:

- return `401` when no staff token is present;
- return `400` when route/request organization mismatch is detected;
- return `403` for authenticated staff without branch permission and write a denied audit record;
- create the code, then write a succeeded audit record with the created enrollment code as `TargetId`.

- [ ] **Step 6: Run authorization tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceEnrollmentAuthorizationTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed!  - Failed:     0
```

## Task 5: EF Core Migration And PostgreSQL Smoke Coverage

**Files:**

- Create: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\<timestamp>_AddIdentityTenancyAndAudit.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Data\Migrations\PlatformDbContextModelSnapshot.cs`
- Modify: `D:\afk4.net\docs\operations\local-postgres-smoke.md`

- [ ] **Step 1: Create the EF migration**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddIdentityTenancyAndAudit --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

Expected:

```text
Done. To undo this action, use 'ef migrations remove'
```

- [ ] **Step 2: Review generated migration**

Verify the migration creates:

- `organizations`;
- `branches`;
- `staff_users`;
- `staff_role_assignments`;
- `staff_access_tokens`;
- `audit_records`.

Verify there are indexes for:

- branch by organization;
- staff username per organization;
- role assignments by staff and branch;
- access token hash;
- audit organization/branch/timestamp.

- [ ] **Step 3: Update local PostgreSQL runbook**

Add a staff-auth smoke section that seeds a test staff user through a short SQL block or a later seed command, signs in through `/api/auth/staff/sign-in`, and uses the returned bearer token to create a device enrollment code.

- [ ] **Step 4: Run migration-aware tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
Passed!  - Failed:     0
```

## Task 6: Operator App Token Storage Baseline

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Auth\IOperatorTokenStore.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Auth\OperatorTokenSnapshot.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Auth\ProtectedDataOperatorTokenStore.cs`
- Create: `D:\afk4.net\tests\AFK4.Operator.App.Tests\OperatorTokenStoreTests.cs`

- [ ] **Step 1: Write failing token store test**

```csharp
using AFK4.Operator.App.Auth;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorTokenStoreTests
{
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsStoredTokenSnapshot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afk4-token-{Guid.NewGuid():N}.bin");
        var store = new ProtectedDataOperatorTokenStore(path);
        var snapshot = new OperatorTokenSnapshot(
            AccessToken: "access-token",
            AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"));

        await store.SaveAsync(snapshot, CancellationToken.None);
        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Equal(snapshot.AccessToken, loaded?.AccessToken);
        Assert.Equal(snapshot.OrganizationId, loaded?.OrganizationId);

        File.Delete(path);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter OperatorTokenStoreTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
error CS0234: The type or namespace name 'Auth' does not exist
```

- [ ] **Step 3: Implement token store**

Use JSON serialization plus Windows DPAPI `ProtectedData.Protect` and `ProtectedData.Unprotect` with `DataProtectionScope.CurrentUser`. Keep the interface asynchronous because the store performs file I/O:

```csharp
namespace AFK4.Operator.App.Auth;

public interface IOperatorTokenStore
{
    Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken);
    Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Run Operator token store test and verify GREEN**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter OperatorTokenStoreTests --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed!  - Failed:     0
```

## Task 7: Full Verification, Progress Log, And Commit

**Files:**

- Modify: `D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md`
- Modify: `D:\afk4.net\README.md` if exposed endpoints or smoke flow changed.

- [ ] **Step 1: Run full build**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 2: Run full test suite**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 3: Update progress log**

Add a Phase 2 section with:

- implemented identity/tenancy/RBAC/audit baseline items;
- protected endpoints;
- migration name;
- latest verification commands and results;
- known limitations, including staff management UI, custom roles, and audit search.

- [ ] **Step 4: Commit coherent Phase 2 baseline**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' status --short
& 'C:\Program Files\Git\cmd\git.exe' add docs src tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add identity tenancy rbac audit baseline"
```

Expected:

```text
[codex/phase2-identity-tenancy-rbac-audit ...] feat: add identity tenancy rbac audit baseline
```

## Plan Self-Review

Spec coverage:

- PRD Phase 2 staff authentication is covered by Tasks 1 and 3.
- Organizations and branches are covered by Task 2.
- Permission-based RBAC with predefined MVP roles is covered by Task 3.
- Tenant-aware branch authorization is covered by Task 4.
- Cross-tenant rejection is covered by Task 4 branch ownership checks.
- Audit for privileged actions is covered by Tasks 2 and 4.
- Operator token storage is covered by Task 6.
- PostgreSQL persistence is covered by Task 5.

Deferred with explicit reasons:

- Staff management screens and role CRUD are deferred because the MVP has no web admin panel and Operator App role management needs its own workflow design.
- Audit search and reports are deferred to the reports/audit review roadmap slice.
- Protecting every future operator-facing endpoint remains a continuing rule as
  endpoints are added. The current Phase 2 slice protects device enrollment-code
  creation plus device command dispatch/status because those are the existing
  privileged staff API paths.

Placeholder scan:

- No `TBD`, `TODO`, or open-ended implementation markers remain.
- Every task has concrete file paths, test names, commands, and expected results.

Type consistency:

- Contract permission name `StaffPermissionNames.CreateDeviceEnrollmentCode` matches backend permission checks.
- Audit action `AuditActionNames.CreateDeviceEnrollmentCode` matches protected endpoint audit writes.
- `StaffRoleNames.Technician` maps to the enrollment-code permission required by Task 4.
