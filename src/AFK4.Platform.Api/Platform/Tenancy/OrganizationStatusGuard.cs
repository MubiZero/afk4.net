using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed record OrganizationStatusSnapshot(string Status, string? Reason)
{
    public bool IsActive => string.Equals(Status, OrganizationStatusNames.Active, StringComparison.Ordinal);

    public bool IsSuspended => string.Equals(Status, OrganizationStatusNames.Suspended, StringComparison.Ordinal);

    public bool IsDeletionPending => string.Equals(Status, OrganizationStatusNames.DeletionPending, StringComparison.Ordinal);
}

public interface IOrganizationStatusGuard
{
    Task<OrganizationStatusSnapshot?> GetAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed class EfOrganizationStatusGuard(PlatformDbContext dbContext) : IOrganizationStatusGuard
{
    public async Task<OrganizationStatusSnapshot?> GetAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .AsNoTracking()
            .Where(org => org.OrganizationId == organizationId)
            .Select(org => new OrganizationStatusSnapshot(org.Status, org.StatusReason))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
