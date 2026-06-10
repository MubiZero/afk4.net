using AFK4.Shared.Contracts.News;

namespace AFK4.Platform.Api.News;

public interface INewsService
{
    Task<IReadOnlyList<NewsItemDto>> ListForOwnerAsync(Guid orgId, CancellationToken ct);
    Task<NewsMutationResult> CreateAsync(Guid orgId, CreateNewsItemRequest request, CancellationToken ct);
    Task<NewsMutationResult> UpdateAsync(Guid orgId, Guid id, UpdateNewsItemRequest request, CancellationToken ct);
    Task<NewsMutationOutcome> DeleteAsync(Guid orgId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<PlayerNewsItemDto>> ListForPlayerAsync(Guid orgId, Guid? homeBranchId, CancellationToken ct);
}
