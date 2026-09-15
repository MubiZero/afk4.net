namespace AFK4.Shared.Contracts.News;

/// <summary>Новость или акция клуба.</summary>
public sealed record PlayerNewsItemDto(
    Guid Id,
    string Title,
    string Body,
    string? ImageUrl,
    DateTimeOffset PublishedAtUtc);
