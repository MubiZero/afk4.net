using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;

namespace AFK4.Platform.Api.Inventory;

public interface IInventoryService
{
    Task<BillingCommandServiceResult<PosProductCategoryDto>> CreateCategoryAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateProductCategoryRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosProductDto>> CreateProductAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateProductRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosProductDto>> UpdateProductAsync(
        Guid branchId,
        Guid productId,
        Guid actorStaffUserId,
        UpdateProductRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<StockMovementDto>> CreateStockMovementAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateStockMovementRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<IReadOnlyList<PosProductDto>>> GetCatalogAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<InventoryStockDto>> GetStockAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<IReadOnlyList<StockMovementDto>>> GetStockMovementsAsync(
        Guid organizationId,
        Guid branchId,
        Guid? productId,
        int limit,
        CancellationToken cancellationToken);
}
