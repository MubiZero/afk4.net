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
            RefreshToken: "refresh-token",
            RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-11T01:00:00Z"),
            BranchIds: [Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2")],
            Permissions: [StaffPermissionNames.CreateDeviceEnrollmentCode]);

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<StaffSignInResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.StaffUserId, copy.StaffUserId);
        Assert.Equal(response.OrganizationId, copy.OrganizationId);
        Assert.Equal(response.RefreshToken, copy.RefreshToken);
        Assert.Equal(response.RefreshTokenExpiresAtUtc, copy.RefreshTokenExpiresAtUtc);
        Assert.Contains(StaffPermissionNames.CreateDeviceEnrollmentCode, copy.Permissions);
        Assert.Single(copy.BranchIds);
    }

    [Fact]
    public void StaffRefreshTokenRequest_RoundTripsThroughJson()
    {
        var request = new StaffRefreshTokenRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            RefreshToken: "refresh-token");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<StaffRefreshTokenRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationId, copy.OrganizationId);
        Assert.Equal(request.RefreshToken, copy.RefreshToken);
    }

    [Fact]
    public void UpdateStaffUserStateRequest_RoundTripsThroughJson()
    {
        var request = new UpdateStaffUserStateRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            IsActive: false);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateStaffUserStateRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationId, copy.OrganizationId);
        Assert.Equal(request.IsActive, copy.IsActive);
    }

    [Fact]
    public void ResetStaffUserPasswordRequest_RoundTripsThroughJson()
    {
        var request = new ResetStaffUserPasswordRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            NewPassword: "ChangeMe456!");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<ResetStaffUserPasswordRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationId, copy.OrganizationId);
        Assert.Equal(request.NewPassword, copy.NewPassword);
    }
}
