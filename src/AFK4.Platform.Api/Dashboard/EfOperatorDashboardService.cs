using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Dashboard;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Dashboard;

public sealed class EfOperatorDashboardService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IOperatorDashboardService
{
    private const string DefaultCurrencyCode = "TJS";
    private const string PaymentKindPayment = "payment";
    private const string PaymentKindRefund = "refund";
    private const int DefaultLimit = 8;
    private const int MaxLimit = 20;

    public async Task<OperatorDashboardSummaryDto> GetSummaryAsync(
        Guid organizationId,
        Guid branchId,
        DashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        var generatedAtUtc = timeProvider.GetUtcNow();
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc, generatedAtUtc);
        var limit = NormalizeLimit(query.Limit);

        var currentShift = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift =>
                shift.OrganizationId == organizationId &&
                shift.BranchId == branchId &&
                shift.State == ShiftStateNames.Open)
            .OrderByDescending(shift => shift.OpenedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        var seats = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.OrganizationId == organizationId && seat.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.OrganizationId == organizationId && device.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var deviceIds = devices.Select(device => device.DeviceId).ToHashSet();
        var sessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.OrganizationId == organizationId && session.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment =>
                payment.OrganizationId == organizationId &&
                payment.BranchId == branchId &&
                payment.CreatedAtUtc >= fromUtc &&
                payment.CreatedAtUtc <= toUtc)
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .ThenByDescending(payment => payment.PaymentId)
            .ToListAsync(cancellationToken);
        var ledgerEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.OrganizationId == organizationId &&
                entry.BranchId == branchId &&
                entry.CreatedAtUtc >= fromUtc &&
                entry.CreatedAtUtc <= toUtc &&
                (entry.EntryType == LedgerEntryTypeNames.GameplayCharge ||
                    entry.EntryType == LedgerEntryTypeNames.PostpaidDebt ||
                    entry.EntryType == LedgerEntryTypeNames.Refund))
            .ToListAsync(cancellationToken);
        IReadOnlyList<DeviceCommandEntity> commands = deviceIds.Count == 0
            ? []
            : await dbContext.DeviceCommands
                .AsNoTracking()
                .Where(command => deviceIds.Contains(command.DeviceId))
                .OrderByDescending(command => command.UpdatedAtUtc)
                .ThenByDescending(command => command.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        IReadOnlyList<DeviceSeatAssignmentEntity> assignments = deviceIds.Count == 0
            ? []
            : await dbContext.DeviceSeatAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.OrganizationId == organizationId &&
                    assignment.BranchId == branchId &&
                    assignment.DetachedAtUtc == null &&
                    deviceIds.Contains(assignment.DeviceId))
                .ToListAsync(cancellationToken);

        var activeSessionCount = sessions.Count(session => IsState(session.State, SessionStateNames.Active));
        var endingSessionCount = sessions.Count(session => IsState(session.State, SessionStateNames.Ending));
        var sessionStarts = sessions.Count(session =>
            session.StartedAtUtc is not null &&
            session.StartedAtUtc >= fromUtc &&
            session.StartedAtUtc <= toUtc);
        var totalSeats = seats.Count;
        var onlineDevices = devices.Count(device => device.IsOnline);
        var offlineDevices = devices.Count(device => !device.IsOnline);
        var pendingCommands = commands.Count(command => IsStatus(command.Status, "Pending"));
        var failedCommands = commands.Count(command => IsStatus(command.Status, "Failed") || IsStatus(command.Status, "Rejected"));
        var totalAlerts = pendingCommands + failedCommands + offlineDevices + endingSessionCount;
        var currencyCode = ResolveCurrencyCode(currentShift, payments, ledgerEntries);
        var posNetSalesMinorUnits = payments.Sum(payment => IsKind(payment.PaymentKind, PaymentKindPayment) || IsKind(payment.PaymentKind, PaymentKindRefund)
            ? payment.AmountMinorUnits
            : 0);
        var gameplayRevenueMinorUnits = ledgerEntries.Sum(entry => entry.EntryType switch
        {
            LedgerEntryTypeNames.GameplayCharge => Math.Abs(entry.AmountMinorUnits),
            LedgerEntryTypeNames.PostpaidDebt => Math.Max(0, entry.AmountMinorUnits),
            LedgerEntryTypeNames.Refund => -Math.Abs(entry.AmountMinorUnits),
            _ => 0
        });
        var paidSaleCount = await dbContext.PosSales
            .AsNoTracking()
            .CountAsync(sale =>
                sale.OrganizationId == organizationId &&
                sale.BranchId == branchId &&
                sale.State == PosSaleStateNames.Paid &&
                sale.PaidAtUtc >= fromUtc &&
                sale.PaidAtUtc <= toUtc,
                cancellationToken);
        var newPlayerCount = await dbContext.PlayerAccounts
            .AsNoTracking()
            .CountAsync(player =>
                player.OrganizationId == organizationId &&
                player.HomeBranchId == branchId &&
                player.CreatedAtUtc >= fromUtc &&
                player.CreatedAtUtc <= toUtc,
                cancellationToken);
        var utilizationPercent = totalSeats == 0
            ? 0
            : Math.Clamp((int)Math.Round(activeSessionCount * 100m / totalSeats), 0, 100);

        return new OperatorDashboardSummaryDto(
            organizationId,
            branchId,
            fromUtc,
            toUtc,
            generatedAtUtc,
            new OperatorDashboardShiftSummaryDto(
                currentShift?.ShiftId,
                currentShift?.State ?? "none",
                currentShift?.OpenedAtUtc,
                currentShift?.OpenedByStaffUserId,
                Money(currencyCode, currentShift?.ExpectedCashMinorUnits ?? 0)),
            new OperatorDashboardRevenueSummaryDto(
                Money(currencyCode, posNetSalesMinorUnits),
                Money(currencyCode, gameplayRevenueMinorUnits),
                Money(currencyCode, posNetSalesMinorUnits + gameplayRevenueMinorUnits),
                paidSaleCount,
                newPlayerCount),
            new OperatorDashboardUtilizationSummaryDto(
                totalSeats,
                activeSessionCount,
                endingSessionCount,
                onlineDevices,
                offlineDevices,
                sessionStarts,
                utilizationPercent),
            new OperatorDashboardAlertPressureDto(
                pendingCommands,
                failedCommands,
                offlineDevices,
                endingSessionCount,
                totalAlerts),
            new OperatorDashboardReservationSummaryDto(
                ActiveReservations: 0,
                AvailableSlots: Math.Max(0, totalSeats - activeSessionCount - endingSessionCount),
                Source: "floor-map-availability"),
            BuildFocusQueue(commands, devices, assignments, seats, sessions, limit),
            payments.Take(limit).Select(payment => new OperatorDashboardRecentPaymentDto(
                payment.PaymentId,
                payment.PosSaleId,
                payment.ShiftId,
                payment.CreatedByStaffUserId,
                payment.PaymentKind,
                payment.PaymentMethod,
                Money(payment.CurrencyCode, payment.AmountMinorUnits),
                payment.CreatedAtUtc)).ToList());
    }

    private static IReadOnlyList<OperatorDashboardQueueItemDto> BuildFocusQueue(
        IReadOnlyList<DeviceCommandEntity> commands,
        IReadOnlyList<DeviceEntity> devices,
        IReadOnlyList<DeviceSeatAssignmentEntity> assignments,
        IReadOnlyList<SeatEntity> seats,
        IReadOnlyList<SessionEntity> sessions,
        int limit)
    {
        var queue = new List<OperatorDashboardQueueItemDto>();
        var deviceById = devices.ToDictionary(device => device.DeviceId);
        var assignmentByDeviceId = assignments
            .GroupBy(assignment => assignment.DeviceId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(assignment => assignment.AttachedAtUtc).First());
        var seatById = seats.ToDictionary(seat => seat.SeatId);

        foreach (var command in commands.Where(command =>
            IsStatus(command.Status, "Failed") ||
            IsStatus(command.Status, "Rejected") ||
            IsStatus(command.Status, "Pending")))
        {
            deviceById.TryGetValue(command.DeviceId, out var device);
            var seatId = assignmentByDeviceId.TryGetValue(command.DeviceId, out var assignment)
                ? assignment.SeatId
                : (Guid?)null;
            var target = device?.MachineName ?? command.DeviceId.ToString("N")[..8];
            var title = IsStatus(command.Status, "Pending")
                ? $"{command.Type} pending"
                : $"{command.Type} {command.Status}";
            var detail = string.IsNullOrWhiteSpace(command.Message)
                ? "Device command needs operator attention."
                : command.Message;

            queue.Add(new OperatorDashboardQueueItemDto(
                IsStatus(command.Status, "Pending") ? "pending" : "blocking",
                target,
                title,
                detail,
                seatId,
                command.DeviceId,
                command.UpdatedAtUtc,
                "device-command"));
            if (queue.Count >= limit)
            {
                return queue;
            }
        }

        foreach (var device in devices.Where(device => !device.IsOnline).OrderBy(device => device.MachineName))
        {
            var seatId = assignmentByDeviceId.TryGetValue(device.DeviceId, out var assignment)
                ? assignment.SeatId
                : (Guid?)null;
            var target = ResolveSeatTarget(device.MachineName, seatId, seatById);
            queue.Add(new OperatorDashboardQueueItemDto(
                "service",
                target,
                "Device offline",
                device.LastHeartbeatAtUtc is null
                    ? "No heartbeat has been reported yet."
                    : $"Last heartbeat {device.LastHeartbeatAtUtc:O}.",
                seatId,
                device.DeviceId,
                device.LastHeartbeatAtUtc ?? device.EnrolledAtUtc,
                "device"));
            if (queue.Count >= limit)
            {
                return queue;
            }
        }

        foreach (var session in sessions.Where(session => IsState(session.State, SessionStateNames.Ending)).OrderByDescending(session => session.UpdatedAtUtc))
        {
            var target = ResolveSeatTarget(session.SeatId.ToString("N")[..8], session.SeatId, seatById);
            queue.Add(new OperatorDashboardQueueItemDto(
                "warning",
                target,
                "Session ending",
                "Session is waiting for device lock confirmation.",
                session.SeatId,
                session.DeviceId,
                session.UpdatedAtUtc,
                "session"));
            if (queue.Count >= limit)
            {
                return queue;
            }
        }

        return queue;
    }

    private static string ResolveSeatTarget(
        string fallback,
        Guid? seatId,
        IReadOnlyDictionary<Guid, SeatEntity> seatById)
    {
        return seatId is not null && seatById.TryGetValue(seatId.Value, out var seat)
            ? seat.Name
            : fallback;
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) NormalizeRange(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        DateTimeOffset nowUtc)
    {
        var to = toUtc ?? nowUtc;
        var from = fromUtc ?? new DateTimeOffset(to.UtcDateTime.Date, TimeSpan.Zero);

        return from <= to
            ? (from, to)
            : (to, from);
    }

    private static int NormalizeLimit(int? limit)
    {
        return limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
    }

    private static bool IsState(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStatus(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKind(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCurrencyCode(
        ShiftEntity? currentShift,
        IReadOnlyList<PaymentEntity> payments,
        IReadOnlyList<LedgerEntryEntity> ledgerEntries)
    {
        return currentShift?.CurrencyCode
            ?? payments.FirstOrDefault()?.CurrencyCode
            ?? ledgerEntries.FirstOrDefault()?.CurrencyCode
            ?? DefaultCurrencyCode;
    }

    private static MoneyDto Money(string currencyCode, long minorUnits)
    {
        return new MoneyDto(currencyCode, minorUnits);
    }
}
