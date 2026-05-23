using AFK4.Shared.Contracts.Platform.Operator;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IOperatorConnectionResolver
{
    Task<PlatformTenantOperationResult<ResolveOperatorConnectionResponse>> ResolveAsync(
        ResolveOperatorConnectionRequest request,
        CancellationToken cancellationToken);
}
