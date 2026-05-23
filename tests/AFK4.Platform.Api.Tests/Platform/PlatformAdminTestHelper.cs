using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

internal static class PlatformAdminTestHelper
{
    public const string DefaultUserName = "owner@platform.test";
    public const string DefaultDisplayName = "Platform Owner";
    public const string DefaultPassword = "Passw0rd!";

    public static async Task<PlatformAdminUserEntity> SeedPlatformAdminAsync(
        PlatformApiFactory factory,
        string userName = DefaultUserName,
        string displayName = DefaultDisplayName,
        string password = DefaultPassword,
        bool isActive = true,
        IEnumerable<string>? roles = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hasher = new PasswordHasher<PlatformAdminUserEntity>();
        var roleNames = (roles ?? [PlatformAdminRoleNames.PlatformOwner]).ToArray();
        var now = DateTimeOffset.Parse("2026-05-23T08:00:00Z");
        var admin = new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = displayName,
            RolesJson = JsonSerializer.Serialize(roleNames),
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        dbContext.PlatformAdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();
        return admin;
    }
}
