using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Dashboard;

public sealed record OperatorDashboardSummaryDto(
    Guid OrganizationId,
    Guid BranchId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    DateTimeOffset GeneratedAtUtc,
    OperatorDashboardShiftSummaryDto Shift,
    OperatorDashboardRevenueSummaryDto Revenue,
    OperatorDashboardUtilizationSummaryDto Utilization,
    OperatorDashboardAlertPressureDto AlertPressure,
    OperatorDashboardReservationSummaryDto Reservations,
    IReadOnlyList<OperatorDashboardQueueItemDto> FocusQueue,
    IReadOnlyList<OperatorDashboardRecentPaymentDto> RecentPayments);

public sealed record OperatorDashboardShiftSummaryDto(
    Guid? ShiftId,
    string State,
    DateTimeOffset? OpenedAtUtc,
    Guid? OpenedByStaffUserId,
    MoneyDto ExpectedCash);

public sealed record OperatorDashboardRevenueSummaryDto(
    MoneyDto PosNetSales,
    MoneyDto GameplayRevenue,
    MoneyDto TotalRevenue,
    int PosCheckCount,
    int NewPlayerCount);

public sealed record OperatorDashboardUtilizationSummaryDto(
    int TotalSeats,
    int ActiveSessions,
    int EndingSessions,
    int OnlineDevices,
    int OfflineDevices,
    int SessionStarts,
    int UtilizationPercent);

public sealed record OperatorDashboardAlertPressureDto(
    int PendingCommands,
    int FailedCommands,
    int OfflineDevices,
    int EndingSessions,
    int TotalAlerts);

public sealed record OperatorDashboardReservationSummaryDto(
    int ActiveReservations,
    int AvailableSlots,
    string Source);

public sealed record OperatorDashboardQueueItemDto(
    string Tone,
    string Target,
    string Title,
    string Detail,
    Guid? SeatId,
    Guid? DeviceId,
    DateTimeOffset CreatedAtUtc,
    string SourceType);

public sealed record OperatorDashboardRecentPaymentDto(
    Guid PaymentId,
    Guid PosSaleId,
    Guid ShiftId,
    Guid CreatedByStaffUserId,
    string PaymentKind,
    string PaymentMethod,
    MoneyDto Amount,
    DateTimeOffset CreatedAtUtc);
