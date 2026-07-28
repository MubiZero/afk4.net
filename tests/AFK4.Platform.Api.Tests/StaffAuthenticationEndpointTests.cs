using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
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
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.True(body.RefreshTokenExpiresAtUtc > body.AccessTokenExpiresAtUtc);
        Assert.Contains(TestIds.BranchId, body.BranchIds);
        Assert.Contains(OrganizationPermissionNames.CreateDeviceEnrollmentCode, body.Permissions);
    }

    [Fact]
    public async Task PostStaffSignInByTenantKey_WithValidTenantSlug_ReturnsAccessTokenAndPermissions()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-tenant-key",
            new StaffSignInByTenantKeyRequest(
                TenantKey: "Demo-Club",
                UserName: "tech@afk4.test",
                Password: "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.Equal("Tech One", body.DisplayName);
        Assert.Contains(TestIds.BranchId, body.BranchIds);
        Assert.Contains(OrganizationPermissionNames.CreateDeviceEnrollmentCode, body.Permissions);
    }

    [Fact]
    public async Task PostStaffSignInByTenantKey_WithOrganizationGuid_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-tenant-key",
            new StaffSignInByTenantKeyRequest(
                TenantKey: TestIds.OrganizationId.ToString("D"),
                UserName: "tech@afk4.test",
                Password: "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostStaffRefresh_WithValidRefreshToken_RotatesTokenAndRejectsOriginalRefreshToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var signInResponse = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(
                OrganizationId: TestIds.OrganizationId,
                UserName: "tech@afk4.test",
                Password: "Passw0rd!"));
        var signInBody = await signInResponse.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.NotNull(signInBody);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/staff/refresh",
            new StaffRefreshTokenRequest(TestIds.OrganizationId, signInBody.RefreshToken));
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshBody);
        Assert.NotEqual(signInBody.AccessToken, refreshBody.AccessToken);
        Assert.NotEqual(signInBody.RefreshToken, refreshBody.RefreshToken);
        Assert.Equal(signInBody.StaffUserId, refreshBody.StaffUserId);
        Assert.Contains(OrganizationPermissionNames.CreateDeviceEnrollmentCode, refreshBody.Permissions);
        Assert.Equal(
            [OrganizationRoleNames.Operator, OrganizationRoleNames.ShiftSupervisor, OrganizationRoleNames.Technician],
            signInBody.RoleNames);
        Assert.Equal(signInBody.RoleNames, refreshBody.RoleNames);

        var replayResponse = await client.PostAsJsonAsync(
            "/api/auth/staff/refresh",
            new StaffRefreshTokenRequest(TestIds.OrganizationId, signInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

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
        Assert.Contains(OrganizationPermissionNames.CreateDeviceEnrollmentCode, body.Permissions);
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

    [Fact]
    public async Task PostStaffSignInByLogin_WithEmail_SingleClub_ReturnsAccessToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory); // creates org A
        await SeedEmailUserInOrgAAsync(factory, "owner-login", "owner@afk4.test", "Passw0rd!");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("owner@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task PostStaffSignInByLogin_EmailCollidesWithAnotherUsername_SignsInEmailOwner()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory); // org A
        // User X in org A: its USERNAME equals the string we'll log in with.
        await SeedEmailUserInOrgAAsync(factory, "shared@afk4.test", "x@afk4.test", "X-pass");
        // User Y in org A: its EMAIL equals that same string (different password).
        await SeedEmailUserInOrgAAsync(factory, "y-login", "shared@afk4.test", "Y-pass");
        using var client = factory.CreateClient();

        // Logging in with the email "shared@afk4.test" + Y's password must sign in Y,
        // not fail because X happens to own that string as a username.
        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("shared@afk4.test", "Y-pass"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task PostStaffSignIn_WithEmailInsteadOfUserName_ReturnsAccessToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        await SeedEmailUserInOrgAAsync(factory, "owner-login", "owner@afk4.test", "Passw0rd!");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "owner@afk4.test", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task PostStaffSignInByLogin_WithEmailWrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        await SeedEmailUserInOrgAAsync(factory, "owner-login", "owner@afk4.test", "Passw0rd!");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("owner@afk4.test", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostStaffSignInByLogin_WithUnknownEmail_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest("ghost@afk4.test", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostStaffSignInByLogin_SameEmailTwoClubsSamePassword_ReturnsChooseClub()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        await SeedEmailUserInOrgAAsync(factory, "owner-a", "shared@afk4.test", "Same-pass");
        await SeedEmailUserInSecondOrgAsync(factory, "owner-b", "shared@afk4.test", "Same-pass");
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
            RoleName = OrganizationRoleNames.OrganizationOwner
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
            RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await dbContext.SaveChangesAsync();
    }

    // Adds a user to org A whose UserName differs from its Email, so an email login
    // must resolve via the email branch (not the username branch).
    private static async Task SeedEmailUserInOrgAAsync(
        PlatformApiFactory factory, string userName, string email, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var user = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            DisplayName = "Email User",
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
            RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await dbContext.SaveChangesAsync();
    }

    // Creates org B and adds an email user there (username differs from email).
    private static async Task SeedEmailUserInSecondOrgAsync(
        PlatformApiFactory factory, string userName, string email, string password)
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
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            DisplayName = "Email User B",
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
            RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedTechnicianAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var createdAt = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
        var user = new StaffUserEntity
        {
            StaffUserId = TestIds.TechnicianStaffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = "tech@afk4.test",
            NormalizedUserName = "TECH@AFK4.TEST",
            DisplayName = "Tech One",
            IsActive = true,
            CreatedAtUtc = createdAt
        };
        user.PasswordHash = hasher.HashPassword(user, "Passw0rd!");

        dbContext.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Slug = "demo-club",
            Name = "Demo Org",
            CreatedAtUtc = createdAt
        });
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Slug = "main",
            Name = "Demo Branch",
            CreatedAtUtc = createdAt
        });
        dbContext.StaffUsers.Add(user);
        dbContext.StaffRoleAssignments.AddRange(
            new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = user.StaffUserId,
                OrganizationId = user.OrganizationId,
                BranchId = TestIds.BranchId,
                RoleName = OrganizationRoleNames.Technician
            },
            new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = user.StaffUserId,
                OrganizationId = user.OrganizationId,
                BranchId = TestIds.BranchId,
                RoleName = OrganizationRoleNames.ShiftSupervisor
            },
            new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = user.StaffUserId,
                OrganizationId = user.OrganizationId,
                BranchId = TestIds.BranchId,
                RoleName = OrganizationRoleNames.Operator
            });
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task PostStaffSignIn_WithVerifiedPhoneAsUserName_ReturnsAccessToken()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        await SeedPhoneUserInOrgAAsync(factory, "992937380070", verified: true, password: "Passw0rd!");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "992937380070", "Passw0rd!"));
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(TestIds.OrganizationId, body!.OrganizationId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task PostStaffSignIn_WithUnverifiedPhone_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedTechnicianAsync(factory);
        await SeedPhoneUserInOrgAAsync(factory, "992937380071", verified: false, password: "Passw0rd!");
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "992937380071", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Adds a phone staff user to org A whose UserName is NOT the phone, so a phone login must
    // resolve via the phone branch (not username/email). `verified` toggles PhoneVerifiedAtUtc.
    private static async Task SeedPhoneUserInOrgAAsync(
        PlatformApiFactory factory, string normalizedPhone, bool verified, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<StaffUserEntity>();
        var user = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            UserName = "phone-staff",
            NormalizedUserName = "PHONE-STAFF",
            DisplayName = "Phone Staff",
            IsActive = true,
            Phone = "+" + normalizedPhone,
            NormalizedPhone = normalizedPhone,
            PhoneVerifiedAtUtc = verified ? DateTimeOffset.Parse("2026-06-01T00:00:00Z") : null,
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
            RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await dbContext.SaveChangesAsync();
    }
}
