namespace AFK4.Shared.Contracts.Pos;

public sealed record CreateProductCategoryRequest(
    Guid OrganizationId,
    string Name,
    string IdempotencyKey);
