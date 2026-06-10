namespace AFK4.Shared.Contracts.News;

public sealed record PlayerNewsItemDto(
    Guid Id,
    string Title,
    string Body,
    string? ImageUrl,
    DateTimeOffset PublishedAtUtc);
