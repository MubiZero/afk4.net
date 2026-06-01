using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class ShiftDiscrepancyHookTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T20:00:00Z");
    private static readonly Guid ActorStaffUserId = Guid.NewGuid();

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task SeedOwnerAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Name = "Central Branch", CreatedAtUtc = Now
        });
        var ownerId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = TestIds.OrganizationId, UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = "owner@club.example", PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, RoleName = StaffRoleNames.Owner
        });
        await db.SaveChangesAsync();
    }

    private static EfShiftService NewService(PlatformDbContext db, RecordingNotificationService recorder)
    {
        var notifier = new EfShiftDiscrepancyNotifier(
            new EfOrganizationOwnerResolver(db), recorder, db,
            Options.Create(new NotificationOptions { DefaultLocale = "ru", ShiftDiscrepancyToleranceMinorUnits = 0 }));
        return new EfShiftService(db, new FixedTimeProvider(Now), notifier);
    }

    [Fact]
    public async Task CloseShiftWithDiscrepancy_EnqueuesOwnerAlert()
    {
        await using var db = CreateDb();
        await SeedOwnerAsync(db);
        var recorder = new RecordingNotificationService();
        var service = NewService(db, recorder);

        var open = await service.OpenShiftAsync(TestIds.BranchId, ActorStaffUserId,
            new OpenShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 50000), "front", "open-1"), CancellationToken.None);
        // No movements/payments → expected = starting 50000; count 45000 → 50.00 short.
        await service.CloseShiftAsync(open.Response!.ShiftId, ActorStaffUserId,
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 45000), "short", "close-1"), CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.ShiftDiscrepancy, request.TemplateKey);
        Assert.Equal("-50.00", request.Tokens["difference"]);
    }

    [Fact]
    public async Task CloseShiftWithoutDiscrepancy_EnqueuesNothing()
    {
        await using var db = CreateDb();
        await SeedOwnerAsync(db);
        var recorder = new RecordingNotificationService();
        var service = NewService(db, recorder);

        var open = await service.OpenShiftAsync(TestIds.BranchId, ActorStaffUserId,
            new OpenShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 50000), "front", "open-1"), CancellationToken.None);
        await service.CloseShiftAsync(open.Response!.ShiftId, ActorStaffUserId,
            new CloseShiftRequest(TestIds.OrganizationId, new MoneyDto("TJS", 50000), "balanced", "close-1"), CancellationToken.None);

        Assert.Empty(recorder.Requests);
    }
}
