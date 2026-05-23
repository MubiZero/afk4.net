using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Tenancy;

public sealed record TenantStatusSnapshot(string Status, string? Reason)
{
    public bool IsActive => string.Equals(Status, TenantStatusNames.Active, StringComparison.Ordinal);

    public bool IsSuspended => string.Equals(Status, TenantStatusNames.Suspended, StringComparison.Ordinal);

    public bool IsDeletionPending => string.Equals(Status, TenantStatusNames.DeletionPending, StringComparison.Ordinal);
}

public interface ITenantStatusGuard
{
    Task<TenantStatusSnapshot?> GetAsync(Guid organizationId, CancellationToken cancellationToken);
}

public sealed class EfTenantStatusGuard(PlatformDbContext dbContext) : ITenantStatusGuard
{
    public async Task<TenantStatusSnapshot?> GetAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        return await dbContext.Organizations
            .AsNoTracking()
            .Where(org => org.OrganizationId == organizationId)
            .Select(org => new TenantStatusSnapshot(org.Status, org.StatusReason))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
