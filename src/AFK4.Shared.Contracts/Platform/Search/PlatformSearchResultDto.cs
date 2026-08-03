namespace AFK4.Shared.Contracts.Platform.Search;

public sealed record PlatformSearchResultDto(
    string Kind,
    Guid Id,
    string Title,
    string Context,
    string Href);
