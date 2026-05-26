using System.Text.Json;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Shared.Contracts.Tests;

public sealed class InstallContractSerializationTests
{
    [Fact]
    public void InstallDiscover_RoundTripsOwnerCodeAndBranchFloorMap()
    {
        var request = new InstallDiscoverRequest("12345678");
        var seatId = Guid.Parse("a1fdf945-6ce1-49ee-8d1e-4b4798c9508b");
        var response = new InstallDiscoverResponse(
            "Owner One",
            [
                new InstallBranchDto(
                    Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                    "main",
                    "Main branch",
                    new FloorMapDto(
                        Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                        "Main branch",
                        [
                            new SeatStatusDto(
                                seatId,
                                "PC-01",
                                Guid.Parse("c6ccf73f-dc8d-4147-bd5b-445143fc90dc"),
                                "Main hall",
                                1,
                                "Maintenance",
                                DeviceId: null,
                                DeviceName: null,
                                IsDeviceOnline: null,
                                IsDeviceLocked: null,
                                LastHeartbeatAtUtc: null,
                                AgentVersion: null,
                                ShellVersion: null,
                                ActiveSessionId: null,
                                RemainingSeconds: null)
                        ]),
                    [seatId])
            ]);

        var requestCopy = JsonSerializer.Deserialize<InstallDiscoverRequest>(JsonSerializer.Serialize(request));
        var responseCopy = JsonSerializer.Deserialize<InstallDiscoverResponse>(JsonSerializer.Serialize(response));

        Assert.NotNull(requestCopy);
        Assert.NotNull(responseCopy);
        Assert.Equal("12345678", requestCopy.OwnerCode);
        Assert.Equal("Owner One", responseCopy.OwnerDisplayName);
        Assert.Equal(seatId, responseCopy.Branches.Single().FreeSeatIds.Single());
    }

    [Fact]
    public void InstallEnroll_RoundTripsDeviceBootstrap()
    {
        var request = new InstallEnrollRequest(
            "12345678",
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Guid.Parse("a1fdf945-6ce1-49ee-8d1e-4b4798c9508b"),
            DeviceRoleNames.GamingPc,
            "PC-01",
            "PC-01",
            "device-public-key");
        var response = new InstallEnrollResponse(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            request.BranchId,
            Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            Guid.Parse("8ab96fb8-b15b-46ea-b6c1-99098a266409"),
            "device-secret",
            DeviceEnrollmentStateNames.Approved,
            "https://api.afk4.test",
            UpdateChannelNames.Internal,
            DateTimeOffset.Parse("2026-05-25T10:00:00Z"))
        {
            LeaseSigningPublicKeyPem = "lease-public-key",
            UpdatePackageSigningPublicKeyPem = "update-public-key"
        };

        var requestCopy = JsonSerializer.Deserialize<InstallEnrollRequest>(JsonSerializer.Serialize(request));
        var responseCopy = JsonSerializer.Deserialize<InstallEnrollResponse>(JsonSerializer.Serialize(response));

        Assert.NotNull(requestCopy);
        Assert.NotNull(responseCopy);
        Assert.Equal(DeviceRoleNames.GamingPc, requestCopy.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, responseCopy.EnrollmentState);
        Assert.Equal(UpdateChannelNames.Internal, responseCopy.UpdateChannel);
        Assert.Equal("device-secret", responseCopy.CredentialSecret);
        Assert.Equal("lease-public-key", responseCopy.LeaseSigningPublicKeyPem);
        Assert.Equal("update-public-key", responseCopy.UpdatePackageSigningPublicKeyPem);
    }

    [Fact]
    public void InstallEnroll_AllowsManagerWorkstationWithoutSeat()
    {
        var request = new InstallEnrollRequest(
            "12345678",
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            null,
            DeviceRoleNames.ManagerWorkstation,
            "Manager desk",
            "MANAGER-01",
            "device-public-key");

        var copy = JsonSerializer.Deserialize<InstallEnrollRequest>(JsonSerializer.Serialize(request));

        Assert.NotNull(copy);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, copy.Role);
        Assert.Null(copy.SeatId);
    }

    [Fact]
    public void InstallCreateSeat_RoundTripsOwnerCodeScopedSeat()
    {
        var request = new InstallCreateSeatRequest(
            "12345678",
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Guid.Parse("c6ccf73f-dc8d-4147-bd5b-445143fc90dc"),
            "PC-NEW");
        var response = new InstallCreateSeatResponse(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            request.BranchId,
            request.ZoneId,
            Guid.Parse("a1fdf945-6ce1-49ee-8d1e-4b4798c9508b"),
            "PC-NEW",
            SortOrder: 5);

        var requestCopy = JsonSerializer.Deserialize<InstallCreateSeatRequest>(JsonSerializer.Serialize(request));
        var responseCopy = JsonSerializer.Deserialize<InstallCreateSeatResponse>(JsonSerializer.Serialize(response));

        Assert.NotNull(requestCopy);
        Assert.NotNull(responseCopy);
        Assert.Equal("12345678", requestCopy.OwnerCode);
        Assert.Equal(request.BranchId, responseCopy.BranchId);
        Assert.Equal(request.ZoneId, responseCopy.ZoneId);
        Assert.Equal("PC-NEW", responseCopy.Name);
        Assert.Equal(5, responseCopy.SortOrder);
    }
}
