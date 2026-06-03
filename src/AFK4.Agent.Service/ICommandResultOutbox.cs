using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

/// <summary>
/// Durable, file-backed queue for device-command results awaiting delivery to the backend (spec §6.4).
/// The agent persists the result locally the instant the command physically executes (lock/unlock),
/// then retries delivery on every heartbeat until the backend acks. Delivery is idempotent — the backend
/// keys commands by <c>CommandId</c> — so SignalR and HTTP can both drain this one queue safely.
/// </summary>
public interface ICommandResultOutbox
{
    /// <summary>Persists a result locally. Re-enqueuing the same <c>CommandId</c> replaces the prior entry.</summary>
    void Enqueue(DeviceCommandResultDto result);

    /// <summary>Results awaiting delivery, in enqueue order.</summary>
    IReadOnlyList<DeviceCommandResultDto> Pending { get; }

    /// <summary>Removes a result once the backend has acknowledged it. No-op for unknown commands.</summary>
    void Acknowledge(Guid commandId);
}
