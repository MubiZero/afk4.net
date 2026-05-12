namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceCommandDto(
    Guid CommandId,
    string Type,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Payload);
