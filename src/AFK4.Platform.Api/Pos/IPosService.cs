using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;

namespace AFK4.Platform.Api.Pos;

public interface IPosService
{
    Task<BillingCommandServiceResult<PosSaleDto>> CreateSaleAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreatePosSaleRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosSaleDto>> PaySaleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        ManualPaymentRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosSaleDto>> RefundSaleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosSaleDto>> VoidSaleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        VoidPosSaleRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosSaleDto>> GetSaleAsync(
        Guid organizationId,
        Guid posSaleId,
        CancellationToken cancellationToken);
}
