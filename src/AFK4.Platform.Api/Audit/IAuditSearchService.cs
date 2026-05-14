using AFK4.Shared.Contracts.Audit;

namespace AFK4.Platform.Api.Audit;

public interface IAuditSearchService
{
    Task<AuditSearchResultDto> SearchAsync(
        Guid organizationId,
        Guid branchId,
        AuditSearchQuery query,
        CancellationToken cancellationToken);
}
