using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminAuthenticationEndpointTests
{
    // Sign-in is now two steps: password proves identity and returns a challenge, the challenge
    // is redeemed at /2fa/verify for the working session. Tests below that only care about what
    // happens AFTER a session exists (refresh, sign-out, cross-boundary checks) go through both
    // steps via this helper; PostPlatformAdminSignIn_WithValidCredentials_... exercises the two
    // steps directly since it is testing that exact contract.
    private static async Task<PlatformAdminSignInResponse> SignInAndVerifyAsync(
        HttpClient client,
        string userName = PlatformAdminTestHelper.DefaultUserName,
        string password = PlatformAdminTestHelper.DefaultPassword)
    {
        var signInResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(userName, password));
        var challenge = await signInResponse.Content.ReadFromJsonAsync<PlatformAdminSignInChallengeResponse>();
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);
        Assert.NotNull(challenge);

        var code = TotpCodeGenerator.Generate(PlatformAdminTestHelper.DefaultTotpSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var verifyResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/2fa/verify",
            new { ChallengeToken = challenge!.ChallengeToken, Code = code });
        var body = await verifyResponse.Content.ReadFromJsonAsync<PlatformAdminSignInResponse>();
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        Assert.NotNull(body);
        return body!;
    }

    [Fact]
    public async Task PostPlatformAdminSignIn_WithValidCredentials_ReturnsAccessTokenAndPermissions()
    {
        await using var factory = new PlatformApiFactory();
        var admin = await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        using var client = factory.CreateClient();

        var body = await SignInAndVerifyAsync(client);
        Assert.Equal(admin.PlatformAdminUserId, body.PlatformAdminId);
        Assert.Equal(admin.UserName, body.UserName);
        Assert.Equal(admin.DisplayName, body.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.True(body.RefreshTokenExpiresAtUtc > body.AccessTokenExpiresAtUtc);
        Assert.Contains(PlatformAdminRoleNames.PlatformAdmin, body.Roles);
        Assert.Contains(PlatformAdminPermissionNames.ViewOrganizations, body.Permissions);
        Assert.Contains(PlatformAdminPermissionNames.CreateOrganization, body.Permissions);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var auditRecords = await dbContext.AuditRecords
            .Where(record => record.Action == "identity.platform_admin.sign_in")
            .ToListAsync();
        Assert.Single(auditRecords);
        Assert.Equal("Succeeded", auditRecords[0].Outcome);
        Assert.Equal(Guid.Empty, auditRecords[0].OrganizationId);
    }

    [Fact]
    public async Task PostPlatformAdminSignIn_WithWrongPassword_ReturnsUnauthorizedAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(PlatformAdminTestHelper.DefaultUserName, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var auditRecords = await dbContext.AuditRecords
            .Where(record => record.Action == "identity.platform_admin.sign_in")
            .ToListAsync();
        Assert.Single(auditRecords);
        Assert.Equal("Denied", auditRecords[0].Outcome);
    }

    [Fact]
    public async Task PostPlatformAdminSignIn_WithUnknownUserName_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest("ghost@nowhere", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPlatformAdminSignIn_WithInactiveAdmin_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, isActive: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(PlatformAdminTestHelper.DefaultUserName, PlatformAdminTestHelper.DefaultPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPlatformAdminRefresh_WithValidRefreshToken_RotatesTokenAndRejectsOriginalRefreshToken()
    {
        await using var factory = new PlatformApiFactory();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        using var client = factory.CreateClient();

        var signInBody = await SignInAndVerifyAsync(client);

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/refresh",
            new PlatformAdminRefreshTokenRequest(signInBody.RefreshToken));
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<PlatformAdminSignInResponse>();

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotNull(refreshBody);
        Assert.NotEqual(signInBody.AccessToken, refreshBody.AccessToken);
        Assert.NotEqual(signInBody.RefreshToken, refreshBody.RefreshToken);
        Assert.Equal(signInBody.PlatformAdminId, refreshBody.PlatformAdminId);
        Assert.Contains(PlatformAdminPermissionNames.ViewOrganizations, refreshBody.Permissions);

        var replayResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/refresh",
            new PlatformAdminRefreshTokenRequest(signInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    [Fact]
    public async Task PostPlatformAdminRefresh_WithExpiredRefreshToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        var admin = await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        using var client = factory.CreateClient();

        var signInBody = await SignInAndVerifyAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var refreshToken = await dbContext.PlatformAdminRefreshTokens
                .SingleAsync(token => token.PlatformAdminUserId == admin.PlatformAdminUserId);
            refreshToken.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();
        }

        var refreshResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/refresh",
            new PlatformAdminRefreshTokenRequest(signInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task PostPlatformAdminSignOut_WhenAuthenticated_RevokesTokensAndPreventsReuse()
    {
        await using var factory = new PlatformApiFactory();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        using var client = factory.CreateClient();

        var signInBody = await SignInAndVerifyAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signInBody.AccessToken);

        var signOutResponse = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-out",
            new PlatformAdminSignOutRequest(signInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);

        var refreshAfterSignOut = await client.PostAsJsonAsync(
            "/api/platform/auth/refresh",
            new PlatformAdminRefreshTokenRequest(signInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterSignOut.StatusCode);
    }

    [Fact]
    public async Task PostPlatformAdminSignOut_WhenUnauthenticated_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-out",
            new PlatformAdminSignOutRequest("non-existent-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StaffRefreshToken_CannotBeUsedAtPlatformAdminRefreshEndpoint()
    {
        await using var factory = new PlatformApiFactory();
        using (var seedClient = factory.CreateClient())
        {
            await StaffAuthTestHelper.AuthorizeAsAsync(factory, seedClient, AFK4.Platform.Api.Identity.OrganizationRoleNames.OrganizationOwner);
        }

        using var staffClient = factory.CreateClient();
        var staffSignIn = await staffClient.PostAsJsonAsync(
            $"/api/organizations/{TestIds.OrganizationId:D}/auth/staff/sign-in",
            new StaffSignInRequest(TestIds.OrganizationId, "tech@afk4.test", "Passw0rd!"));
        var staffSignInBody = await staffSignIn.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(staffSignInBody);

        var response = await staffClient.PostAsJsonAsync(
            "/api/platform/auth/refresh",
            new PlatformAdminRefreshTokenRequest(staffSignInBody.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PlatformAdminAccessToken_CannotReachStaffBranchEndpoints()
    {
        await using var factory = new PlatformApiFactory();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        using var client = factory.CreateClient();

        var signInBody = await SignInAndVerifyAsync(client);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signInBody.AccessToken);

        var floorMapResponse = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/floor-map");

        Assert.Equal(HttpStatusCode.Unauthorized, floorMapResponse.StatusCode);
    }
}
