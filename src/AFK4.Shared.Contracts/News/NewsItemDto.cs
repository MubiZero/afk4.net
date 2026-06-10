namespace AFK4.Shared.Contracts.News;

public sealed record NewsItemDto(
    Guid Id,
    Guid? BranchId,
    string Title,
    string Body,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset? PublishAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
