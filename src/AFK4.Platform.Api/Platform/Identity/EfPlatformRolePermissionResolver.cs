using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class EfPlatformRolePermissionResolver(PlatformDbContext dbContext) : IPlatformRolePermissionResolver
{
    public async Task<IReadOnlySet<string>> ResolveAsync(
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken)
    {
        var names = roleNames.ToArray();
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (names.Length == 0)
        {
            return permissions;
        }

        var roles = await dbContext.PlatformRoles
            .AsNoTracking()
            .Where(role => names.Contains(role.RoleName))
            .Select(role => new { role.RoleName, role.GrantsAllPermissions })
            .ToArrayAsync(cancellationToken);

        // Роль с полным доступом получает и те права, которых ещё не существовало, когда её
        // заводили: иначе новое право после деплоя не принадлежит никому.
        if (roles.Any(role => role.GrantsAllPermissions))
        {
            permissions.UnionWith(PlatformAdminPermissionNames.All);
            return permissions;
        }

        var granted = await dbContext.PlatformRolePermissions
            .AsNoTracking()
            .Where(rolePermission => names.Contains(rolePermission.RoleName))
            .Select(rolePermission => rolePermission.PermissionName)
            .ToArrayAsync(cancellationToken);

        permissions.UnionWith(granted);

        // Право, исчезнувшее из кода, не должно продолжать действовать из-за старой строки в базе.
        permissions.IntersectWith(PlatformAdminPermissionNames.All);
        return permissions;
    }

    public Task<bool> IsKnownRoleAsync(string roleName, CancellationToken cancellationToken) =>
        dbContext.PlatformRoles.AnyAsync(role => role.RoleName == roleName, cancellationToken);
}
