namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Во что упирается ответ «на связи ли машина»: текущее время и допустимая давность последнего
/// сердцебиения. Пара ходит вместе, чтобы список устройств, карточка и карта зала не разошлись
/// в том, кого считать живым.
/// </summary>
public readonly record struct DeviceOnlineWindow(DateTimeOffset NowUtc, int StaleHeartbeatSeconds);
