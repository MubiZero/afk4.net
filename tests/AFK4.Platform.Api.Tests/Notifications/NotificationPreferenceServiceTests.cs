using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class NotificationPreferenceServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfNotificationPreferenceService CreateService(PlatformDbContext db) =>
        new(db, new FixedTimeProvider(Now));

    private static NotificationRecipient StaffRecipient() =>
        new(Locale: "ru", EmailAddress: "owner@club.example", StaffUserId: StaffId);

    [Fact]
    public async Task IsSuppressed_TransactionalNeverSuppressedEvenWithOptOutRow()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        await service.SetPreferenceAsync(StaffId, null, NotificationCategory.Operational, NotificationChannel.Email, optedOut: true, CancellationToken.None);

        var suppressed = await service.IsSuppressedAsync(StaffRecipient(), NotificationCategory.Transactional, NotificationChannel.Email, CancellationToken.None);

        Assert.False(suppressed);
    }

    [Fact]
    public async Task IsSuppressed_OperationalDefaultsToAllowedWhenNoPreference()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var suppressed = await service.IsSuppressedAsync(StaffRecipient(), NotificationCategory.Operational, NotificationChannel.Email, CancellationToken.None);

        Assert.False(suppressed);
    }

    [Fact]
    public async Task IsSuppressed_OperationalOptedOut()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        await service.SetPreferenceAsync(StaffId, null, NotificationCategory.Operational, NotificationChannel.Email, optedOut: true, CancellationToken.None);

        var suppressed = await service.IsSuppressedAsync(StaffRecipient(), NotificationCategory.Operational, NotificationChannel.Email, CancellationToken.None);

        Assert.True(suppressed);
    }

    [Fact]
    public async Task IsSuppressed_OptOutForDifferentChannelDoesNotAffect()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        await service.SetPreferenceAsync(StaffId, null, NotificationCategory.Operational, NotificationChannel.Sms, optedOut: true, CancellationToken.None);

        var suppressed = await service.IsSuppressedAsync(StaffRecipient(), NotificationCategory.Operational, NotificationChannel.Email, CancellationToken.None);

        Assert.False(suppressed);
    }

    [Fact]
    public async Task IsSuppressed_PlayerOptedOutOfDigest()
    {
        await using var db = CreateDb();
        var service = CreateService(db);
        await service.SetPreferenceAsync(null, PlayerId, NotificationCategory.Digest, NotificationChannel.Email, optedOut: true, CancellationToken.None);
        var player = new NotificationRecipient(Locale: "ru", EmailAddress: "p@club.example", PlayerAccountId: PlayerId);

        var suppressed = await service.IsSuppressedAsync(player, NotificationCategory.Digest, NotificationChannel.Email, CancellationToken.None);

        Assert.True(suppressed);
    }

    [Fact]
    public async Task SetPreference_RejectsOptOutOnTransactionalCategory()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetPreferenceAsync(StaffId, null, NotificationCategory.Transactional, NotificationChannel.Email, optedOut: true, CancellationToken.None));
    }

    [Fact]
    public async Task SetPreference_UpsertsSingleRow()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.SetPreferenceAsync(StaffId, null, NotificationCategory.Operational, NotificationChannel.Email, optedOut: true, CancellationToken.None);
        await service.SetPreferenceAsync(StaffId, null, NotificationCategory.Operational, NotificationChannel.Email, optedOut: false, CancellationToken.None);

        var row = await db.NotificationPreferences.SingleAsync();
        Assert.False(row.OptedOut);
        Assert.Equal(Now, row.UpdatedUtc);
    }
}
