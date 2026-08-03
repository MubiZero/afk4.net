namespace AFK4.Shared.Contracts.Platform.Pulse;

public static class PulseAlertLevelNames
{
    public const string Normal = "normal";
    public const string Attention = "attention";
    public const string Critical = "critical";
}

public static class PulseAlertKindNames
{
    public const string AgentSilent = "agent_silent";
    public const string ShiftNotClosed = "shift_not_closed";
    public const string PaymentOverdue = "payment_overdue";
    public const string RolloutFailed = "rollout_failed";
}

public sealed record PulseAlertDto(
    string Kind,
    string Level,
    string? Detail);

public sealed record PulseClubDto(
    Guid BranchId,
    string Name,
    string City,
    int DevicesOnline,
    int DevicesTotal,
    int SeatsOccupied,
    int SeatsTotal,
    bool ShiftOpen,
    DateTimeOffset? ShiftOpenedAtUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    IReadOnlyList<PulseAlertDto> Alerts);

public sealed record PulseOrganizationDto(
    Guid OrganizationId,
    string Name,
    string Status,
    string PlanCode,
    string SubscriptionStatus,
    string AlertLevel,
    long OutstandingMinorUnits,
    string CurrencyCode,
    IReadOnlyList<PulseAlertDto> Alerts,
    IReadOnlyList<PulseClubDto> Clubs);

public sealed record PlatformPulseDto(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<PulseOrganizationDto> Organizations);
