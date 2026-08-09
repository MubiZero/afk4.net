using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformRoleEndpointTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_RequiresAuthentication()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_RequiresManagePlatformAdmins_AndWritesDeniedAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.GetAsync("/api/platform/roles");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.AuditRecords.AnyAsync(record => record.Action == "platform.roles.view"));
    }

    [Fact]
    public async Task Get_ListsBuiltInRolesWithPermissionsAndHolderCount()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var roles = await client.GetFromJsonAsync<PlatformRoleDto[]>("/api/platform/roles");

        Assert.NotNull(roles);
        var admin = roles!.Single(role => role.RoleName == PlatformAdminRoleNames.PlatformAdmin);
        Assert.True(admin.IsBuiltIn);
        Assert.True(admin.GrantsAllPermissions);
        // Роль с полным доступом отдаёт весь список прав из кода, а не сохранённый снимок.
        Assert.Equal(PlatformAdminPermissionNames.All.Count, admin.Permissions.Count);
        Assert.Equal(1, admin.AdminCount);

        var support = roles.Single(role => role.RoleName == PlatformAdminRoleNames.PlatformSupport);
        Assert.Contains(PlatformAdminPermissionNames.ViewOrganizations, support.Permissions);
        Assert.DoesNotContain(PlatformAdminPermissionNames.ManagePlatformAdmins, support.Permissions);
        Assert.Equal(0, support.AdminCount);
    }

    [Fact]
    public async Task GetPermissions_ListsEveryPermissionFromCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var permissions = await client.GetFromJsonAsync<string[]>("/api/platform/permissions");

        Assert.Equal(
            PlatformAdminPermissionNames.All.OrderBy(name => name, StringComparer.Ordinal),
            permissions!.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Post_CreatesACustomRole()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PostAsJsonAsync("/api/platform/roles", new CreatePlatformRoleRequest(
            "billing_clerk", "Бухгалтер", "Смотрит счета",
            [PlatformAdminPermissionNames.ViewOrganizations, PlatformAdminPermissionNames.ViewBilling]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PlatformRoleDto>();
        Assert.False(created!.IsBuiltIn);
        Assert.False(created.GrantsAllPermissions);
        Assert.Equal(2, created.Permissions.Count);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.AuditRecords.AnyAsync(record => record.Action == "platform.roles.create"));
    }

    [Fact]
    public async Task Post_RejectsADuplicateRoleName()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PostAsJsonAsync("/api/platform/roles", new CreatePlatformRoleRequest(
            PlatformAdminRoleNames.PlatformSupport, "Дубль", "", [PlatformAdminPermissionNames.ViewOrganizations]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PlatformRoleErrorCodes.RoleNameTaken, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Post_RejectsAnUnknownPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PostAsJsonAsync("/api/platform/roles", new CreatePlatformRoleRequest(
            "ghost", "Призрак", "", ["platform.everything.rule"]));

        // Право, которого нет в коде, никто не проверяет: выдать его значит нарисовать доступ,
        // которого не существует.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PlatformRoleErrorCodes.UnknownPermission, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Post_RefusesToGrantAPermissionTheActorLacks()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var actor = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "narrow", [PlatformAdminPermissionNames.ManagePlatformAdmins]);
            var row = await db.PlatformAdminUsers.SingleAsync(admin => admin.PlatformAdminUserId == actor.PlatformAdminId);
            row.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["narrow"]);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/platform/roles", new CreatePlatformRoleRequest(
            "wide", "Широкая", "", [PlatformAdminPermissionNames.ManageInvoices]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(PlatformRoleErrorCodes.PermissionNotHeldByActor, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Put_ChangesPermissionsOfACustomRole()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "clerk", [PlatformAdminPermissionNames.ViewOrganizations]);
        }

        var response = await client.PutAsJsonAsync("/api/platform/roles/clerk", new UpdatePlatformRoleRequest(
            "Клерк", "Обновлено",
            [PlatformAdminPermissionNames.ViewOrganizations, PlatformAdminPermissionNames.ViewBilling]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PlatformRoleDto>();
        Assert.Equal(2, updated!.Permissions.Count);
        Assert.Equal("Обновлено", updated.Description);
    }

    [Fact]
    public async Task Put_CanEditABuiltInRole()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PutAsJsonAsync($"/api/platform/roles/{PlatformAdminRoleNames.PlatformSupport}",
            new UpdatePlatformRoleRequest("Поддержка", "Урезанная",
                [PlatformAdminPermissionNames.ViewOrganizations]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PlatformRoleDto>();
        Assert.Single(updated!.Permissions);
        Assert.True(updated.IsBuiltIn);
    }

    [Fact]
    public async Task Put_RefusesToRemoveAdminsManageFromARoleTheActorHolds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var actor = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "keeper",
                [PlatformAdminPermissionNames.ManagePlatformAdmins, PlatformAdminPermissionNames.ViewOrganizations]);
            // Вторая роль с ключом — чтобы сработал именно предохранитель самоблокировки,
            // а не «последняя роль с управлением администраторами».
            await SeedRoleAsync(db, "spare_keeper", [PlatformAdminPermissionNames.ManagePlatformAdmins]);
            var row = await db.PlatformAdminUsers.SingleAsync(admin => admin.PlatformAdminUserId == actor.PlatformAdminId);
            row.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["keeper"]);
            await db.SaveChangesAsync();
        }

        var response = await client.PutAsJsonAsync("/api/platform/roles/keeper", new UpdatePlatformRoleRequest(
            "keeper", "", [PlatformAdminPermissionNames.ViewOrganizations]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PlatformRoleErrorCodes.SelfLockout, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Delete_RemovesACustomRoleThatNobodyHolds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "temporary", [PlatformAdminPermissionNames.ViewOrganizations]);
        }

        var response = await client.DeleteAsync("/api/platform/roles/temporary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await verifyDb.PlatformRoles.AnyAsync(role => role.RoleName == "temporary"));
        Assert.False(await verifyDb.PlatformRolePermissions.AnyAsync(rolePermission => rolePermission.RoleName == "temporary"));
    }

    [Fact]
    public async Task Delete_RefusesABuiltInRole()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.DeleteAsync($"/api/platform/roles/{PlatformAdminRoleNames.PlatformSupport}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PlatformRoleErrorCodes.BuiltInRole, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task Delete_RefusesARoleThatSomebodyHolds()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "worn", [PlatformAdminPermissionNames.ViewOrganizations]);
            db.PlatformAdminUsers.Add(new PlatformAdminUserEntity
            {
                PlatformAdminUserId = Guid.NewGuid(),
                UserName = "wearer",
                NormalizedUserName = "WEARER",
                DisplayName = "Носитель",
                PasswordHash = "x",
                RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["worn"]),
                IsActive = true,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync("/api/platform/roles/worn");

        // Иначе человек молча остаётся без прав и понять причину невозможно.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(PlatformRoleErrorCodes.RoleInUse, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task PermissionRemoval_TakesEffectWithoutRelogin()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var actor = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "watcher",
                [PlatformAdminPermissionNames.ViewOrganizations, PlatformAdminPermissionNames.ManagePlatformAdmins]);
            var row = await db.PlatformAdminUsers.SingleAsync(admin => admin.PlatformAdminUserId == actor.PlatformAdminId);
            row.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["watcher"]);
            await db.SaveChangesAsync();
        }

        var before = await client.GetAsync("/api/platform/organizations");
        Assert.NotEqual(HttpStatusCode.Forbidden, before.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var granted = await db.PlatformRolePermissions.SingleAsync(rolePermission =>
                rolePermission.RoleName == "watcher"
                && rolePermission.PermissionName == PlatformAdminPermissionNames.ViewOrganizations);
            db.PlatformRolePermissions.Remove(granted);
            await db.SaveChangesAsync();
        }

        // Тот же токен, никакого перелогина — и права уже другие. Ради этого весь переезд.
        var after = await client.GetAsync("/api/platform/organizations");
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task Post_StoresPermissionsInTheSpellingUsedByTheCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PostAsJsonAsync("/api/platform/roles", new CreatePlatformRoleRequest(
            "loud", "Крикун", "", [PlatformAdminPermissionNames.ViewBilling.ToUpperInvariant()]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await db.PlatformRolePermissions.SingleAsync(rolePermission => rolePermission.RoleName == "loud");
        // Регистр приводится к коду: иначе строка живёт как «почти» право и любое посимвольное
        // сравнение считает её другой.
        Assert.Equal(PlatformAdminPermissionNames.ViewBilling, stored.PermissionName);
    }

    [Fact]
    public async Task Put_KeepsAPermissionWhoseStoredSpellingDiffersInCase()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var actor = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "keeper",
                [
                    PlatformAdminPermissionNames.ManagePlatformAdmins.ToUpperInvariant(),
                    PlatformAdminPermissionNames.ViewOrganizations
                ]);
            var row = await db.PlatformAdminUsers.SingleAsync(admin => admin.PlatformAdminUserId == actor.PlatformAdminId);
            row.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["keeper"]);
            await db.SaveChangesAsync();
        }

        var response = await client.PutAsJsonAsync("/api/platform/roles/keeper", new UpdatePlatformRoleRequest(
            "Хранитель", "",
            [PlatformAdminPermissionNames.ManagePlatformAdmins, PlatformAdminPermissionNames.ViewOrganizations]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await verifyDb.PlatformRolePermissions
            .Where(rolePermission => rolePermission.RoleName == "keeper")
            .Select(rolePermission => rolePermission.PermissionName)
            .ToArrayAsync();

        // Ключ от платформы был в списке, который прислали, — значит он обязан остаться. Сравнение
        // по буквам вместо сравнения без учёта регистра тихо снимало бы его мимо предохранителя
        // самоблокировки.
        Assert.Contains(stored, permission => string.Equals(
            permission, PlatformAdminPermissionNames.ManagePlatformAdmins, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Put_FindsTheRoleWhateverCaseTheUrlUses()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "clerk", [PlatformAdminPermissionNames.ViewOrganizations]);
        }

        var response = await client.PutAsJsonAsync("/api/platform/roles/CLERK", new UpdatePlatformRoleRequest(
            "Клерк", "", [PlatformAdminPermissionNames.ViewOrganizations]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_WritesAuditWhenTheActorTriesToGrantAPermissionTheyLack()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var actor = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await SeedRoleAsync(db, "narrow", [PlatformAdminPermissionNames.ManagePlatformAdmins]);
            var row = await db.PlatformAdminUsers.SingleAsync(admin => admin.PlatformAdminUserId == actor.PlatformAdminId);
            row.RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(["narrow"]);
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/platform/roles", new CreatePlatformRoleRequest(
            "wide", "Широкая", "", [PlatformAdminPermissionNames.ManageInvoices]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope2 = factory.Services.CreateAsyncScope();
        var verifyDb = scope2.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Попытка расширить себе доступ обязана остаться в следе, а не только в красной плашке.
        var record = await verifyDb.AuditRecords.SingleAsync(entry => entry.Action == "platform.roles.create");
        Assert.Equal("Denied", record.Outcome);
        Assert.Contains(PlatformRoleErrorCodes.PermissionNotHeldByActor, record.DetailsJson);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task SeedRoleAsync(PlatformDbContext db, string roleName, IReadOnlyList<string> permissions)
    {
        db.PlatformRoles.Add(new PlatformRoleEntity
        {
            RoleName = roleName,
            DisplayName = roleName,
            Description = string.Empty,
            IsBuiltIn = false,
            GrantsAllPermissions = false,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

        foreach (var permission in permissions)
        {
            db.PlatformRolePermissions.Add(new PlatformRolePermissionEntity
            {
                PlatformRolePermissionId = Guid.NewGuid(),
                RoleName = roleName,
                PermissionName = permission
            });
        }

        await db.SaveChangesAsync();
    }
}
