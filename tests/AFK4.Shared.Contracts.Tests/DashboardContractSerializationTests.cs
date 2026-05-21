using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Dashboard;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DashboardContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OperatorDashboardSummaryDto_RoundTrips()
    {
        var summary = new OperatorDashboardSummaryDto(
            OrganizationId: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            FromUtc: DateTimeOffset.Parse("2026-05-21T00:00:00Z"),
            ToUtc: DateTimeOffset.Parse("2026-05-21T23:59:59Z"),
            GeneratedAtUtc: DateTimeOffset.Parse("2026-05-21T12:00:00Z"),
            Shift: new OperatorDashboardShiftSummaryDto(
                ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
                State: "open",
                OpenedAtUtc: DateTimeOffset.Parse("2026-05-21T08:00:00Z"),
                OpenedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                ExpectedCash: new MoneyDto("TJS", 52400)),
            Revenue: new OperatorDashboardRevenueSummaryDto(
                PosNetSales: new MoneyDto("TJS", 2400),
                GameplayRevenue: new MoneyDto("TJS", 12000),
                TotalRevenue: new MoneyDto("TJS", 14400),
                PosCheckCount: 1,
                NewPlayerCount: 2),
            Utilization: new OperatorDashboardUtilizationSummaryDto(
                TotalSeats: 24,
                ActiveSessions: 9,
                EndingSessions: 1,
                OnlineDevices: 20,
                OfflineDevices: 4,
                SessionStarts: 12,
                UtilizationPercent: 38),
            AlertPressure: new OperatorDashboardAlertPressureDto(
                PendingCommands: 1,
                FailedCommands: 1,
                OfflineDevices: 4,
                EndingSessions: 1,
                TotalAlerts: 7),
            Reservations: new OperatorDashboardReservationSummaryDto(
                ActiveReservations: 0,
                AvailableSlots: 14,
                Source: "floor-map-availability"),
            FocusQueue:
            [
                new OperatorDashboardQueueItemDto(
                    Tone: "blocking",
                    Target: "PC-11",
                    Title: "lock Failed",
                    Detail: "Device command needs operator attention.",
                    SeatId: Guid.Parse("cccccccc-0000-0000-0000-000000000001"),
                    DeviceId: Guid.Parse("dddddddd-0000-0000-0000-000000000001"),
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-21T10:00:00Z"),
                    SourceType: "device-command")
            ],
            RecentPayments:
            [
                new OperatorDashboardRecentPaymentDto(
                    PaymentId: Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"),
                    PosSaleId: Guid.Parse("ffffffff-0000-0000-0000-000000000001"),
                    ShiftId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
                    CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                    PaymentKind: "payment",
                    PaymentMethod: "cash",
                    Amount: new MoneyDto("TJS", 2400),
                    CreatedAtUtc: DateTimeOffset.Parse("2026-05-21T10:03:00Z"))
            ]);

        var copy = JsonSerializer.Deserialize<OperatorDashboardSummaryDto>(
            JsonSerializer.Serialize(summary, Options),
            Options);

        Assert.NotNull(copy);
        Assert.Equal(14400, copy.Revenue.TotalRevenue.MinorUnits);
        Assert.Equal(7, copy.AlertPressure.TotalAlerts);
        Assert.Equal("PC-11", Assert.Single(copy.FocusQueue).Target);
        Assert.Equal(2400, Assert.Single(copy.RecentPayments).Amount.MinorUnits);
    }
}
