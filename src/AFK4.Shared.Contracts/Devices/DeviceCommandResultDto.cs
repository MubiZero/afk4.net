namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceCommandResultDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CommandId,
    string Status,
    string Message,
    DateTimeOffset ObservedAtUtc);
