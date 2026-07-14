using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Pos;

namespace AFK4.Platform.Api.Pos;

public interface IPosSettlementService
{
    Task<BillingCommandServiceResult<PosSaleDto>> SettleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        SettlePosSaleRequest request,
        CancellationToken cancellationToken);
}
