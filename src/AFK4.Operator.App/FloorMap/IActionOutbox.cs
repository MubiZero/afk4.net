namespace AFK4.Operator.App.FloorMap;

/// <summary>
/// Local queue of operator lock/unlock intents recorded while the platform is unreachable (spec §6.5).
/// Lock/unlock are idempotent device commands that reconcile against cloud authority, so they are safe
/// to queue and replay on reconnect (unlike billing, which stays online-only per D2).
/// </summary>
public interface IActionOutbox
{
    /// <summary>
    /// Records an action. A newer action for the same device replaces the prior one (latest operator
    /// intent wins) so a queued lock followed by an unlock collapses to a single unlock.
    /// </summary>
    void Enqueue(OperatorActionOutboxEntry entry);

    IReadOnlyList<OperatorActionOutboxEntry> Pending { get; }

    /// <summary>Removes a delivered action. No-op for an unknown key.</summary>
    void Acknowledge(Guid idempotencyKey);
}

public sealed record OperatorActionOutboxEntry(
    Guid IdempotencyKey,
    Guid DeviceId,
    Guid SeatId,
    string SeatName,
    string CommandType,
    Guid? ExpectedSessionId,
    DateTimeOffset QueuedAtUtc);
