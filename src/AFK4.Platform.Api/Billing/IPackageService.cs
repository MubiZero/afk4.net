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

    Task<BillingCommandServiceResult<PackageDefinitionDto>> UpdatePackageDefinitionAsync(
        Guid branchId,
        Guid packageDefinitionId,
        Guid actorStaffUserId,
        UpdatePackageDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PlayerPackageDto>> PurchasePackageAsync(
        Guid playerAccountId,
        Guid branchId,
        Guid actorStaffUserId,
        PurchasePackageRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// The same purchase, bought by the player from the app instead of at the counter. A package is
    /// prepaid time and moves money the player already holds from wallet to package time, so unlike
    /// a counter sale it needs no open shift — requiring one would mean the club can only sell
    /// prepaid time while it is open, which is the opposite of what prepaying is for.
    /// </summary>
    Task<BillingCommandServiceResult<PlayerPackageDto>> PurchasePackageAsPlayerAsync(
        Guid playerAccountId,
        Guid branchId,
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
