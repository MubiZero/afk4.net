namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Повтор команды ПК после обрыва связи. Панель не знает, дошла ли перезагрузка, если ответ
/// потерялся по дороге, и шлёт её снова с тем же ключом. Сервер узнаёт ключ и отдаёт уже
/// записанную команду — вторая перезагрузка посреди загрузки Windows до ПК не доедет.
/// </summary>
public static class DeviceCommandIdempotency
{
    public const int MaxClientKeyLength = 128;

    /// <summary>
    /// Ключ живёт в пределах ПК, на который жали. Чужой ключ, совпавший случайно или нарочно,
    /// не вернёт команду другого ПК — а пробуждение, которое исполняет сосед, всё равно
    /// опознаётся по ПК, который будили.
    /// </summary>
    public static string Scope(Guid deviceId, string clientKey) => $"{deviceId:N}:{clientKey}";
}
