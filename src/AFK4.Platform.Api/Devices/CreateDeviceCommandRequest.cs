namespace AFK4.Platform.Api.Devices;

public sealed record CreateDeviceCommandRequest(
    string Type,
    IReadOnlyDictionary<string, string> Payload);
