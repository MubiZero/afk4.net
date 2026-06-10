namespace AFK4.Shared.Contracts.News;

public sealed record CreateNewsItemRequest(
    Guid? BranchId,
    string Title,
    string Body,
    string? ImageUrl,
    bool IsPublished,
    DateTimeOffset? PublishAtUtc,
    DateTimeOffset? ExpiresAtUtc);
