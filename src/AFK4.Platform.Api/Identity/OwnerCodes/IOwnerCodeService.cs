namespace AFK4.Platform.Api.Identity.OwnerCodes;

public interface IOwnerCodeService
{
    Task<OwnerCodeOperationResult<OwnerCodeIssued>> GenerateAsync(
        Guid staffUserId,
        CancellationToken cancellationToken);

    Task<OwnerCodeOperationResult<OwnerCodeIssued>> RotateAsync(
        Guid staffUserId,
        string? reason,
        CancellationToken cancellationToken);

    Task<OwnerCodeSummary?> GetActiveSummaryAsync(
        Guid staffUserId,
        CancellationToken cancellationToken);

    Task<OwnerCodeLookupResult> LookupActiveAsync(
        string rawCode,
        CancellationToken cancellationToken);
}
