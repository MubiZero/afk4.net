using AFK4.Platform.Api.Billing;

namespace AFK4.Platform.Api.Shifts;

public interface IOpenShiftResolver
{
    Task<BillingCommandServiceResult<Guid>> GetOpenShiftIdAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);
}
