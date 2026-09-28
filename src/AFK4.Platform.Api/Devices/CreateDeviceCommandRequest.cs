namespace AFK4.Platform.Api.Devices;

/// <param name="IdempotencyKey">
/// Ключ повтора. В теле запроса это ключ нажатия из Панели; дальше по серверу он едет уже
/// привязанным к ПК, на который жали (<see cref="DeviceCommandIdempotency.Scope"/>).
/// Null — повтор не распознаётся.
/// </param>
public sealed record CreateDeviceCommandRequest(
    string Type,
    IReadOnlyDictionary<string, string> Payload,
    string? IdempotencyKey = null);
