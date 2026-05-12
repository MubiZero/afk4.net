using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceCredentialLifecycleContractSerializationTests
{
    [Fact]
    public void RotateDeviceCredentialResponse_RoundTripsThroughJson()
    {
        var response = new RotateDeviceCredentialResponse(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CredentialId: Guid.Parse("4e083a8a-2669-48be-86b5-e44285012f8c"),
            CredentialSecret: "rotated-secret",
            RotatedAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"));

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<RotateDeviceCredentialResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.DeviceId, copy.DeviceId);
        Assert.Equal(response.CredentialId, copy.CredentialId);
        Assert.Equal("rotated-secret", copy.CredentialSecret);
    }

    [Fact]
    public void RevokeDeviceCredentialResponse_RoundTripsThroughJson()
    {
        var response = new RevokeDeviceCredentialResponse(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CredentialId: Guid.Parse("4e083a8a-2669-48be-86b5-e44285012f8c"),
            RevokedAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"));

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<RevokeDeviceCredentialResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.DeviceId, copy.DeviceId);
        Assert.Equal(response.CredentialId, copy.CredentialId);
        Assert.Equal(response.RevokedAtUtc, copy.RevokedAtUtc);
    }
}
