using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Sessions;

public sealed record SessionCommandResponse(
    string IdempotencyKey,
    SessionDto Session,
    IReadOnlyList<DeviceCommandDto> DeviceCommands,
    // Anti-fraud §5.4: the assessed value of a comp (free) session; null for non-comp starts.
    long? CompValueMinorUnits = null);
