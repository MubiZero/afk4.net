using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class NotificationContactFieldsTests
{
    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task StaffUser_PersistsEmail()
    {
        var staffUserId = Guid.NewGuid();
        await using (var db = CreateDb())
        {
            db.StaffUsers.Add(new StaffUserEntity
            {
                StaffUserId = staffUserId,
                OrganizationId = Guid.NewGuid(),
                UserName = "owner",
                NormalizedUserName = "OWNER",
                DisplayName = "Owner",
                Email = "owner@club.example",
                PasswordHash = "hash",
            });
            await db.SaveChangesAsync();

            var stored = await db.StaffUsers.SingleAsync(user => user.StaffUserId == staffUserId);
            Assert.Equal("owner@club.example", stored.Email);
        }
    }

    [Fact]
    public async Task PlayerAccount_PersistsEmailAndPreferredLocale()
    {
        var playerId = Guid.NewGuid();
        await using var db = CreateDb();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = Guid.NewGuid(),
            HomeBranchId = Guid.NewGuid(),
            DisplayName = "Player One",
            Email = "player@club.example",
            PreferredLocale = "tg",
        });
        await db.SaveChangesAsync();

        var stored = await db.PlayerAccounts.SingleAsync(player => player.PlayerAccountId == playerId);
        Assert.Equal("player@club.example", stored.Email);
        Assert.Equal("tg", stored.PreferredLocale);
    }

    [Fact]
    public async Task PlayerAccount_AllowsNullContactFields()
    {
        await using var db = CreateDb();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            HomeBranchId = Guid.NewGuid(),
            DisplayName = "Legacy Player",
        });
        await db.SaveChangesAsync();

        var stored = await db.PlayerAccounts.SingleAsync();
        Assert.Null(stored.Email);
        Assert.Null(stored.PreferredLocale);
    }
}
