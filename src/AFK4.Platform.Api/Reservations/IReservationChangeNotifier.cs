using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Рассказывает стойке об изменении брони. Тонкий шов над DeviceHub — тот же приём, что у
/// событий сессии: сервисы броней не берут прямую зависимость на хаб и остаются проверяемыми с
/// записывающей подделкой.
///
/// Зовут его <b>после</b> того, как изменение легло в базу: откатившаяся правка не имеет права
/// оставить на экранах решение, которого не было.
/// </summary>
public interface IReservationChangeNotifier
{
    Task NotifyAsync(ReservationChangedDto change, CancellationToken cancellationToken);
}

public sealed class SignalRReservationChangeNotifier(IHubContext<DeviceHub> hubContext) : IReservationChangeNotifier
{
    public Task NotifyAsync(ReservationChangedDto change, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(DeviceHubGroups.Branch(change.BranchId))
            .SendAsync(DeviceRealtimeEvents.ReservationChanged, change, cancellationToken);
}

/// <summary>Никому не рассказывает. Для путей, где стойки нет и слушать событие некому.</summary>
public sealed class NullReservationChangeNotifier : IReservationChangeNotifier
{
    public static readonly NullReservationChangeNotifier Instance = new();

    public Task NotifyAsync(ReservationChangedDto change, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
