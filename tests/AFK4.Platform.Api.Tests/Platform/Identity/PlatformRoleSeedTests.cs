using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Tests.Platform.Identity;

public sealed class PlatformRoleSeedTests
{
    // The test host strips every IHostedService registration (see PlatformApiFactory.SharedAppHost)
    // to keep timer-driven jobs off the shared host, so it can't be resolved via
    // GetServices<IHostedService>() here — construct it directly instead, mirroring
    // FeatureCatalogSeedTests.
    private static PlatformRoleSeedHostedService NewSeeder(IServiceProvider services) =>
        new(
            services,
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<PlatformRoleSeedHostedService>>());

    [Fact]
    public async Task Seed_CreatesBothBuiltInRoles()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var roles = await db.PlatformRoles.AsNoTracking().ToListAsync();

        Assert.Contains(roles, role => role.RoleName == PlatformAdminRoleNames.PlatformAdmin);
        Assert.Contains(roles, role => role.RoleName == PlatformAdminRoleNames.PlatformSupport);
    }

    [Fact]
    public async Task Seed_GivesSupportExactlyTodaysPermissions()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var supportPermissions = await db.PlatformRolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleName == PlatformAdminRoleNames.PlatformSupport)
            .Select(rolePermission => rolePermission.PermissionName)
            .ToListAsync();

        // Девять прав, перечисленных явно здесь (не через PlatformRoleCatalog), чтобы тест
        // действительно сверял сидер с сегодняшним поведением, а не объявление само с собой.
        string[] expectedPermissions =
        [
            "platform.support.access",
            "platform.organizations.view",
            "platform.organizations.status.update",
            "platform.organizations.support_notes.view",
            "platform.organizations.support_notes.manage",
            "platform.organizations.owner_invites.manage",
            "platform.organizations.health.view",
            "platform.audit.view",
            "platform.health.view"
        ];

        Assert.Equal(
            expectedPermissions.OrderBy(name => name, StringComparer.Ordinal),
            supportPermissions.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Seed_MarksAdminRoleAsGrantingAllPermissions()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var adminRole = await db.PlatformRoles.AsNoTracking()
            .SingleAsync(role => role.RoleName == PlatformAdminRoleNames.PlatformAdmin);

        Assert.True(adminRole.GrantsAllPermissions);
        Assert.True(adminRole.IsBuiltIn);
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteAnExistingRole()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var supportRole = await db.PlatformRoles
            .SingleAsync(role => role.RoleName == PlatformAdminRoleNames.PlatformSupport);
        supportRole.Description = "Отредактировано в панели";
        await db.SaveChangesAsync();

        var existingPermission = await db.PlatformRolePermissions
            .SingleAsync(rolePermission =>
                rolePermission.RoleName == PlatformAdminRoleNames.PlatformSupport &&
                rolePermission.PermissionName == PlatformAdminPermissionNames.ViewOrganizations);
        db.PlatformRolePermissions.Remove(existingPermission);
        await db.SaveChangesAsync();

        var seeder = NewSeeder(scope.ServiceProvider);
        await seeder.StartAsync(CancellationToken.None);

        // Панель авторитетна после создания строки: повторный старт не откатывает осознанную
        // правку ни описания роли, ни её набора прав.
        var reloadedRole = await db.PlatformRoles.AsNoTracking()
            .SingleAsync(role => role.RoleName == PlatformAdminRoleNames.PlatformSupport);
        Assert.Equal("Отредактировано в панели", reloadedRole.Description);

        var reloadedPermissions = await db.PlatformRolePermissions.AsNoTracking()
            .Where(rolePermission => rolePermission.RoleName == PlatformAdminRoleNames.PlatformSupport)
            .Select(rolePermission => rolePermission.PermissionName)
            .ToListAsync();
        Assert.DoesNotContain(PlatformAdminPermissionNames.ViewOrganizations, reloadedPermissions);
    }
}
