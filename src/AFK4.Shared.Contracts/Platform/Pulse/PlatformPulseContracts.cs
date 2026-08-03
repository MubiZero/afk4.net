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

// DetailValue carries the single numeric figure behind an alert, meaning depends on Kind:
// minutes since the last agent heartbeat for AgentSilent, minutes since the shift was
// opened for ShiftNotClosed, count of devices that reported a failed install for
// RolloutFailed. It is null for alert kinds/situations with no such figure
// (PaymentOverdue always; AgentSilent when the device has never reported a heartbeat at
// all; RolloutFailed when the rollout was flagged manually before any device reported a
// failure). Clients must never render a raw backend string as user-facing alert text —
// every kind has a translated label, and any parameterized detail is built client-side
// from DetailValue, not shipped as pre-rendered prose.
public sealed record PulseAlertDto(
    string Kind,
    string Level,
    int? DetailValue);

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
