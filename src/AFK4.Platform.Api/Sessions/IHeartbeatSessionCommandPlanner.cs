using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Sessions;

public interface IHeartbeatSessionCommandPlanner
{
    Task<IReadOnlyList<HeartbeatSessionCommandPlan>> PlanAsync(
        Guid deviceId,
        DeviceHeartbeatRequest heartbeat,
        CancellationToken cancellationToken);

    /// <summary>
    /// «Разблокировать» из Панели: агент отпирает ПК только по подписанной аренде, а Панель её не
    /// знает. Идёт сессия — к команде добавляются sessionId и свежая аренда; нет — тело как есть.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> WithSessionLeaseAsync(
        Guid deviceId,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken);
}
