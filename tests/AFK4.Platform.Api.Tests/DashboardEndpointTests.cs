using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Dashboard;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class DashboardEndpointTests
{
    private static readonly DateTimeOffset DashboardDay = DateTimeOffset.Parse("2026-05-21T00:00:00Z");
    private static readonly Guid ShiftId = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private static readonly Guid SeatOneId = Guid.Parse("aaaaaaaa-1111-4111-8111-111111111111");
    private static readonly Guid SeatTwoId = Guid.Parse("aaaaaaaa-2222-4222-8222-222222222222");
    private static readonly Guid OfflineDeviceId = Guid.Parse("bbbbbbbb-2222-4222-8222-222222222222");
    private static readonly Guid SaleId = Guid.Parse("cccccccc-3333-4333-8333-333333333333");
    private static readonly Guid ActiveSessionId = Guid.Parse("dddddddd-4444-4444-8444-444444444444");
    private static readonly Guid EndingSessionId = Guid.Parse("eeeeeeee-5555-4555-8555-555555555555");

    [Fact]
    public async Task GetDashboardSummary_WithoutStaffToken_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/dashboard/summary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetDashboardSummary_WithCashierRole_ReturnsForbiddenAndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        var response = await client.GetAsync($"/api/branches/{TestIds.BranchId}/dashboard/summary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync();
        Assert.Equal(AuditActionNames.ViewDashboardSummary, audit.Action);
        Assert.Equal(AuditOutcome.Denied, audit.Outcome);
    }

    [Fact]
    public async Task GetDashboardSummary_WithManagerRole_ReturnsBackendMetricsAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.BranchManager);
        await SeedDashboardDataAsync(factory);

        var response = await client.GetAsync(
            $"/api/branches/{TestIds.BranchId}/dashboard/summary?fromUtc=2026-05-21T00:00:00Z&toUtc=2026-05-21T23:59:59Z&limit=3");
        var result = await response.Content.ReadFromJsonAsync<OperatorDashboardSummaryDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(ShiftId, result.Shift.ShiftId);
        Assert.Equal(2, result.Utilization.TotalSeats);
        Assert.Equal(1, result.Utilization.ActiveSessions);
        Assert.Equal(1, result.Utilization.EndingSessions);
        Assert.Equal(1, result.Utilization.OnlineDevices);
        Assert.Equal(1, result.Utilization.OfflineDevices);
        Assert.Equal(2, result.Utilization.SessionStarts);
        Assert.Equal(50, result.Utilization.UtilizationPercent);
        Assert.Equal(2_000, result.Revenue.PosNetSales.MinorUnits);
        Assert.Equal(12_000, result.Revenue.GameplayRevenue.MinorUnits);
        Assert.Equal(14_000, result.Revenue.TotalRevenue.MinorUnits);
        Assert.Equal(1, result.Revenue.PosCheckCount);
        Assert.Equal(1, result.Revenue.NewPlayerCount);
        Assert.Equal(1, result.AlertPressure.PendingCommands);
        Assert.Equal(1, result.AlertPressure.FailedCommands);
        Assert.Equal(1, result.AlertPressure.OfflineDevices);
        Assert.Equal(1, result.AlertPressure.EndingSessions);
        Assert.Equal(4, result.AlertPressure.TotalAlerts);
        Assert.Equal("reservation-contract", result.Reservations.Source);
        Assert.Equal(0, result.Reservations.AvailableSlots);
        Assert.Contains(result.FocusQueue, item => item.SourceType == "device-command" && item.Tone == "blocking");
        Assert.Contains(result.FocusQueue, item => item.SourceType == "device" && item.Target == "PC-02");
        Assert.Equal(2, result.RecentPayments.Count);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == AuditActionNames.ViewDashboardSummary);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Contains("\"TotalAlerts\":4", audit.DetailsJson);
    }

    private static async Task SeedDashboardDataAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        dbContext.Seats.AddRange(
            new SeatEntity
            {
                SeatId = SeatOneId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = Guid.NewGuid(),
                Name = "PC-01",
                SortOrder = 10,
                CreatedAtUtc = DashboardDay.AddHours(7)
            },
            new SeatEntity
            {
                SeatId = SeatTwoId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                ZoneId = Guid.NewGuid(),
                Name = "PC-02",
                SortOrder = 20,
                CreatedAtUtc = DashboardDay.AddHours(7)
            });
        dbContext.Devices.AddRange(
            new DeviceEntity
            {
                DeviceId = TestIds.DeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-01",
                AgentVersion = "0.1.14",
                ShellVersion = "0.1.14",
                EnrolledAtUtc = DashboardDay.AddHours(7),
                LastHeartbeatAtUtc = DashboardDay.AddHours(12),
                IsOnline = true,
                IsLocked = false
            },
            new DeviceEntity
            {
                DeviceId = OfflineDeviceId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                MachineName = "PC-02",
                AgentVersion = "0.1.14",
                ShellVersion = "0.1.14",
                EnrolledAtUtc = DashboardDay.AddHours(7),
                LastHeartbeatAtUtc = DashboardDay.AddHours(10),
                IsOnline = false,
                IsLocked = true
            });
        dbContext.DeviceSeatAssignments.AddRange(
            new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatOneId,
                DeviceId = TestIds.DeviceId,
                AttachedAtUtc = DashboardDay.AddHours(7),
                DetachedAtUtc = null
            },
            new DeviceSeatAssignmentEntity
            {
                DeviceSeatAssignmentId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatTwoId,
                DeviceId = OfflineDeviceId,
                AttachedAtUtc = DashboardDay.AddHours(7),
                DetachedAtUtc = null
            });
        dbContext.Shifts.Add(new ShiftEntity
        {
            ShiftId = ShiftId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            ClosedByStaffUserId = null,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50_000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 52_400,
            DifferenceMinorUnits = 0,
            OpeningNote = "morning",
            ClosingNote = string.Empty,
            OpenedAtUtc = DashboardDay.AddHours(8),
            ClosedAtUtc = null
        });
        dbContext.Sessions.AddRange(
            new SessionEntity
            {
                SessionId = ActiveSessionId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatOneId,
                DeviceId = TestIds.DeviceId,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                PlayerKind = "guest",
                PlayerAccountId = null,
                TariffRuleVersionId = "standard-v1",
                State = SessionStateNames.Active,
                RequestedAtUtc = DashboardDay.AddHours(9).AddMinutes(-1),
                StartedAtUtc = DashboardDay.AddHours(9),
                EndsAtUtc = DashboardDay.AddHours(11),
                EndedAtUtc = null,
                CurrentLeaseId = null,
                UpdatedAtUtc = DashboardDay.AddHours(10)
            },
            new SessionEntity
            {
                SessionId = EndingSessionId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = SeatTwoId,
                DeviceId = OfflineDeviceId,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                PlayerKind = "guest",
                PlayerAccountId = null,
                TariffRuleVersionId = "standard-v1",
                State = SessionStateNames.Ending,
                RequestedAtUtc = DashboardDay.AddHours(9).AddMinutes(59),
                StartedAtUtc = DashboardDay.AddHours(10),
                EndsAtUtc = DashboardDay.AddHours(12),
                EndedAtUtc = null,
                CurrentLeaseId = null,
                UpdatedAtUtc = DashboardDay.AddHours(12)
            });
        dbContext.DeviceCommands.AddRange(
            new DeviceCommandEntity
            {
                CommandId = Guid.NewGuid(),
                DeviceId = TestIds.DeviceId,
                Type = "unlock",
                PayloadJson = "{}",
                Status = "Pending",
                Message = null,
                CreatedAtUtc = DashboardDay.AddHours(10),
                UpdatedAtUtc = DashboardDay.AddHours(10)
            },
            new DeviceCommandEntity
            {
                CommandId = Guid.NewGuid(),
                DeviceId = OfflineDeviceId,
                Type = "lock",
                PayloadJson = "{}",
                Status = "Failed",
                Message = "Agent did not confirm lock.",
                CreatedAtUtc = DashboardDay.AddHours(11),
                UpdatedAtUtc = DashboardDay.AddHours(11)
            });
        dbContext.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = SaleId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = ShiftId,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = PosSaleStateNames.Paid,
            CurrencyCode = "TJS",
            TotalMinorUnits = 2_400,
            RefundReason = string.Empty,
            VoidReason = string.Empty,
            CreatedAtUtc = DashboardDay.AddHours(12),
            PaidAtUtc = DashboardDay.AddHours(12).AddMinutes(1),
            RefundedAtUtc = null,
            VoidedAtUtc = null
        });
        dbContext.Payments.AddRange(
            new PaymentEntity
            {
                PaymentId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                PosSaleId = SaleId,
                ShiftId = ShiftId,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                PaymentKind = "payment",
                Provider = "manual",
                PaymentMethod = PaymentMethodNames.Cash,
                CurrencyCode = "TJS",
                AmountMinorUnits = 2_400,
                Note = "sale",
                CreatedAtUtc = DashboardDay.AddHours(12)
            },
            new PaymentEntity
            {
                PaymentId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                PosSaleId = SaleId,
                ShiftId = ShiftId,
                CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
                PaymentKind = "refund",
                Provider = "manual",
                PaymentMethod = PaymentMethodNames.Cash,
                CurrencyCode = "TJS",
                AmountMinorUnits = -400,
                Note = "partial refund",
                CreatedAtUtc = DashboardDay.AddHours(13)
            });
        dbContext.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = ShiftId,
            PlayerAccountId = Guid.NewGuid(),
            SessionId = ActiveSessionId,
            PlayerPackageId = null,
            EntryType = LedgerEntryTypeNames.GameplayCharge,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = -12_000,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = LedgerEntryTypeNames.GameplayCharge,
            Reason = "gameplay",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = TestIds.TechnicianStaffUserId,
            CreatedAtUtc = DashboardDay.AddHours(10)
        });
        dbContext.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "New Player",
            PhoneNumber = null,
            IsActive = true,
            CreatedAtUtc = DashboardDay.AddHours(14)
        });

        await dbContext.SaveChangesAsync();
    }
}
