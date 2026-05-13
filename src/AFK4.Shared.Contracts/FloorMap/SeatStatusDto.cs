namespace AFK4.Shared.Contracts.FloorMap;

public sealed record SeatStatusDto(
    Guid SeatId,
    string SeatName,
    Guid ZoneId,
    string ZoneName,
    int SortOrder,
    string State,
    Guid? DeviceId,
    string? DeviceName,
    bool? IsDeviceOnline,
    bool? IsDeviceLocked,
    DateTimeOffset? LastHeartbeatAtUtc,
    string? AgentVersion,
    string? ShellVersion,
    Guid? ActiveSessionId,
    int? RemainingSeconds);
