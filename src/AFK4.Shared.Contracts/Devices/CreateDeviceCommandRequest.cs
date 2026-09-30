namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Команда ПК из Панели и между службами платформы. IdempotencyKey — ключ одного нажатия: связь
/// оборвалась до ответа, и Панель шлёт команду снова с тем же ключом — сервер вернёт уже записанную
/// команду, а не пошлёт на ПК вторую перезагрузку. Null — повтор не распознаётся.
/// </summary>
public sealed record CreateDeviceCommandRequest(
    string Type,
    IReadOnlyDictionary<string, string> Payload,
    string? IdempotencyKey = null);
