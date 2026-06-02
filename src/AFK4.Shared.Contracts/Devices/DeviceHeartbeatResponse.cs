namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatResponse(
    DateTimeOffset ServerTimeUtc,
    int HeartbeatIntervalSeconds,
    IReadOnlyList<DeviceCommandDto> Commands,
    // Effective offline grace window (minutes) for this device's branch. The agent applies it locally
    // to keep a paying customer playing for this long after the network actually drops (spec §6.1).
    int EffectiveGraceMinutes = 15);
