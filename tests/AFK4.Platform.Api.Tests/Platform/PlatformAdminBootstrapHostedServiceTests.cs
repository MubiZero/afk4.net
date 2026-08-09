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
        return services.BuildServiceProvider();
    }
}
