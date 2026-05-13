using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Platform.Api.Shifts;

public interface IShiftService
{
    Task<BillingCommandServiceResult<ShiftDto>> OpenShiftAsync(
        Guid branchId,
        Guid actorStaffUserId,
        OpenShiftRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<ShiftDto?>> GetCurrentShiftAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<CashMovementDto>> RecordCashMovementAsync(
        Guid shiftId,
        Guid actorStaffUserId,
        RecordCashMovementRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<ShiftDto>> CloseShiftAsync(
        Guid shiftId,
        Guid actorStaffUserId,
        CloseShiftRequest request,
        CancellationToken cancellationToken);
}
