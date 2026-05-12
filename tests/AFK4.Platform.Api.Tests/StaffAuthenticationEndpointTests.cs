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
        Assert.Contains(StaffPermissionNames.CreateDeviceEnrollmentCode, body.Permissions);
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
        Assert.Contains(StaffPermissionNames.CreateDeviceEnrollmentCode, refreshBody.Permissions);

        var replayResponse = await client.PostAsJsonAsync(
            "/api/auth/staff/refresh",
            new StaffRefreshTokenRequest(TestIds.OrganizationId, signInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
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
            Name = "Demo Org",
            CreatedAtUtc = createdAt
        });
        dbContext.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = createdAt
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
