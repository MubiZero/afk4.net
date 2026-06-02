using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class EfShiftServiceSignOffTests
{
    private static readonly Guid Opener = Guid.Parse("a1a1a1a1-a1a1-4a1a-8a1a-a1a1a1a1a1a1");
    private static readonly Guid Closer = Guid.Parse("b2b2b2b2-b2b2-4b2b-8b2b-b2b2b2b2b2b2");
    private static readonly Guid Manager = Guid.Parse("c3c3c3c3-c3c3-4c3c-8c3c-c3c3c3c3c3c3");
    private static readonly Guid Outsider = Guid.Parse("d4d4d4d4-d4d4-4d4d-8d4d-d4d4d4d4d4d4");
    private static readonly Guid ShiftId = Guid.Parse("e5e5e5e5-e5e5-4e5e-8e5e-e5e5e5e5e5e5");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T18:00:00Z");

    [Fact]
    public async Task Close_WithinTolerance_NoSignOffNeeded()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db);
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        // Expected = starting 50000; count 49000 ⇒ |diff| 1000 ≤ 2000 default tolerance.
        var result = await service.CloseShiftAsync(ShiftId, Closer, CloseRequest(counted: 49000), CancellationToken.None);

        Assert.True(result.Succeeded);
        var shift = await db.Shifts.SingleAsync();
        Assert.Equal(ShiftStateNames.Closed, shift.State);
        Assert.Null(shift.ManagerSignOffStaffUserId);
    }

    [Fact]
    public async Task Close_OverTolerance_WithoutSignOff_Rejected()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db);
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        // count 40000 ⇒ |diff| 10000 > 2000.
        var result = await service.CloseShiftAsync(ShiftId, Closer, CloseRequest(counted: 40000), CancellationToken.None);

        Assert.False(result.Succeeded);
        var shift = await db.Shifts.SingleAsync();
        Assert.Equal(ShiftStateNames.Open, shift.State); // stays open
    }

    [Fact]
    public async Task Close_OverTolerance_WithDifferentManagerSignOff_Succeeds()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db);
        await SeedRoleAsync(db, Manager, StaffRoleNames.BranchManager);
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        var result = await service.CloseShiftAsync(
            ShiftId, Closer, CloseRequest(counted: 40000, signOff: Manager, signOffReason: "counted twice, till was short"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var shift = await db.Shifts.SingleAsync();
        Assert.Equal(ShiftStateNames.Closed, shift.State);
        Assert.Equal(Manager, shift.ManagerSignOffStaffUserId);
        Assert.Equal("counted twice, till was short", shift.SignOffReason);
    }

    [Fact]
    public async Task Close_OverTolerance_SelfSignOffByCloser_Rejected()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db);
        await SeedRoleAsync(db, Closer, StaffRoleNames.BranchManager);
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        var result = await service.CloseShiftAsync(
            ShiftId, Closer, CloseRequest(counted: 40000, signOff: Closer), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ShiftStateNames.Open, (await db.Shifts.SingleAsync()).State);
    }

    [Fact]
    public async Task Close_OverTolerance_SignOffByOpener_Rejected()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db);
        await SeedRoleAsync(db, Opener, StaffRoleNames.BranchManager);
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        var result = await service.CloseShiftAsync(
            ShiftId, Closer, CloseRequest(counted: 40000, signOff: Opener), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Close_OverTolerance_SignOffUserLacksPermission_Rejected()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db);
        await SeedRoleAsync(db, Outsider, StaffRoleNames.Technician); // no CloseShift / ManageShiftCash
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        var result = await service.CloseShiftAsync(
            ShiftId, Closer, CloseRequest(counted: 40000, signOff: Outsider), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Close_OverTolerance_BranchToleranceOverride_AllowsWithoutSignOff()
    {
        await using var db = CreateDbContext();
        await SeedOpenShiftAsync(db, discrepancyTolerance: 20000);
        var service = new EfShiftService(db, new FixedTimeProvider(Now));

        // |diff| 10000 ≤ 20000 branch tolerance ⇒ closes without sign-off.
        var result = await service.CloseShiftAsync(ShiftId, Closer, CloseRequest(counted: 40000), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShiftStateNames.Closed, (await db.Shifts.SingleAsync()).State);
    }

    private static CloseShiftRequest CloseRequest(long counted, Guid? signOff = null, string? signOffReason = null) =>
        new(
            TestIds.OrganizationId,
            new MoneyDto("TJS", counted),
            "end of shift",
            $"close-{counted}-{signOff}",
            signOff,
            signOffReason);

    private static async Task SeedOpenShiftAsync(PlatformDbContext db, long? discrepancyTolerance = null)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Slug = "main",
            Name = "Main",
            City = "Dushanbe",
            ShiftDiscrepancyToleranceMinorUnits = discrepancyTolerance,
            CreatedAtUtc = Now
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = ShiftId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = Opener,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            OpeningNote = "open",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedRoleAsync(PlatformDbContext db, Guid staffUserId, string role)
    {
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(),
            StaffUserId = staffUserId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            RoleName = role
        });
        await db.SaveChangesAsync();
    }

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
