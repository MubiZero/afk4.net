namespace AFK4.Shared.Contracts.FloorMap;

/// <summary>
/// Состояние места на карте зала — то, что сервер кладёт в <see cref="SeatStatusDto"/>.State.
/// Пишутся с большой буквы, в отличие от состояний сессии: это отдельный словарь карты, а не код
/// сессии. Пока их писали литералами, Панель сравнивала сырое значение с «free» и «ready», которых
/// сервер не присылает никогда, и «Посадить за ПК» из карточки клиента не предлагало ни одного
/// места (#423).
/// </summary>
public static class SeatStateNames
{
    public const string Free = "Free";

    /// <summary>ПК на связи и заперт: гость может сесть — это то же «свободно».</summary>
    public const string Locked = "Locked";

    public const string Active = "Active";

    public const string Paused = "Paused";

    public const string Ending = "Ending";

    /// <summary>ПК не привязан к месту или не одобрен.</summary>
    public const string Maintenance = "Maintenance";

    public const string Offline = "Offline";
}
