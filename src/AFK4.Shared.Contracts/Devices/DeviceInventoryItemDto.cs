namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceInventoryItemDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset EnrolledAtUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    bool IsOnline,
    bool IsLocked,
    Guid? SeatId,
    string? SeatName,
    Guid? ZoneId,
    string? ZoneName,
    int ActiveCredentialCount,
    int InstalledAppCount,
    int PendingCommandCount,
    int FailedCommandCount);
