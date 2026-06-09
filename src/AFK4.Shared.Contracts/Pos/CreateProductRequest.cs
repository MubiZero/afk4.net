using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Pos;

public sealed record CreateProductRequest
{
    public CreateProductRequest(
        Guid organizationId,
        Guid categoryId,
        string name,
        string sku,
        MoneyDto price,
        bool trackStock,
        bool allowNegativeStock,
        string idempotencyKey,
        int reorderThreshold = 0)
    {
        OrganizationId = organizationId;
        CategoryId = categoryId;
        Name = name;
        Sku = sku;
        Price = price;
        TrackStock = trackStock;
        AllowNegativeStock = allowNegativeStock;
        IdempotencyKey = idempotencyKey;
        ReorderThreshold = reorderThreshold;
    }

    public Guid OrganizationId { get; init; }

    public Guid CategoryId { get; init; }

    public string Name { get; init; }

    public string Sku { get; init; }

    public MoneyDto Price { get; init; }

    public bool TrackStock { get; init; }

    public bool AllowNegativeStock { get; init; }

    public string IdempotencyKey { get; init; }

    public int ReorderThreshold { get; init; }

    public bool AvailableInShell { get; init; }
}
