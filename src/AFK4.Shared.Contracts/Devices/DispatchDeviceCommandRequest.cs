namespace AFK4.Shared.Contracts.Devices;

public sealed record DispatchDeviceCommandRequest(
    string Type,
    IReadOnlyDictionary<string, string> Payload);
