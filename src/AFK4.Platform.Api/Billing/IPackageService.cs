using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Platform.Api.Billing;

public interface IPackageService
{
    Task<BillingCommandServiceResult<PackageDefinitionDto>> CreatePackageDefinitionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePackageDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PlayerPackageDto>> PurchasePackageAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PurchasePackageRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<IReadOnlyList<LedgerEntryDto>>> ConsumePackageTimeAsync(
        Guid playerAccountId,
        Guid playerPackageId,
        Guid branchId,
        Guid sessionId,
        Guid actorStaffUserId,
        int durationSeconds,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
