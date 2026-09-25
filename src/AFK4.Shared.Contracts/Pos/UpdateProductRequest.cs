using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Pos;

public sealed record UpdateProductRequest(
    Guid OrganizationId,
    Guid CategoryId,
    string Name,
    string Sku,
    MoneyDto Price,
    bool TrackStock,
    bool AllowNegativeStock,
    bool IsActive,
    int ReorderThreshold = 0,
    bool AvailableInShell = false,
    // Товар крутится в витрине свободного ПК.
    bool FeaturedOnPcs = false,
    // Фото товара: адрес загрузки с назначением product-image. Пусто — без фото.
    string? ImageUrl = null);
