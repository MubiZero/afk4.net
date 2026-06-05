using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class StaffPhoneVerificationServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-05T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedStaffAsync(PlatformDbContext db, Guid orgId)
    {
        var staffUserId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = orgId,
            UserName = "owner",
            NormalizedUserName = "OWNER",
            DisplayName = "Owner",
            PasswordHash = "x",
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return staffUserId;
    }

    private static (EfStaffPhoneVerificationService Service, CapturingNotificationService Notifications, FixedTimeProvider Time)
        CreateService(PlatformDbContext db, string fixedCode = "123456")
    {
        var notifications = new CapturingNotificationService();
        var time = new FixedTimeProvider(Now);
        var service = new EfStaffPhoneVerificationService(
            db,
            notifications,
            new Sha256PhoneOtpHasher(),
            new FixedPhoneOtpGenerator(fixedCode),
            time,
            Options.Create(new PhoneOtpOptions()),
            Options.Create(new NotificationOptions { DefaultLocale = "ru" }));
        return (service, notifications, time);
    }

    [Fact]
    public async Task Start_StoresOtp_AndSendsSmsWithCode()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, notifications, _) = CreateService(db, fixedCode: "424242");

        var result = await service.StartAsync(staffUserId, orgId, "+992 93 738-00-70", CancellationToken.None);

        Assert.Equal(PhoneVerificationStartStatus.Sent, result.Status);
        Assert.Equal(1, await db.StaffPhoneOtps.CountAsync());
        var request = Assert.Single(notifications.SentNow);
        Assert.Equal(NotificationTemplateKeys.StaffPhoneVerification, request.TemplateKey);
        Assert.Equal("424242", request.Tokens["code"]);
        Assert.Equal("+992937380070", request.Recipient.PhoneNumber);
        Assert.NotNull(request.PreferredChannels);
        Assert.Contains(NotificationChannel.Sms, request.PreferredChannels!);
    }

    [Fact]
    public async Task Start_InvalidPhone_ReturnsInvalid_NoOtp()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, notifications, _) = CreateService(db);

        var result = await service.StartAsync(staffUserId, orgId, "12345", CancellationToken.None);

        Assert.Equal(PhoneVerificationStartStatus.InvalidPhone, result.Status);
        Assert.Equal(0, await db.StaffPhoneOtps.CountAsync());
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Start_WithinCooldown_ReturnsCooldown()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, _) = CreateService(db);

        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);
        var second = await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        Assert.Equal(PhoneVerificationStartStatus.CooldownActive, second.Status);
        Assert.True(second.ResendAfterSeconds > 0);
        Assert.Equal(1, await db.StaffPhoneOtps.CountAsync());
    }

    [Fact]
    public async Task Confirm_CorrectCode_MarksPhoneVerified_ConsumesOtp()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        var result = await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.Confirmed, result.Status);
        var staff = await db.StaffUsers.SingleAsync(user => user.StaffUserId == staffUserId);
        Assert.Equal("992937380070", staff.NormalizedPhone);
        Assert.Equal("+992937380070", staff.Phone);
        Assert.NotNull(staff.PhoneVerifiedAtUtc);
        var otp = await db.StaffPhoneOtps.SingleAsync();
        Assert.NotNull(otp.ConsumedAtUtc);
    }

    [Fact]
    public async Task Confirm_WrongCode_IncrementsAttempt_ThenLocksOut()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.InvalidCode, (await service.ConfirmAsync(staffUserId, "000000", CancellationToken.None)).Status);
        Assert.Equal(PhoneConfirmStatus.InvalidCode, (await service.ConfirmAsync(staffUserId, "000000", CancellationToken.None)).Status);
        Assert.Equal(PhoneConfirmStatus.InvalidCode, (await service.ConfirmAsync(staffUserId, "000000", CancellationToken.None)).Status);
        // 4th attempt: attempts exhausted, even the correct code is rejected.
        Assert.Equal(PhoneConfirmStatus.TooManyAttempts, (await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Confirm_ExpiredCode_ReturnsExpired()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, time) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);
        time.Now = Now.AddMinutes(10);

        var result = await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.Expired, result.Status);
    }

    [Fact]
    public async Task Confirm_PhoneAlreadyVerifiedByAnother_ReturnsConflict()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserName = "other",
            NormalizedUserName = "OTHER",
            DisplayName = "Other",
            PasswordHash = "x",
            IsActive = true,
            NormalizedPhone = "992937380070",
            Phone = "+992937380070",
            PhoneVerifiedAtUtc = Now.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        var result = await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.PhoneAlreadyInUse, result.Status);
    }

    // ---- test doubles ----

    private sealed class FixedPhoneOtpGenerator(string code) : IPhoneOtpGenerator
    {
        public string Generate() => code;
    }

    private sealed class CapturingNotificationService : INotificationService
    {
        public List<NotificationRequest> SentNow { get; } = [];

        public Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NotificationHandle([Guid.NewGuid()], Created: true));

        public Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken)
        {
            SentNow.Add(request);
            return Task.FromResult(new NotificationDeliveryResult(
                new NotificationHandle([Guid.NewGuid()], Created: true), Delivered: true, Error: null));
        }
    }
}
