using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class PlatformRoleSeedHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<PlatformRoleSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Добавляет только недостающие объявленные роли и никогда не трогает существующую строку:
        // после создания авторитетна панель, и сидер, переписывающий имя/описание/права на каждом
        // рестарте, молча откатывал бы осознанную правку. Раннего выхода «таблица непустая» тоже
        // нет: база из продакшена должна получать новые встроенные роли следующим деплоем.
        var existingRoleNames = await dbContext.PlatformRoles
            .Select(role => role.RoleName)
            .ToHashSetAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var declaration in PlatformRoleCatalog.Declared)
        {
            if (existingRoleNames.Contains(declaration.RoleName))
            {
                continue;
            }

            dbContext.PlatformRoles.Add(new PlatformRoleEntity
            {
                RoleName = declaration.RoleName,
                DisplayName = declaration.DisplayName,
                Description = declaration.Description,
                IsBuiltIn = true,
                GrantsAllPermissions = declaration.GrantsAllPermissions,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            foreach (var permissionName in declaration.Permissions)
            {
                dbContext.PlatformRolePermissions.Add(new PlatformRolePermissionEntity
                {
                    PlatformRolePermissionId = Guid.NewGuid(),
                    RoleName = declaration.RoleName,
                    PermissionName = permissionName
                });
            }

            added++;
        }

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Platform role seed: added {Added} missing declared roles.", added);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
