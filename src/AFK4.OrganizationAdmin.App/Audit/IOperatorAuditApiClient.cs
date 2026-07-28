using AFK4.Shared.Contracts.Audit;

namespace AFK4.OrganizationAdmin.App.Audit;

public interface IOperatorAuditApiClient
{
    Task<AuditSearchResultDto> SearchAsync(
        OperatorAuditSearchRequest request,
        CancellationToken cancellationToken);
}

public sealed record OperatorAuditSearchRequest(
    Guid BranchId,
    string? Action,
    string? Outcome,
    string? TargetType,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int? Limit);
