using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Sessions;

public sealed record SessionCommandResponse(
    string IdempotencyKey,
    SessionDto Session,
    IReadOnlyList<DeviceCommandDto> DeviceCommands);
