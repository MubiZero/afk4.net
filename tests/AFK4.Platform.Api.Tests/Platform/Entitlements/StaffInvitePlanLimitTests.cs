using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class StaffInvitePlanLimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<string> Roles = [OrganizationRoleNames.Operator];

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfStaffInviteService CreateService(PlatformDbContext db) =>
        new(db,
            new RecordingNotificationService(),
            new AFK4.Platform.Api.Identity.PhoneOtp.RandomPhoneOtpGenerator(),
            new AFK4.Platform.Api.Identity.PhoneOtp.Sha256PhoneOtpHasher(),
            new FixedTimeProvider(Now),
            Options.Create(new NotificationOptions { DefaultLocale = "ru" }),
            new EfPlanLimitGuard(db));

    private static async Task<(Guid OrganizationId, Guid BranchId)> SeedOrganizationAsync(
        PlatformDbContext db, int? maxStaffUsersPerBranch, string planCode = "growth")
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Клуб",
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            LimitsJson = OrganizationLimitsJson.Serialize(
                new OrganizationLimitsDto(null, null, null, maxStaffUsersPerBranch)),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "branch-" + branchId.ToString("N")[..8],
            Name = "Филиал",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (organizationId, branchId);
    }

    private static async Task SeedActiveStaffUserAsync(PlatformDbContext db, Guid organizationId, Guid branchId)
    {
        var staffUserId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = organizationId,
            UserName = staffUserId.ToString("N")[..8],
            NormalizedUserName = staffUserId.ToString("N")[..8].ToUpperInvariant(),
            DisplayName = "Сотрудник",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            OrganizationId = organizationId,
            BranchId = branchId,
            RoleName = OrganizationRoleNames.BranchManager
        });
        await db.SaveChangesAsync();
    }

    private static int phoneCounter;

    /// <summary>Свой номер каждому приглашению: номер — глобальный вход, и повтор его занимает.</summary>
    private static string NextPhone() => $"+9929{System.Threading.Interlocked.Increment(ref phoneCounter):D8}";

    [Fact]
    public async Task CreateInvite_RefusesWithNumbers_WhenBranchIsAtStaffLimit()
    {
        await using var db = CreateDb();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxStaffUsersPerBranch: 1);
        await SeedActiveStaffUserAsync(db, organizationId, branchId);
        var service = CreateService(db);

        var result = await service.CreateInviteAsync(
            organizationId, branchId, "newcashier", "New Cashier", NextPhone(), "cashier@club.example", Roles, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.PlanLimit);
        Assert.Equal(PlanLimitNames.StaffUsersPerBranch, result.PlanLimit!.LimitName);
        Assert.Equal(1, result.PlanLimit.Limit);
        Assert.Equal(1, result.PlanLimit.Current);
        Assert.Equal(0, await db.StaffInvites.CountAsync());
    }

    [Fact]
    public async Task CreateInvite_CountsPendingInvitesTowardTheLimit()
    {
        await using var db = CreateDb();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxStaffUsersPerBranch: 2);
        await SeedActiveStaffUserAsync(db, organizationId, branchId);
        var service = CreateService(db);

        var first = await service.CreateInviteAsync(
            organizationId, branchId, "firstinvite", "First Invite", NextPhone(), "first@club.example", Roles, CancellationToken.None);
        Assert.True(first.Succeeded);

        var second = await service.CreateInviteAsync(
            organizationId, branchId, "secondinvite", "Second Invite", NextPhone(), "second@club.example", Roles, CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.NotNull(second.PlanLimit);
        Assert.Equal(PlanLimitNames.StaffUsersPerBranch, second.PlanLimit!.LimitName);
        Assert.Equal(2, second.PlanLimit.Limit);
        Assert.Equal(2, second.PlanLimit.Current);
        Assert.Equal(1, await db.StaffInvites.CountAsync());
    }

    [Fact]
    public async Task AcceptInvite_RefusesWhenLimitDroppedBelowUsageSinceTheInvite()
    {
        await using var db = CreateDb();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxStaffUsersPerBranch: 5);
        var service = CreateService(db);

        var phone = NextPhone();
        var created = await service.CreateInviteAsync(
            organizationId, branchId, "newcashier", "New Cashier", phone, "cashier@club.example", Roles, CancellationToken.None);
        Assert.True(created.Succeeded);

        await SeedActiveStaffUserAsync(db, organizationId, branchId);
        var organization = await db.Organizations.SingleAsync(org => org.OrganizationId == organizationId);
        organization.LimitsJson = OrganizationLimitsJson.Serialize(new OrganizationLimitsDto(null, null, null, 1));
        await db.SaveChangesAsync();

        var result = await service.AcceptInviteAsync(phone, created.Code, "FreshPass123", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.PlanLimit);
        Assert.Equal(PlanLimitNames.StaffUsersPerBranch, result.PlanLimit!.LimitName);
        Assert.Equal(1, result.PlanLimit.Limit);
        Assert.Equal(1, result.PlanLimit.Current);
        Assert.False(await db.StaffUsers.AnyAsync(user => user.NormalizedUserName == "NEWCASHIER"));
        var invite = await db.StaffInvites.SingleAsync();
        Assert.Null(invite.AcceptedAtUtc);
    }

    [Fact]
    public async Task AcceptInvite_Succeeds_WhenBelowLimit()
    {
        await using var db = CreateDb();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxStaffUsersPerBranch: 3);
        await SeedActiveStaffUserAsync(db, organizationId, branchId);
        var service = CreateService(db);

        var phone = NextPhone();
        var created = await service.CreateInviteAsync(
            organizationId, branchId, "newcashier", "New Cashier", phone, "cashier@club.example", Roles, CancellationToken.None);
        Assert.True(created.Succeeded);

        var result = await service.AcceptInviteAsync(phone, created.Code, "FreshPass123", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.PlanLimit);
        var staff = await db.StaffUsers.SingleAsync(user => user.NormalizedUserName == "NEWCASHIER");
        Assert.True(staff.IsActive);
        var invite = await db.StaffInvites.SingleAsync();
        Assert.NotNull(invite.AcceptedAtUtc);
    }

    [Fact]
    public async Task AcceptInvite_Succeeds_WhenAcceptingTheLastSlot_BecauseTheInviteBeingAcceptedDoesNotAddASeat()
    {
        // Лимит 2, один активный сотрудник, одно живое приглашение: приём превращает само это
        // приглашение в сотрудника, а не занимает место сверх уже посчитанного — после приёма
        // ровно 2 сотрудника и 0 приглашений, то есть точно в лимите. Раньше приглашение считало
        // само себя как «непринятое» и отказывало последнему месту навсегда.
        await using var db = CreateDb();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxStaffUsersPerBranch: 2);
        await SeedActiveStaffUserAsync(db, organizationId, branchId);
        var service = CreateService(db);

        var phone = NextPhone();
        var created = await service.CreateInviteAsync(
            organizationId, branchId, "newcashier", "New Cashier", phone, "cashier@club.example", Roles, CancellationToken.None);
        Assert.True(created.Succeeded);

        var result = await service.AcceptInviteAsync(phone, created.Code, "FreshPass123", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.PlanLimit);
        var staff = await db.StaffUsers.SingleAsync(user => user.NormalizedUserName == "NEWCASHIER");
        Assert.True(staff.IsActive);
        Assert.Equal(2, await db.StaffUsers.CountAsync());
        var invite = await db.StaffInvites.SingleAsync();
        Assert.NotNull(invite.AcceptedAtUtc);
    }
}
