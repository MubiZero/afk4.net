using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class PlanLimitGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    private static async Task<(Guid OrganizationId, Guid BranchId)> SeedAsync(
        PlatformDbContext db,
        OrganizationLimitsDto limits,
        string planCode = "growth")
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
            LimitsJson = OrganizationLimitsJson.Serialize(limits),
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

    private static void SeedDevice(PlatformDbContext db, Guid organizationId, Guid branchId, string state)
    {
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            MachineName = "PC",
            DisplayName = "PC",
            Role = DeviceRoleNames.GamingPc,
            EnrollmentState = state,
            EnrolledAtUtc = Now
        });
    }

    private static void SeedSession(PlatformDbContext db, Guid organizationId, Guid branchId, string state)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "guest",
            BillingMode = BillingModeNames.PostpaidDebt,
            State = state,
            RequestedAtUtc = Now,
            StartedAtUtc = Now,
            UpdatedAtUtc = Now,
            Version = 1
        });
    }

    [Fact]
    public async Task NoLimit_MeansUnlimited()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, null, null, null));
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Approved);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.Null(await guard.CheckBranchAsync(organizationId, CancellationToken.None));
        Assert.Null(await guard.CheckDeviceAsync(organizationId, branchId, CancellationToken.None));
        Assert.Null(await guard.CheckConcurrentSessionAsync(organizationId, CancellationToken.None));
        Assert.Null(await guard.CheckStaffUserAsync(organizationId, branchId, CancellationToken.None));
    }

    [Fact]
    public async Task Branch_RefusesWhenLimitReached_AndCarriesNumbers()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, _) = await SeedAsync(db, new OrganizationLimitsDto(1, null, null, null), "starter");
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        var verdict = await guard.CheckBranchAsync(organizationId, CancellationToken.None);

        Assert.NotNull(verdict);
        Assert.Equal(PlanLimitNames.ReachedCode, verdict!.Code);
        Assert.Equal(PlanLimitNames.Branches, verdict.LimitName);
        Assert.Equal(1, verdict.Limit);
        Assert.Equal(1, verdict.Current);
        Assert.Equal("starter", verdict.PlanCode);
    }

    [Fact]
    public async Task Branch_AllowsWhileBelowLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, _) = await SeedAsync(db, new OrganizationLimitsDto(3, null, null, null));
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.Null(await guard.CheckBranchAsync(organizationId, CancellationToken.None));
    }

    [Fact]
    public async Task Device_CountsOnlyLiveEnrollments()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, 2, null, null));
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Approved);
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Removed);
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Rejected);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        // Живое устройство одно из двух разрешённых: снятые и отклонённые места не занимают.
        Assert.Null(await guard.CheckDeviceAsync(organizationId, branchId, CancellationToken.None));
    }

    [Fact]
    public async Task Device_LimitIsPerBranch()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, 1, null, null));
        var otherBranchId = Guid.NewGuid();
        db.Branches.Add(new BranchEntity
        {
            BranchId = otherBranchId,
            OrganizationId = organizationId,
            Slug = "branch-" + otherBranchId.ToString("N")[..8],
            Name = "Второй",
            CreatedAtUtc = Now
        });
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Approved);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.NotNull(await guard.CheckDeviceAsync(organizationId, branchId, CancellationToken.None));
        Assert.Null(await guard.CheckDeviceAsync(organizationId, otherBranchId, CancellationToken.None));
    }

    [Fact]
    public async Task Session_CountsLiveStatesAcrossOrganization_AndIgnoresEnded()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, null, 2, null));
        SeedSession(db, organizationId, branchId, SessionStateNames.Active);
        SeedSession(db, organizationId, branchId, SessionStateNames.Ended);
        SeedSession(db, organizationId, branchId, SessionStateNames.Reconciled);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.Null(await guard.CheckConcurrentSessionAsync(organizationId, CancellationToken.None));

        SeedSession(db, organizationId, branchId, SessionStateNames.Paused);
        await db.SaveChangesAsync();

        var verdict = await guard.CheckConcurrentSessionAsync(organizationId, CancellationToken.None);
        Assert.NotNull(verdict);
        Assert.Equal(PlanLimitNames.ConcurrentSessions, verdict!.LimitName);
        Assert.Equal(2, verdict.Current);
    }

    [Fact]
    public async Task Staff_CountsActiveUsersAndPendingInvitesOfBranch()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, null, null, 2));

        var activeUserId = Guid.NewGuid();
        var disabledUserId = Guid.NewGuid();
        foreach (var (staffUserId, isActive) in new[] { (activeUserId, true), (disabledUserId, false) })
        {
            db.StaffUsers.Add(new StaffUserEntity
            {
                StaffUserId = staffUserId,
                OrganizationId = organizationId,
                UserName = staffUserId.ToString("N")[..8],
                NormalizedUserName = staffUserId.ToString("N")[..8].ToUpperInvariant(),
                DisplayName = "Сотрудник",
                IsActive = isActive,
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
        }
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        // Отключённый сотрудник места не занимает: один активный из двух разрешённых.
        Assert.Null(await guard.CheckStaffUserAsync(organizationId, branchId, CancellationToken.None));

        db.StaffInvites.Add(new StaffInviteEntity
        {
            StaffInviteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            UserName = "newbie",
            NormalizedUserName = "NEWBIE",
            DisplayName = "Новичок",
            Email = "newbie@example.test",
            RoleNamesCsv = OrganizationRoleNames.BranchManager,
            PhoneNumber = "+992937380099",
            NormalizedPhone = "992937380099",
            CodeHash = "hash",
            CreatedAtUtc = Now,
            ExpiresAtUtc = Now.AddDays(7)
        });
        await db.SaveChangesAsync();

        // Непринятое приглашение занимает место заранее — иначе три приглашения на филиал
        // с лимитом два перепрыгнут границу все разом в момент приёма.
        var verdict = await guard.CheckStaffUserAsync(organizationId, branchId, CancellationToken.None);
        Assert.NotNull(verdict);
        Assert.Equal(PlanLimitNames.StaffUsersPerBranch, verdict!.LimitName);
        Assert.Equal(2, verdict.Current);
    }

    [Fact]
    public async Task UnknownOrganization_IsNotRefused()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        // Несуществующая организация — не повод отказывать по лимиту: за «нет такой» отвечает
        // вызывающий код своей ошибкой, иначе пользователь получит ложное объяснение отказа.
        Assert.Null(await guard.CheckBranchAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
