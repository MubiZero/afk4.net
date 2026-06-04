using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Payments;

public interface IBranchPaymentGatewayResolver
{
    // Outbound: the active gateway for a branch, falling back to the org-level (null-branch) gateway.
    // Returns null when the branch has no usable online-payment gateway.
    Task<BranchPaymentGatewayEntity?> ResolveForBranchAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken);

    // Inbound: the gateway that owns a dcgate project id (any status), or null if unknown.
    Task<BranchPaymentGatewayEntity?> ResolveByProjectIdAsync(
        string dcgateProjectId, CancellationToken cancellationToken);
}
