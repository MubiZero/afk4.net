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

// DetailMinutes carries the elapsed-time figure behind a timing alert (minutes since the
// last agent heartbeat for AgentSilent, minutes since the shift was opened for
// ShiftNotClosed) so the client can build a localized message from it. It is null for
// alert kinds that carry no elapsed-time figure (PaymentOverdue, RolloutFailed) and for
// AgentSilent specifically when the device has never reported a heartbeat at all.
// Clients must never render a raw backend string as user-facing alert text — every kind
// has a translated label, and any parameterized detail is built client-side from
// DetailMinutes, not shipped as pre-rendered prose.
public sealed record PulseAlertDto(
    string Kind,
    string Level,
    int? DetailMinutes);

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
