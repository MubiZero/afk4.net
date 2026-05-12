namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceStatusChangedDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    bool IsOnline,
    bool IsLocked,
    DateTimeOffset ObservedAtUtc);
