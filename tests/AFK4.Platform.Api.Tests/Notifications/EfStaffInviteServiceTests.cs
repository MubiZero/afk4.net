using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class EfStaffInviteServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
    private static readonly Guid OrgId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static (EfStaffInviteService Service, RecordingNotificationService Notifications, FixedTimeProvider Time) CreateService(PlatformDbContext db)
    {
        var notifications = new RecordingNotificationService();
        var time = new FixedTimeProvider(Now);
        var service = new EfStaffInviteService(
            db, notifications, time, Options.Create(new NotificationOptions { DefaultLocale = "ru" }), new EfPlanLimitGuard(db));
        return (service, notifications, time);
    }

    private static IReadOnlyList<string> Roles => [OrganizationRoleNames.Operator];

    [Fact]
    public async Task CreateInviteAsync_PersistsInviteAndEnqueuesStaffInviteEmailWithCode()
    {
        await using var db = CreateDb();
        var (service, notifications, _) = CreateService(db);

        var result = await service.CreateInviteAsync(OrgId, BranchId, "newcashier", "New Cashier", "cashier@club.example", Roles, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.StaffInviteId);
        Assert.False(string.IsNullOrWhiteSpace(result.Code));
        Assert.Equal(1, await db.StaffInvites.CountAsync());

        var request = Assert.Single(notifications.Requests);
        Assert.Equal(NotificationTemplateKeys.StaffInvite, request.TemplateKey);
        Assert.Equal(NotificationCategory.Transactional, request.Category);
        Assert.Equal("cashier@club.example", request.Recipient.EmailAddress);
        Assert.Equal("New Cashier", request.Tokens["displayName"]);
        Assert.Equal(result.Code, request.Tokens["code"]);
    }

    [Fact]
    public async Task CreateInviteAsync_UserNameAlreadyExistsInOrg_FailsWithoutEmail()
    {
        await using var db = CreateDb();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(), OrganizationId = OrgId, UserName = "taken",
            NormalizedUserName = "TAKEN", DisplayName = "Taken", PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        var (service, notifications, _) = CreateService(db);

        var result = await service.CreateInviteAsync(OrgId, BranchId, "taken", "Dup", "dup@club.example", Roles, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(0, await db.StaffInvites.CountAsync());
        Assert.Empty(notifications.Requests);
    }

    [Fact]
    public async Task AcceptInviteAsync_ValidToken_CreatesActiveStaffUserWithRolesAndPassword()
    {
        await using var db = CreateDb();
        var (service, notifications, _) = CreateService(db);
        var created = await service.CreateInviteAsync(OrgId, BranchId, "newcashier", "New Cashier", "cashier@club.example",
            [OrganizationRoleNames.Operator, OrganizationRoleNames.Technician], CancellationToken.None);

        var result = await service.AcceptInviteAsync(created.Code, "FreshPass123", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(OrgId, result.OrganizationId);
        Assert.Equal("newcashier", result.UserName);

        var staff = await db.StaffUsers.SingleAsync(user => user.NormalizedUserName == "NEWCASHIER");
        Assert.True(staff.IsActive);
        Assert.Equal("cashier@club.example", staff.Email);
        Assert.Equal(PasswordVerificationResult.Success,
            new PasswordHasher<StaffUserEntity>().VerifyHashedPassword(staff, staff.PasswordHash, "FreshPass123"));

        var roles = await db.StaffRoleAssignments
            .Where(r => r.StaffUserId == staff.StaffUserId && r.BranchId == BranchId)
            .Select(r => r.RoleName).ToListAsync();
        Assert.Contains(OrganizationRoleNames.Operator, roles);
        Assert.Contains(OrganizationRoleNames.Technician, roles);

        var invite = await db.StaffInvites.SingleAsync();
        Assert.NotNull(invite.AcceptedAtUtc);
        Assert.Equal(staff.StaffUserId, invite.AcceptedByStaffUserId);
    }

    [Fact]
    public async Task AcceptInviteAsync_IsSingleUse()
    {
        await using var db = CreateDb();
        var (service, _, _) = CreateService(db);
        var created = await service.CreateInviteAsync(OrgId, BranchId, "newcashier", "New Cashier", "cashier@club.example", Roles, CancellationToken.None);

        Assert.True((await service.AcceptInviteAsync(created.Code, "FreshPass123", CancellationToken.None)).Succeeded);
        Assert.False((await service.AcceptInviteAsync(created.Code, "Another123", CancellationToken.None)).Succeeded);
        Assert.Equal(1, await db.StaffUsers.CountAsync());
    }

    [Fact]
    public async Task AcceptInviteAsync_ExpiredToken_Fails()
    {
        await using var db = CreateDb();
        var (service, _, time) = CreateService(db);
        var created = await service.CreateInviteAsync(OrgId, BranchId, "newcashier", "New Cashier", "cashier@club.example", Roles, CancellationToken.None);
        time.Now = Now.AddDays(30);

        var result = await service.AcceptInviteAsync(created.Code, "FreshPass123", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(0, await db.StaffUsers.CountAsync());
    }

    [Fact]
    public async Task AcceptInviteAsync_UnknownToken_Fails()
    {
        await using var db = CreateDb();
        var (service, _, _) = CreateService(db);

        var result = await service.AcceptInviteAsync("00000000000000000000000000000000.deadbeef", "FreshPass123", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AcceptInviteAsync_UserNameTakenAfterInvite_Fails()
    {
        await using var db = CreateDb();
        var (service, _, _) = CreateService(db);
        var created = await service.CreateInviteAsync(OrgId, BranchId, "newcashier", "New Cashier", "cashier@club.example", Roles, CancellationToken.None);
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(), OrganizationId = OrgId, UserName = "newcashier",
            NormalizedUserName = "NEWCASHIER", DisplayName = "Race", PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();

        var result = await service.AcceptInviteAsync(created.Code, "FreshPass123", CancellationToken.None);

        Assert.False(result.Succeeded);
    }
}
