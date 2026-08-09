using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminBootstrapHostedServiceTests
{
    [Fact]
    public async Task StartAsync_WithBootstrapConfigAndEmptyTable_CreatesAdminAndWritesAudit()
    {
        await using var serviceProvider = BuildServiceProvider();
        var options = Options.Create(new PlatformAdminBootstrapOptions
        {
            UserName = "boot-admin@afk4.local",
            DisplayName = "Boot Admin",
            Password = "BootPassw0rd!",
            Roles = [PlatformAdminRoleNames.PlatformAdmin]
        });
        var timeProvider = TimeProvider.System;
        var hostedService = new PlatformAdminBootstrapHostedService(
            serviceProvider,
            options,
            timeProvider,
            NullLogger<PlatformAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var admin = await dbContext.PlatformAdminUsers.SingleAsync();
        Assert.Equal("boot-admin@afk4.local", admin.UserName);
        Assert.Equal("BOOT-ADMIN@AFK4.LOCAL", admin.NormalizedUserName);
        Assert.True(admin.IsActive);
        Assert.Contains("platform_admin", admin.RolesJson);

        var hasher = new PasswordHasher<PlatformAdminUserEntity>();
        Assert.Equal(
            PasswordVerificationResult.Success,
            hasher.VerifyHashedPassword(admin, admin.PasswordHash, "BootPassw0rd!"));

        var audit = await dbContext.AuditRecords.SingleAsync(record => record.Action == "identity.platform_admin.bootstrap");
        Assert.Equal("Succeeded", audit.Outcome);
        Assert.Equal(admin.PlatformAdminUserId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task StartAsync_WithExistingAdmin_DoesNotCreateAnotherAdmin()
    {
        await using var serviceProvider = BuildServiceProvider();
        await using (var seedScope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            dbContext.PlatformAdminUsers.Add(new PlatformAdminUserEntity
            {
                PlatformAdminUserId = Guid.NewGuid(),
                UserName = "existing@afk4.local",
                NormalizedUserName = "EXISTING@AFK4.LOCAL",
                DisplayName = "Existing",
                PasswordHash = "hash",
                RolesJson = "[\"platform_admin\"]",
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var options = Options.Create(new PlatformAdminBootstrapOptions
        {
            UserName = "boot-admin@afk4.local",
            DisplayName = "Boot Admin",
            Password = "BootPassw0rd!",
            Roles = [PlatformAdminRoleNames.PlatformAdmin]
        });
        var hostedService = new PlatformAdminBootstrapHostedService(
            serviceProvider,
            options,
            TimeProvider.System,
            NullLogger<PlatformAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var dbContext2 = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var admins = await dbContext2.PlatformAdminUsers.ToListAsync();
        Assert.Single(admins);
        Assert.Equal("existing@afk4.local", admins[0].UserName);
    }

    [Fact]
    public async Task StartAsync_WithMissingConfiguration_SkipsSeeding()
    {
        await using var serviceProvider = BuildServiceProvider();
        var options = Options.Create(new PlatformAdminBootstrapOptions());
        var hostedService = new PlatformAdminBootstrapHostedService(
            serviceProvider,
            options,
            TimeProvider.System,
            NullLogger<PlatformAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Empty(await dbContext.PlatformAdminUsers.ToListAsync());
        Assert.Empty(await dbContext.AuditRecords.ToListAsync());
    }

    [Fact]
    public async Task StartAsync_WithUnknownRoles_FallsBackToPlatformOwner()
    {
        await using var serviceProvider = BuildServiceProvider();
        var options = Options.Create(new PlatformAdminBootstrapOptions
        {
            UserName = "boot-admin@afk4.local",
            DisplayName = "Boot Admin",
            Password = "BootPassw0rd!",
            Roles = ["ghost_role", "another_ghost_role"]
        });
        var hostedService = new PlatformAdminBootstrapHostedService(
            serviceProvider,
            options,
            TimeProvider.System,
            NullLogger<PlatformAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var admin = await dbContext.PlatformAdminUsers.SingleAsync();
        Assert.Contains("platform_admin", admin.RolesJson);
        Assert.DoesNotContain("ghost_role", admin.RolesJson);
    }

    // Regression for a review finding on task 2: with an unseeded PlatformRoles table,
    // IsKnownRoleAsync answers false for EVERY role name, including real ones, so a configured
    // known role and a configured unknown role were indistinguishable — both fell through to the
    // same platform_admin default and StartAsync_WithBootstrapConfigAndEmptyTable_... passed only
    // because platform_admin happens to be that default too. This test configures a real,
    // non-default role (platform_support) and asserts the bootstrap keeps it as-is instead of
    // silently substituting platform_admin — it fails if IsKnownRoleAsync is broken or the seed
    // above is missing.
    [Fact]
    public async Task StartAsync_WithKnownNonDefaultRole_KeepsTheConfiguredRole()
    {
        await using var serviceProvider = BuildServiceProvider();
        var options = Options.Create(new PlatformAdminBootstrapOptions
        {
            UserName = "boot-support@afk4.local",
            DisplayName = "Boot Support",
            Password = "BootPassw0rd!",
            Roles = [PlatformAdminRoleNames.PlatformSupport]
        });
        var hostedService = new PlatformAdminBootstrapHostedService(
            serviceProvider,
            options,
            TimeProvider.System,
            NullLogger<PlatformAdminBootstrapHostedService>.Instance);

        await hostedService.StartAsync(CancellationToken.None);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var admin = await dbContext.PlatformAdminUsers.SingleAsync();
        Assert.Contains(PlatformAdminRoleNames.PlatformSupport, admin.RolesJson);
        Assert.DoesNotContain(PlatformAdminRoleNames.PlatformAdmin, admin.RolesJson);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IStaffContextAccessor, StaffContextAccessor>();
        services.AddScoped<IAuditRecordStager, AuditRecordStager>();
        services.AddScoped<IAuditRecordWriter, AuditRecordWriter>();
        services.AddScoped<IPlatformRolePermissionResolver, EfPlatformRolePermissionResolver>();
        var serviceProvider = services.BuildServiceProvider();

        // Seed PlatformRoles/PlatformRolePermissions the same way PlatformRoleSeedHostedService
        // would on a real startup, so IsKnownRoleAsync here answers from real data instead of an
        // always-empty table that would make every role name look unknown.
        using (var seedScope = serviceProvider.CreateScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var now = DateTimeOffset.UtcNow;
            foreach (var declaration in PlatformRoleCatalog.Declared)
            {
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
            }

            dbContext.SaveChanges();
        }

        return serviceProvider;
    }
}
