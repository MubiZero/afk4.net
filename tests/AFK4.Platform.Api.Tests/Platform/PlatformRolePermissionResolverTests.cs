using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformRolePermissionResolverTests
{
    private static PlatformDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static PlatformRoleEntity NewRole(string roleName, bool grantsAllPermissions = false)
    {
        var now = DateTimeOffset.UtcNow;
        return new PlatformRoleEntity
        {
            RoleName = roleName,
            DisplayName = roleName,
            Description = roleName,
            IsBuiltIn = false,
            GrantsAllPermissions = grantsAllPermissions,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static PlatformRolePermissionEntity NewGrant(string roleName, string permissionName)
    {
        return new PlatformRolePermissionEntity
        {
            PlatformRolePermissionId = Guid.NewGuid(),
            RoleName = roleName,
            PermissionName = permissionName
        };
    }

    [Fact]
    public async Task Resolve_ReturnsPermissionsOfTheRole()
    {
        await using var dbContext = NewDbContext();
        dbContext.PlatformRoles.Add(NewRole("custom_role"));
        dbContext.PlatformRolePermissions.Add(NewGrant("custom_role", PlatformAdminPermissionNames.ViewOrganizations));
        dbContext.PlatformRolePermissions.Add(NewGrant("custom_role", PlatformAdminPermissionNames.ViewPlatformAudit));
        await dbContext.SaveChangesAsync();

        var resolver = new EfPlatformRolePermissionResolver(dbContext);

        var permissions = await resolver.ResolveAsync(["custom_role"], CancellationToken.None);

        Assert.Equal(
            new[] { PlatformAdminPermissionNames.ViewOrganizations, PlatformAdminPermissionNames.ViewPlatformAudit }
                .OrderBy(name => name, StringComparer.Ordinal),
            permissions.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Resolve_UnionsPermissionsOfSeveralRoles()
    {
        await using var dbContext = NewDbContext();
        dbContext.PlatformRoles.Add(NewRole("role_a"));
        dbContext.PlatformRoles.Add(NewRole("role_b"));
        dbContext.PlatformRolePermissions.Add(NewGrant("role_a", PlatformAdminPermissionNames.ViewOrganizations));
        dbContext.PlatformRolePermissions.Add(NewGrant("role_b", PlatformAdminPermissionNames.ViewBilling));
        await dbContext.SaveChangesAsync();

        var resolver = new EfPlatformRolePermissionResolver(dbContext);

        var permissions = await resolver.ResolveAsync(["role_a", "role_b"], CancellationToken.None);

        Assert.Equal(
            new[] { PlatformAdminPermissionNames.ViewOrganizations, PlatformAdminPermissionNames.ViewBilling }
                .OrderBy(name => name, StringComparer.Ordinal),
            permissions.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Resolve_GivesEveryPermissionToARoleThatGrantsAll()
    {
        await using var dbContext = NewDbContext();
        dbContext.PlatformRoles.Add(NewRole("full_access_role", grantsAllPermissions: true));
        await dbContext.SaveChangesAsync();

        var resolver = new EfPlatformRolePermissionResolver(dbContext);

        var permissions = await resolver.ResolveAsync(["full_access_role"], CancellationToken.None);

        Assert.Equal(
            PlatformAdminPermissionNames.All.OrderBy(name => name, StringComparer.Ordinal),
            permissions.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Resolve_IgnoresUnknownRoleNames()
    {
        await using var dbContext = NewDbContext();
        dbContext.PlatformRoles.Add(NewRole("known_role"));
        dbContext.PlatformRolePermissions.Add(NewGrant("known_role", PlatformAdminPermissionNames.ViewOrganizations));
        await dbContext.SaveChangesAsync();

        var resolver = new EfPlatformRolePermissionResolver(dbContext);

        var permissions = await resolver.ResolveAsync(["known_role", "ghost_role"], CancellationToken.None);

        Assert.Single(permissions);
        Assert.Contains(PlatformAdminPermissionNames.ViewOrganizations, permissions);
    }

    [Fact]
    public async Task Resolve_ReflectsAPermissionRemovedRightNow()
    {
        await using var dbContext = NewDbContext();
        dbContext.PlatformRoles.Add(NewRole("shrinking_role"));
        dbContext.PlatformRolePermissions.Add(NewGrant("shrinking_role", PlatformAdminPermissionNames.ViewOrganizations));
        dbContext.PlatformRolePermissions.Add(NewGrant("shrinking_role", PlatformAdminPermissionNames.ViewBilling));
        await dbContext.SaveChangesAsync();

        var resolver = new EfPlatformRolePermissionResolver(dbContext);

        // First check: the permission is granted.
        var before = await resolver.ResolveAsync(["shrinking_role"], CancellationToken.None);
        Assert.Contains(PlatformAdminPermissionNames.ViewBilling, before);

        // Take the permission away, exactly the way the admin panel would: delete the row.
        var grant = await dbContext.PlatformRolePermissions.SingleAsync(
            rolePermission =>
                rolePermission.RoleName == "shrinking_role" &&
                rolePermission.PermissionName == PlatformAdminPermissionNames.ViewBilling);
        dbContext.PlatformRolePermissions.Remove(grant);
        await dbContext.SaveChangesAsync();

        // Second check, same resolver, no re-login, no cache to invalidate: the answer changes.
        var after = await resolver.ResolveAsync(["shrinking_role"], CancellationToken.None);
        Assert.DoesNotContain(PlatformAdminPermissionNames.ViewBilling, after);
        Assert.Contains(PlatformAdminPermissionNames.ViewOrganizations, after);
    }

    [Fact]
    public async Task IsKnownRole_AnswersFromTheDatabase()
    {
        await using var dbContext = NewDbContext();
        dbContext.PlatformRoles.Add(NewRole("registered_role"));
        await dbContext.SaveChangesAsync();

        var resolver = new EfPlatformRolePermissionResolver(dbContext);

        Assert.True(await resolver.IsKnownRoleAsync("registered_role", CancellationToken.None));
        Assert.False(await resolver.IsKnownRoleAsync("ghost_role", CancellationToken.None));
    }
}
