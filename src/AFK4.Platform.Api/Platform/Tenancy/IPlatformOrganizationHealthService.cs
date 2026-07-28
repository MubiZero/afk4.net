using AFK4.Shared.Contracts.Platform.Health;

namespace AFK4.Platform.Api.Platform.Tenancy;

public interface IPlatformOrganizationHealthService
{
    Task<OrganizationHealthDto?> GetAsync(Guid organizationId, CancellationToken cancellationToken);
}
