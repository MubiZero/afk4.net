namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceCommandStatusDto(
    Guid DeviceId,
    Guid CommandId,
    string Type,
    string Status,
    string? Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
