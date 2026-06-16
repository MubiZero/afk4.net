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
    int? SessionVersion = null,
    // Who is on the seat right now: the active session's player display name. Null for a
    // guest session with no account, or a free seat.
    string? PlayerDisplayName = null,
    // The tariff the active session bills against. Null for guest/package sessions that
    // carry no named tariff, or a free seat.
    string? TariffName = null,
    // When the active session started (UTC) — lets the operator show real elapsed time.
    DateTimeOffset? SessionStartedAtUtc = null,
    // Floor-plan layout: grid cell + orientation + host type. Null/default until the branch is
    // arranged in the «План» editor (B2); the abstract grid view ignores these.
    int? PosX = null,
    int? PosY = null,
    int Rotation = 0,
    string SeatType = "pc");
