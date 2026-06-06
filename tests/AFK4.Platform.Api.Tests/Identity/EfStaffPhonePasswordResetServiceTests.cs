using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class EfStaffPhonePasswordResetServiceTests
{
    private const string Phone = "992937380070";
    private const string OldPassword = "OldPassw0rd!";
    private const string NewPassword = "NewPassw0rd!";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-06T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<(Guid StaffUserId, Guid OrgId)> SeedVerifiedStaffAsync(
        PlatformDbContext db, bool verified = true, bool active = true)
    {
        var staffUserId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var staff = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = orgId,
            UserName = "owner",
            NormalizedUserName = "OWNER",
            DisplayName = "Owner",
            IsActive = active,
            Phone = "+" + Phone,
            NormalizedPhone = Phone,
            PhoneVerifiedAtUtc = verified ? Now.AddDays(-1) : null,
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, OldPassword);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        return (staffUserId, orgId);
    }

    private static (EfStaffPhonePasswordResetService Service, CapturingNotificationService Notifications, FixedTimeProvider Time)
        CreateService(PlatformDbContext db, string fixedCode = "424242")
    {
        var notifications = new CapturingNotificationService();
        var time = new FixedTimeProvider(Now);
        var service = new EfStaffPhonePasswordResetService(
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
    public async Task Request_VerifiedPhone_StoresResetOtp_AndSendsSms()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, notifications, _) = CreateService(db, fixedCode: "424242");

        var result = await service.RequestResetAsync("+992 93 738-00-70", CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.Accepted, result.Status);
        var otp = Assert.Single(await db.StaffPhoneOtps.ToListAsync());
        Assert.Equal(StaffPhoneOtpPurpose.PasswordReset, otp.Purpose);
        var request = Assert.Single(notifications.SentNow);
        Assert.Equal(NotificationTemplateKeys.StaffPasswordResetSms, request.TemplateKey);
        Assert.Equal("424242", request.Tokens["code"]);
        Assert.Equal("+992937380070", request.Recipient.PhoneNumber);
        Assert.Contains(NotificationChannel.Sms, request.PreferredChannels!);
    }

    [Fact]
    public async Task Request_UnknownPhone_IsAccepted_ButSendsNothing()
    {
        await using var db = CreateDb();
        var (service, notifications, _) = CreateService(db);

        var result = await service.RequestResetAsync(Phone, CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.Accepted, result.Status);
        Assert.Empty(await db.StaffPhoneOtps.ToListAsync());
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Request_UnverifiedPhone_IsAccepted_ButSendsNothing()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db, verified: false);
        var (service, notifications, _) = CreateService(db);

        var result = await service.RequestResetAsync(Phone, CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.Accepted, result.Status);
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Request_InvalidPhoneFormat_ReturnsInvalidPhone()
    {
        await using var db = CreateDb();
        var (service, notifications, _) = CreateService(db);

        var result = await service.RequestResetAsync("12345", CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.InvalidPhone, result.Status);
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Reset_CorrectCode_SetsNewPassword_ConsumesOtp_RevokesTokens()
    {
        await using var db = CreateDb();
        var (staffUserId, orgId) = await SeedVerifiedStaffAsync(db);
        // StaffAccessTokenEntity: PK = StaffAccessTokenId, TokenHash = byte[]
        db.StaffAccessTokens.Add(new StaffAccessTokenEntity
        {
            StaffAccessTokenId = Guid.NewGuid(),
            OrganizationId = orgId,
            StaffUserId = staffUserId,
            TokenHash = "ah"u8.ToArray(),
            CreatedAtUtc = Now.AddHours(-1),
            ExpiresAtUtc = Now.AddHours(7),
        });
        // StaffRefreshTokenEntity: PK = StaffRefreshTokenId, TokenHash = byte[]
        db.StaffRefreshTokens.Add(new StaffRefreshTokenEntity
        {
            StaffRefreshTokenId = Guid.NewGuid(),
            OrganizationId = orgId,
            StaffUserId = staffUserId,
            TokenHash = "rh"u8.ToArray(),
            CreatedAtUtc = Now.AddHours(-1),
            ExpiresAtUtc = Now.AddDays(30),
        });
        await db.SaveChangesAsync();
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);

        var result = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.Success, result.Status);
        var staff = await db.StaffUsers.SingleAsync(u => u.StaffUserId == staffUserId);
        var hasher = new PasswordHasher<StaffUserEntity>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(staff, staff.PasswordHash, NewPassword));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(staff, staff.PasswordHash, OldPassword));
        Assert.NotNull((await db.StaffPhoneOtps.SingleAsync()).ConsumedAtUtc);
        Assert.All(await db.StaffAccessTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAtUtc));
        Assert.All(await db.StaffRefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task Reset_WrongCode_IncrementsAttempt_ReturnsRemaining()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);

        var result = await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.InvalidCode, result.Status);
        Assert.Equal(2, result.RemainingAttempts);
        Assert.Equal(1, (await db.StaffPhoneOtps.SingleAsync()).AttemptCount);
    }

    [Fact]
    public async Task Reset_TooManyWrongAttempts_LocksOut()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);

        await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);
        await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);
        await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);
        var afterLockout = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.TooManyAttempts, afterLockout.Status);
    }

    [Fact]
    public async Task Reset_ExpiredCode_ReturnsExpired()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, time) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);
        time.Now = Now.AddMinutes(10);

        var result = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.Expired, result.Status);
    }

    [Fact]
    public async Task Reset_NoCodeRequested_ReturnsNoActiveCode()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, _) = CreateService(db, fixedCode: "424242");

        var result = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.NoActiveCode, result.Status);
    }

    // ---- test doubles (mirror StaffPhoneVerificationServiceTests) ----

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
