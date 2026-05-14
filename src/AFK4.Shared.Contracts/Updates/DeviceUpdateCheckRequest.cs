namespace AFK4.Shared.Contracts.Updates;

public sealed record DeviceUpdateCheckRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string Channel,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<DeviceComponentVersionDto> InstalledComponents);
