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
    int? RemainingSeconds,
    // Live accrued time cost for an open-tab session (count-up). Null for fixed
    // sessions (which expose RemainingSeconds instead) and unbilled guests.
    long? AccruedCostMinorUnits = null,
    string? CurrencyCode = null,
    // Optimistic-concurrency version of the active session; the operator echoes it back as
    // ExpectedVersion on a seat mutation so a stale view loses the race with a 409.
    int? SessionVersion = null);
