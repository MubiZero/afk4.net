namespace AFK4.Shared.Contracts.Inventory;

/// <summary>
/// Машинные имена отказов по каталогу склада: категории, товары, штрихкоды. См.
/// <see cref="Pos.PosErrorCodeNames"/> — та же причина: имя или код уже заняты соседней записью,
/// и форма не проверяет это заранее.
/// </summary>
public static class InventoryErrorCodeNames
{
    /// <summary>Категория с таким именем в филиале уже есть.</summary>
    public const string CategoryNameTaken = "product_category_name_taken";

    /// <summary>Товар с таким артикулом в филиале уже есть.</summary>
    public const string SkuTaken = "product_sku_taken";

    /// <summary>Этот штрихкод уже привязан к другому товару.</summary>
    public const string BarcodeAlreadyBound = "barcode_already_bound";
}
