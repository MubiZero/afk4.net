namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Команда ПК из Панели. IdempotencyKey — ключ одного нажатия: связь оборвалась до ответа, и
/// Панель шлёт команду снова с тем же ключом — сервер вернёт уже записанную команду, а не
/// пошлёт на ПК вторую перезагрузку.
/// </summary>
public sealed record DispatchDeviceCommandRequest(
    string Type,
    IReadOnlyDictionary<string, string> Payload,
    string? IdempotencyKey = null);
