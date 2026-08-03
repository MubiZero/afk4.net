using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Pulse;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class PulseContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PlatformPulseDto_RoundTripsOrganizationsAndClubs()
    {
        var pulse = new PlatformPulseDto(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-08-03T08:00:00Z"),
            Organizations:
            [
                new PulseOrganizationDto(
                    OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
                    Name: "Demo Club",
                    Status: "active",
                    PlanCode: "starter",
                    SubscriptionStatus: "trial",
                    AlertLevel: PulseAlertLevelNames.Critical,
                    OutstandingMinorUnits: 50_000,
                    CurrencyCode: "TJS",
                    Alerts:
                    [
                        new PulseAlertDto(PulseAlertKindNames.PaymentOverdue, PulseAlertLevelNames.Attention, "10 days overdue")
                    ],
                    Clubs:
                    [
                        new PulseClubDto(
                            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
                            Name: "Main Branch",
                            City: "Dushanbe",
                            DevicesOnline: 0,
                            DevicesTotal: 1,
                            SeatsOccupied: 0,
                            SeatsTotal: 1,
                            ShiftOpen: false,
                            ShiftOpenedAtUtc: null,
                            LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-08-03T06:00:00Z"),
                            Alerts:
                            [
                                new PulseAlertDto(PulseAlertKindNames.AgentSilent, PulseAlertLevelNames.Critical, "120 minutes ago")
                            ])
                    ])
            ]);

        var json = JsonSerializer.Serialize(pulse, Options);
        var copy = JsonSerializer.Deserialize<PlatformPulseDto>(json, Options);

        Assert.NotNull(copy);
        Assert.Single(copy.Organizations);
        Assert.Single(copy.Organizations[0].Clubs);
        Assert.Equal(PulseAlertKindNames.AgentSilent, copy.Organizations[0].Clubs[0].Alerts[0].Kind);

        Assert.Contains("\"organizations\"", json);
        Assert.Contains("\"clubs\"", json);
        Assert.Contains("\"devicesOnline\"", json);
        Assert.Contains("\"seatsOccupied\"", json);
        Assert.Contains("\"alerts\"", json);
        Assert.Contains("\"alertLevel\"", json);
    }
}
