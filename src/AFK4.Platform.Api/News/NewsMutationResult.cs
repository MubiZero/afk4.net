using AFK4.Shared.Contracts.News;

namespace AFK4.Platform.Api.News;

public enum NewsMutationOutcome
{
    Success,
    ValidationFailed,
    NotFound
}

public sealed record NewsMutationResult(NewsMutationOutcome Outcome, NewsItemDto? Item, string? Error)
{
    public static NewsMutationResult Ok(NewsItemDto item) => new(NewsMutationOutcome.Success, item, null);
    public static NewsMutationResult Invalid(string error) => new(NewsMutationOutcome.ValidationFailed, null, error);
    public static readonly NewsMutationResult Missing = new(NewsMutationOutcome.NotFound, null, null);
}
