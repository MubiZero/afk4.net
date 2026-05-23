using AFK4.Shared.Contracts.Platform.Health;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IPlatformTenantHealthService
{
    Task<TenantHealthDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken);
}
