using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class StaffPasswordResetServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
    private static readonly Guid OrgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedStaffAsync(PlatformDbContext db, string userName = "owner", string? email = "owner@club.example", string password = "OldPassw0rd")
    {
        var staffUserId = Guid.NewGuid();
        var staff = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = OrgId,
            UserName = userName,
            NormalizedUserName = userName.Trim().ToUpperInvariant(),
            DisplayName = "Club Owner",
            Email = email,
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, password);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        return staffUserId;
    }

    private static (EfStaffPasswordResetService Service, CapturingNotificationService Notifications, FixedTimeProvider Time) CreateService(PlatformDbContext db)
    {
        var notifications = new CapturingNotificationService();
        var time = new FixedTimeProvider(Now);
        var service = new EfStaffPasswordResetService(
            db,
            notifications,
            new RandomPhoneOtpGenerator(),
            time,
            Options.Create(new PhoneOtpOptions()),
            Options.Create(new NotificationOptions { DefaultLocale = "ru" }));
        return (service, notifications, time);
    }

    [Fact]
    public async Task RequestReset_IssuesTokenAndSendsTransactionalEmail()
    {
        await using var db = CreateDb();
        var staffUserId = await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);

        await service.RequestResetAsync("owner", CancellationToken.None);

        Assert.Equal(1, await db.PasswordResetTokens.CountAsync());
        var request = Assert.Single(notifications.SentNow);
        Assert.Equal(NotificationTemplateKeys.StaffPasswordReset, request.TemplateKey);
        Assert.Equal(NotificationCategory.Transactional, request.Category);
        Assert.Equal("owner@club.example", request.Recipient.EmailAddress);
        Assert.Equal(staffUserId, request.Recipient.StaffUserId);
        Assert.True(request.Tokens.ContainsKey("code"));
        Assert.Equal("Club Owner", request.Tokens["displayName"]);
    }

    [Fact]
    public async Task RequestReset_ResolvesByEmail()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);

        await service.RequestResetAsync("owner@club.example", CancellationToken.None);

        Assert.Single(notifications.SentNow);
    }

    [Fact]
    public async Task RequestReset_UnknownUser_DoesNothing()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);

        await service.RequestResetAsync("ghost", CancellationToken.None);

        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task RequestReset_UserWithoutEmail_DoesNotSend()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db, email: null);
        var (service, notifications, _) = CreateService(db);

        await service.RequestResetAsync("owner", CancellationToken.None);

        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Reset_ValidCode_SetsNewPasswordAndConsumesToken()
    {
        await using var db = CreateDb();
        var staffUserId = await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);
        await service.RequestResetAsync("owner", CancellationToken.None);
        var code = notifications.SentNow.Single().Tokens["code"];

        var result = await service.ResetAsync("owner", code, "BrandNewPass1", CancellationToken.None);

        Assert.Equal(ResetPasswordByEmailStatus.Success, result.Status);
        var token = await db.PasswordResetTokens.SingleAsync();
        Assert.NotNull(token.ConsumedAtUtc);
        var staff = await db.StaffUsers.SingleAsync(user => user.StaffUserId == staffUserId);
        var verification = new PasswordHasher<StaffUserEntity>().VerifyHashedPassword(staff, staff.PasswordHash, "BrandNewPass1");
        Assert.Equal(PasswordVerificationResult.Success, verification);
    }

    [Fact]
    public async Task Reset_CodeIsSixDigits()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);
        await service.RequestResetAsync("owner", CancellationToken.None);

        var code = notifications.SentNow.Single().Tokens["code"];
        Assert.Matches("^[0-9]{6}$", code);
    }

    [Fact]
    public async Task Reset_ResolvesByEmail()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);
        await service.RequestResetAsync("owner@club.example", CancellationToken.None);
        var code = notifications.SentNow.Single().Tokens["code"];

        var result = await service.ResetAsync("owner@club.example", code, "BrandNewPass1", CancellationToken.None);

        Assert.Equal(ResetPasswordByEmailStatus.Success, result.Status);
    }

    [Fact]
    public async Task Reset_IsSingleUse()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);
        await service.RequestResetAsync("owner", CancellationToken.None);
        var code = notifications.SentNow.Single().Tokens["code"];

        Assert.Equal(ResetPasswordByEmailStatus.Success,
            (await service.ResetAsync("owner", code, "BrandNewPass1", CancellationToken.None)).Status);
        Assert.Equal(ResetPasswordByEmailStatus.NoActiveCode,
            (await service.ResetAsync("owner", code, "AnotherPass2", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Reset_ExpiredCode_Fails()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, time) = CreateService(db);
        await service.RequestResetAsync("owner", CancellationToken.None);
        var code = notifications.SentNow.Single().Tokens["code"];
        time.Now = Now.AddMinutes(20);

        var result = await service.ResetAsync("owner", code, "BrandNewPass1", CancellationToken.None);
        Assert.Equal(ResetPasswordByEmailStatus.Expired, result.Status);
    }

    [Fact]
    public async Task Reset_NoPendingCode_Fails()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, _, _) = CreateService(db);

        var result = await service.ResetAsync("owner", "000000", "BrandNewPass1", CancellationToken.None);
        Assert.Equal(ResetPasswordByEmailStatus.NoActiveCode, result.Status);
    }

    [Fact]
    public async Task Reset_WrongCode_DecrementsRemainingAttemptsThenLocksOut()
    {
        await using var db = CreateDb();
        await SeedStaffAsync(db);
        var (service, notifications, _) = CreateService(db);
        await service.RequestResetAsync("owner", CancellationToken.None);
        var realCode = notifications.SentNow.Single().Tokens["code"];
        var wrongCode = realCode == "000000" ? "111111" : "000000";

        var first = await service.ResetAsync("owner", wrongCode, "BrandNewPass1", CancellationToken.None);
        Assert.Equal(ResetPasswordByEmailStatus.InvalidCode, first.Status);
        Assert.Equal(2, first.RemainingAttempts);

        await service.ResetAsync("owner", wrongCode, "BrandNewPass1", CancellationToken.None);
        var third = await service.ResetAsync("owner", wrongCode, "BrandNewPass1", CancellationToken.None);
        Assert.Equal(ResetPasswordByEmailStatus.InvalidCode, third.Status);
        Assert.Equal(0, third.RemainingAttempts);

        var locked = await service.ResetAsync("owner", realCode, "BrandNewPass1", CancellationToken.None);
        Assert.Equal(ResetPasswordByEmailStatus.TooManyAttempts, locked.Status);
    }

    [Fact]
    public async Task Reset_RevokesExistingStaffTokens()
    {
        await using var db = CreateDb();
        var staffUserId = await SeedStaffAsync(db);
        db.StaffAccessTokens.Add(new StaffAccessTokenEntity
        {
            StaffAccessTokenId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            OrganizationId = OrgId,
            TokenHash = [1, 2, 3],
            CreatedAtUtc = Now,
            ExpiresAtUtc = Now.AddHours(8),
        });
        await db.SaveChangesAsync();
        var (service, notifications, _) = CreateService(db);
        await service.RequestResetAsync("owner", CancellationToken.None);
        var code = notifications.SentNow.Single().Tokens["code"];

        await service.ResetAsync("owner", code, "BrandNewPass1", CancellationToken.None);

        var accessToken = await db.StaffAccessTokens.SingleAsync();
        Assert.NotNull(accessToken.RevokedAtUtc);
    }

    private sealed class CapturingNotificationService : INotificationService
    {
        public List<NotificationRequest> SentNow { get; } = [];

        public Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NotificationHandle([Guid.NewGuid()], Created: true));

        public Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken)
        {
            SentNow.Add(request);
            return Task.FromResult(new NotificationDeliveryResult(new NotificationHandle([Guid.NewGuid()], Created: true), Delivered: true, Error: null));
        }
    }
}
