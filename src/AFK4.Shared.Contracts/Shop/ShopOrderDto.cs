using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shop;

/// <summary>Заказ к месту и его судьба: оформлен, готовится, принесли, отменён.</summary>
public sealed record ShopOrderDto(
    Guid Id,
    Guid BranchId,
    Guid SeatId,
    Guid PlayerAccountId,
    string PlayerDisplayName,
    // `placed` и `accepted` — заказ ещё в работе, за ним есть смысл следить и его ещё можно
    // отменить. После «принесли» отменять нечего. Одно из ShopOrderStatusNames.
    string Status,
    MoneyDto Total,
    IReadOnlyList<ShopOrderLineDto> Lines,
    // Когда заказ оформили. Нужно списку прошлых заказов: без времени «принесли» и «отменён»
    // сливаются в кучу одинаковых строк.
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? CancelledAtUtc,
    int Version,
    Guid? PosSaleId = null,
    // Имя места на стене — «PC-12»: туда и несут заказ. Без него лента на стойке могла показать
    // только идентификатор места, который вслух никто не произносит.
    string? SeatName = null);
