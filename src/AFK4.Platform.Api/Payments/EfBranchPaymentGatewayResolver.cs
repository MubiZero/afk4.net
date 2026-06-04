using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Payments;

public sealed class EfBranchPaymentGatewayResolver(PlatformDbContext dbContext)
    : IBranchPaymentGatewayResolver
{
    public async Task<BranchPaymentGatewayEntity?> ResolveForBranchAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var branchGateway = await dbContext.BranchPaymentGateways
            .AsNoTracking()
            .SingleOrDefaultAsync(
                gateway => gateway.OrganizationId == organizationId
                    && gateway.BranchId == branchId
                    && gateway.Status == BranchPaymentGatewayStatus.Active,
                cancellationToken);
        if (branchGateway is not null)
        {
            return branchGateway;
        }

        return await dbContext.BranchPaymentGateways
            .AsNoTracking()
            .SingleOrDefaultAsync(
                gateway => gateway.OrganizationId == organizationId
                    && gateway.BranchId == null
                    && gateway.Status == BranchPaymentGatewayStatus.Active,
                cancellationToken);
    }

    public async Task<BranchPaymentGatewayEntity?> ResolveByProjectIdAsync(
        string dcgateProjectId, CancellationToken cancellationToken) =>
        await dbContext.BranchPaymentGateways
            .AsNoTracking()
            .SingleOrDefaultAsync(
                gateway => gateway.DcgateProjectId == dcgateProjectId, cancellationToken);
}
