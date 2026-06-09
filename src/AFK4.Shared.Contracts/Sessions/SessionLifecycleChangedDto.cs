namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// A session-lifecycle change broadcast over the DeviceHub branch group so operator clients can
/// patch the floor map and apply a dashboard delta without polling. Pushes are hints; the client
/// reconciles against an authoritative reload and ignores an event whose <see cref="Version"/> is
/// not newer than the one it already applied for that session.
/// </summary>
public sealed record SessionLifecycleChangedDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid SeatId,
    Guid SessionId,
    string Kind,
    string State,
    int Version,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? EndsAtUtc,
    DateTimeOffset ObservedAtUtc,
    long? AccruedCostMinorUnits = null,
    string? CurrencyCode = null);

public static class SessionLifecycleKinds
{
    public const string Started = "started";

    public const string Extended = "extended";

    public const string Transferred = "transferred";

    public const string Ended = "ended";

    public const string Checkout = "checkout";
}
