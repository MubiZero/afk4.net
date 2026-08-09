using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Identity;

/// <summary>
/// Creates the initial platform admin from configuration on startup when the
/// platform_admin_users table is empty. Subsequent rotations happen through
/// platform-admin APIs, not configuration.
/// </summary>
public sealed class PlatformAdminBootstrapHostedService(
    IServiceProvider serviceProvider,
    IOptions<PlatformAdminBootstrapOptions> options,
    TimeProvider timeProvider,
    ILogger<PlatformAdminBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrap = options.Value;
        if (string.IsNullOrWhiteSpace(bootstrap.UserName) ||
            string.IsNullOrWhiteSpace(bootstrap.Password) ||
            string.IsNullOrWhiteSpace(bootstrap.DisplayName))
        {
            logger.LogInformation(
                "Platform admin bootstrap configuration is empty; skipping initial admin seed.");
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var auditRecordWriter = scope.ServiceProvider.GetRequiredService<IAuditRecordWriter>();
        var rolePermissionResolver = scope.ServiceProvider.GetRequiredService<IPlatformRolePermissionResolver>();

        var configuredRoleNames = (bootstrap.Roles ?? [PlatformAdminRoleNames.PlatformAdmin])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roleNames = new List<string>();
        foreach (var roleName in configuredRoleNames)
        {
            if (await rolePermissionResolver.IsKnownRoleAsync(roleName, cancellationToken))
            {
                roleNames.Add(roleName);
            }
        }

        if (roleNames.Count == 0)
        {
            logger.LogWarning(
                "Platform admin bootstrap configuration listed no known roles; defaulting to platform_admin.");
            roleNames = [PlatformAdminRoleNames.PlatformAdmin];
        }

        var hasAnyAdmin = await dbContext.PlatformAdminUsers.AnyAsync(cancellationToken);
        if (hasAnyAdmin)
        {
            logger.LogInformation(
                "Platform admin table already populated; bootstrap admin will not be created.");
            return;
        }

        var hasher = new PasswordHasher<PlatformAdminUserEntity>();
        var now = timeProvider.GetUtcNow();
        var admin = new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = bootstrap.UserName.Trim(),
            NormalizedUserName = bootstrap.UserName.Trim().ToUpperInvariant(),
            DisplayName = bootstrap.DisplayName.Trim(),
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles(roleNames),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        admin.PasswordHash = hasher.HashPassword(admin, bootstrap.Password);

        dbContext.PlatformAdminUsers.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
            OrganizationId: Guid.Empty,
            BranchId: null,
            ActorStaffUserId: null,
            Action: AuditActionNames.PlatformAdminBootstrap,
            TargetType: "PlatformAdminUser",
            TargetId: admin.PlatformAdminUserId.ToString("D"),
            Outcome: AuditOutcome.Succeeded,
            SourceApp: "PlatformApi",
            DetailsJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                admin.UserName,
                Roles = roleNames
            })), cancellationToken);

        logger.LogInformation(
            "Bootstrap platform admin '{UserName}' created with roles [{Roles}].",
            admin.UserName,
            string.Join(", ", roleNames));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
