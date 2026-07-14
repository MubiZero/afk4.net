using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void SettlePosSaleRequest_RoundTripsPaymentParts()
    {
        var request = new SettlePosSaleRequest(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            [
                new PaymentPartDto("wallet", new MoneyDto("TJS", 4_000)),
                new PaymentPartDto("cash", new MoneyDto("TJS", 6_000))
            ],
            "operator POS checkout",
            "pos-settle-1");

        var copy = JsonSerializer.Deserialize<SettlePosSaleRequest>(JsonSerializer.Serialize(request))!;

        Assert.Equal(2, copy.Payments.Count);
        Assert.Equal(10_000, copy.Payments.Sum(part => part.Amount.MinorUnits));
    }

    [Fact]
    public void DeviceHeartbeatRequest_RoundTripsThroughJson()
    {
        var request = new DeviceHeartbeatRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            IsLocked: true,
            ActiveSessionId: null,
            ActiveSessionLeaseExpiresAtUtc: null,
            ActiveSessionLeaseSequence: null);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<DeviceHeartbeatRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.True(copy.IsLocked);
    }

    [Fact]
    public void FloorMapDto_ContainsSeatStatuses()
    {
        var seat = new SeatStatusDto(
            SeatId: Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414"),
            SeatName: "PC-001",
            ZoneId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            ZoneName: "Main Hall",
            SortOrder: 10,
            State: "Locked",
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            DeviceName: "PC-001",
            IsDeviceOnline: true,
            IsDeviceLocked: true,
            LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-12T00:02:00Z"),
            AgentVersion: "0.1.1",
            ShellVersion: "0.1.2",
            ActiveSessionId: Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"),
            RemainingSeconds: null,
            AccruedCostMinorUnits: 2250,
            CurrencyCode: "TJS",
            SessionVersion: 3,
            PlayerDisplayName: "Иван Петров",
            TariffName: "VIP час",
            SessionStartedAtUtc: DateTimeOffset.Parse("2026-05-11T23:20:00Z"));

        var map = new FloorMapDto(
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            BranchName: "Demo Branch",
            Seats: [seat])
        {
            Zones =
            [
                new FloorMapZoneDto(
                    ZoneId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
                    Name: "Main Hall",
                    SortOrder: 1)
            ]
        };
        var copy = JsonSerializer.Deserialize<FloorMapDto>(JsonSerializer.Serialize(map));

        Assert.NotNull(copy);
        Assert.Single(copy.Zones);
        Assert.Equal("Main Hall", copy.Zones[0].Name);
        Assert.Single(copy.Seats);
        Assert.Equal("Locked", copy.Seats[0].State);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"), copy.Seats[0].ZoneId);
        Assert.Equal(Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"), copy.Seats[0].DeviceId);
        Assert.True(copy.Seats[0].IsDeviceOnline);
        Assert.Equal("0.1.2", copy.Seats[0].ShellVersion);
        Assert.Equal(2250, copy.Seats[0].AccruedCostMinorUnits);
        Assert.Equal("TJS", copy.Seats[0].CurrencyCode);
        Assert.Equal(3, copy.Seats[0].SessionVersion);
        Assert.Equal("Иван Петров", copy.Seats[0].PlayerDisplayName);
        Assert.Equal("VIP час", copy.Seats[0].TariffName);
        Assert.Equal(DateTimeOffset.Parse("2026-05-11T23:20:00Z"), copy.Seats[0].SessionStartedAtUtc);
    }
}
