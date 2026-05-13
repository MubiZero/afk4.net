namespace AFK4.Shared.Contracts.Pos;

public sealed record PosProductCategoryDto(
    Guid CategoryId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
