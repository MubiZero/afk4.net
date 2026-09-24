namespace AFK4.Shared.Contracts.Devices;

/// <summary>Где команда: ждёт, отдана агенту, устарела или агент уже ответил.</summary>
public static class DeviceCommandStatusNames
{
    public const string Pending = "Pending";

    /// <summary>
    /// Отдана агенту и больше не отдаётся — у неповторяемых команд (перезагрузка, выключение,
    /// пробуждение). Повторная выдача той же перезагрузки после перезапуска агента была бы петлёй.
    /// </summary>
    public const string Delivered = "Delivered";

    /// <summary>
    /// Неповторяемая команда пролежала дольше срока и не отдана: перезагрузка, пришедшая через три
    /// дня после просьбы, хуже потерянной.
    /// </summary>
    public const string Expired = "Expired";

    public const string Accepted = "Accepted";

    public const string Rejected = "Rejected";

    public const string Failed = "Failed";

    public const string Completed = "Completed";
}
