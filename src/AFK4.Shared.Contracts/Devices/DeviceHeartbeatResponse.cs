namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatResponse(
    DateTimeOffset ServerTimeUtc,
    int HeartbeatIntervalSeconds,
    IReadOnlyList<DeviceCommandDto> Commands);
