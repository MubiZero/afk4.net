using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

public sealed class EfOrganizationOwnerResolver(PlatformDbContext dbContext) : IOrganizationOwnerResolver
{
    public async Task<OwnerRecipient?> ResolveAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var owner = await (
            from assignment in dbContext.StaffRoleAssignments
            join staff in dbContext.StaffUsers on assignment.StaffUserId equals staff.StaffUserId
            where assignment.OrganizationId == organizationId
                && assignment.RoleName == StaffRoleNames.Owner
                && staff.IsActive
                && staff.Email != null
            select staff)
            .FirstOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            return null;
        }

        var organizationName = await dbContext.Organizations
            .Where(org => org.OrganizationId == organizationId)
            .Select(org => org.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new OwnerRecipient(owner.StaffUserId, owner.DisplayName, owner.Email!, organizationName);
    }
}
