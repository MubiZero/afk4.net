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
    DateTimeOffset UpdatedAtUtc,
    // Новость крутится и на экране свободного ПК (витрина), а не только в приложении.
    bool ShowOnPcs = false);
