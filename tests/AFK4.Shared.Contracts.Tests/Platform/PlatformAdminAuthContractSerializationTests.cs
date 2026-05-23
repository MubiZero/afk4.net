using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class PlatformAdminAuthContractSerializationTests
{
    [Fact]
    public void PlatformAdminSignInRequest_RoundTripsThroughJson()
    {
        var request = new PlatformAdminSignInRequest("admin@afk4.local", "ChangeMe!");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<PlatformAdminSignInRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.UserName, copy.UserName);
        Assert.Equal(request.Password, copy.Password);
    }

    [Fact]
    public void PlatformAdminSignInResponse_RoundTripsRolesAndPermissions()
    {
        var response = new PlatformAdminSignInResponse(
            PlatformAdminId: Guid.Parse("11111111-2222-3333-4444-555555555555"),
            UserName: "admin@afk4.local",
            DisplayName: "Local Platform Admin",
            AccessToken: "opaque-access",
            AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-23T18:00:00Z"),
            RefreshToken: "opaque-refresh",
            RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-22T18:00:00Z"),
            Roles: [PlatformAdminRoleNames.PlatformOwner],
            Permissions: [
                PlatformAdminPermissionNames.ViewTenants,
                PlatformAdminPermissionNames.CreateTenant
            ]);

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<PlatformAdminSignInResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.PlatformAdminId, copy.PlatformAdminId);
        Assert.Equal(response.UserName, copy.UserName);
        Assert.Equal(response.AccessToken, copy.AccessToken);
        Assert.Equal(response.RefreshTokenExpiresAtUtc, copy.RefreshTokenExpiresAtUtc);
        Assert.Equal(PlatformAdminRoleNames.PlatformOwner, copy.Roles.Single());
        Assert.Contains(PlatformAdminPermissionNames.ViewTenants, copy.Permissions);
        Assert.Contains(PlatformAdminPermissionNames.CreateTenant, copy.Permissions);
    }

    [Fact]
    public void PlatformAdminRefreshTokenRequest_RoundTripsThroughJson()
    {
        var request = new PlatformAdminRefreshTokenRequest("opaque-refresh-token");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<PlatformAdminRefreshTokenRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.RefreshToken, copy.RefreshToken);
    }

    [Fact]
    public void PlatformAdminSignOutRequest_RoundTripsThroughJson()
    {
        var request = new PlatformAdminSignOutRequest("opaque-refresh-token");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<PlatformAdminSignOutRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.RefreshToken, copy.RefreshToken);
    }
}
