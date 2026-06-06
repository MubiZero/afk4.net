using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class StaffPhoneColumnsRoundTripTests
{
    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task StaffUser_PersistsPhoneFields()
    {
        await using var db = CreateDb();
        var staffUserId = Guid.NewGuid();
        var verifiedAt = DateTimeOffset.Parse("2026-06-05T10:00:00Z");

        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = Guid.NewGuid(),
            UserName = "owner",
            NormalizedUserName = "OWNER",
            DisplayName = "Owner",
            PasswordHash = "x",
            Phone = "+992937380070",
            NormalizedPhone = "992937380070",
            PhoneVerifiedAtUtc = verifiedAt,
        });
        await db.SaveChangesAsync();

        var loaded = await db.StaffUsers.SingleAsync(user => user.StaffUserId == staffUserId);
        Assert.Equal("+992937380070", loaded.Phone);
        Assert.Equal("992937380070", loaded.NormalizedPhone);
        Assert.Equal(verifiedAt, loaded.PhoneVerifiedAtUtc);
    }
}
