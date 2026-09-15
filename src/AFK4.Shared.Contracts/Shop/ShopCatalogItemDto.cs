using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shop;

/// <summary>
/// Позиция меню бара: что можно заказать к месту прямо во время сессии.
/// </summary>
public sealed record ShopCatalogItemDto(
    Guid ProductId,
    string Name,
    string Sku,
    MoneyDto Price,
    // Остаток на складе филиала. Сервер уже убрал отсюда то, что кончилось и не продаётся в минус,
    // поэтому число нужно только чтобы предупредить о последних штуках.
    int StockOnHand);
