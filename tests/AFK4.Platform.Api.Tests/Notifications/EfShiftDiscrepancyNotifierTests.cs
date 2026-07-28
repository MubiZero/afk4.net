using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class EfShiftDiscrepancyNotifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T20:00:00Z");
    private static readonly Guid OrgId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid BranchId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task SeedOrgWithOwnerAsync(PlatformDbContext db, string? ownerEmail = "owner@club.example")
    {
        var ownerId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrgId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId, OrganizationId = OrgId, Name = "Central Branch", CreatedAtUtc = Now
        });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = OrgId, UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = ownerEmail, PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = OrgId, BranchId = BranchId, RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await db.SaveChangesAsync();
    }

    private static EfShiftDiscrepancyNotifier NewNotifier(PlatformDbContext db, RecordingNotificationService recorder, long toleranceMinorUnits = 0) =>
        new(new EfOrganizationOwnerResolver(db), recorder, db,
            Options.Create(new NotificationOptions { DefaultLocale = "ru", ShiftDiscrepancyToleranceMinorUnits = toleranceMinorUnits }));

    private static ShiftEntity ClosedShift(long differenceMinorUnits) => new()
    {
        ShiftId = Guid.NewGuid(),
        OrganizationId = OrgId,
        BranchId = BranchId,
        State = "closed",
        CurrencyCode = "TJS",
        StartingCashMinorUnits = 100000,
        CountedCashMinorUnits = 100000 + differenceMinorUnits,
        ExpectedCashMinorUnits = 100000,
        DifferenceMinorUnits = differenceMinorUnits,
        OpenedAtUtc = Now.AddHours(-8),
        ClosedAtUtc = Now,
    };

    [Fact]
    public async Task NotifyIfOverToleranceAsync_DifferenceOverTolerance_EnqueuesOperationalDiscrepancyToOwner()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        var recorder = new RecordingNotificationService();
        var notifier = NewNotifier(db, recorder, toleranceMinorUnits: 500);
        var shift = ClosedShift(-2500); // 25.00 short

        await notifier.NotifyIfOverToleranceAsync(shift, CancellationToken.None);

        var request = Assert.Single(recorder.Requests);
        Assert.Equal(NotificationTemplateKeys.ShiftDiscrepancy, request.TemplateKey);
        Assert.Equal(NotificationCategory.Operational, request.Category);
        Assert.Equal("owner@club.example", request.Recipient.EmailAddress);
        Assert.Equal($"shift-discrepancy:{shift.ShiftId:N}", request.IdempotencyKey);
        Assert.Equal("Demo Club", request.Tokens["organizationName"]);
        Assert.Equal("Central Branch", request.Tokens["branchName"]);
        Assert.Equal("-25.00", request.Tokens["difference"]);
        Assert.Equal("TJS", request.Tokens["currency"]);
    }

    [Fact]
    public async Task NotifyIfOverToleranceAsync_DifferenceWithinTolerance_SendsNothing()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db);
        var recorder = new RecordingNotificationService();
        var notifier = NewNotifier(db, recorder, toleranceMinorUnits: 500);

        await notifier.NotifyIfOverToleranceAsync(ClosedShift(-300), CancellationToken.None); // 3.00, within 5.00

        Assert.Empty(recorder.Requests);
    }

    [Fact]
    public async Task NotifyIfOverToleranceAsync_NoOwnerEmail_SendsNothing()
    {
        await using var db = NewContext();
        await SeedOrgWithOwnerAsync(db, ownerEmail: null);
        var recorder = new RecordingNotificationService();
        var notifier = NewNotifier(db, recorder);

        await notifier.NotifyIfOverToleranceAsync(ClosedShift(-2500), CancellationToken.None);

        Assert.Empty(recorder.Requests);
    }
}
