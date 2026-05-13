using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfShiftServiceTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T12:00:00Z");

    [Fact]
    public async Task OpenShiftAsync_CreatesOneOpenShiftPerBranchAndReturnsShiftDto()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = OpenShiftRequest("shift-open-001");

        var result = await service.OpenShiftAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(TestIds.OrganizationId, result.Response.OrganizationId);
        Assert.Equal(TestIds.BranchId, result.Response.BranchId);
        Assert.Equal(ActorStaffUserId, result.Response.OpenedByStaffUserId);
        Assert.Equal(ShiftStateNames.Open, result.Response.State);
        Assert.Equal(new MoneyDto("TJS", 50000), result.Response.StartingCash);
        var shift = await db.Shifts.SingleAsync();
        Assert.Equal(result.Response.ShiftId, shift.ShiftId);
        Assert.Equal(ShiftStateNames.Open, shift.State);
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task OpenShiftAsync_RejectsSecondOpenShiftInSameBranch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var first = await service.OpenShiftAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            OpenShiftRequest("shift-open-001"),
            CancellationToken.None);
        var second = await service.OpenShiftAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            OpenShiftRequest("shift-open-002"),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.False(second.Conflict);
        Assert.Single(await db.Shifts.ToListAsync());
    }

    [Fact]
    public async Task OpenShiftAsync_AllowsOpenShiftInAnotherBranch()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var first = await service.OpenShiftAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            OpenShiftRequest("shift-open-001"),
            CancellationToken.None);
        var second = await service.OpenShiftAsync(
            OtherBranchId,
            ActorStaffUserId,
            OpenShiftRequest("shift-open-002"),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(2, await db.Shifts.CountAsync());
    }

    [Fact]
    public async Task OpenShiftAsync_ReplaysSameIdempotencyKeyAndRequest()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = OpenShiftRequest("shift-open-001");

        var first = await service.OpenShiftAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.OpenShiftAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.ShiftId, second.Response.ShiftId);
        Assert.Single(await db.Shifts.ToListAsync());
    }

    [Fact]
    public async Task OpenShiftAsync_ReusingIdempotencyKeyWithDifferentRequestReturnsConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = OpenShiftRequest("shift-open-001");

        await service.OpenShiftAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var conflict = await service.OpenShiftAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            request with { OpeningNote = "different drawer" },
            CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Single(await db.Shifts.ToListAsync());
    }

    [Fact]
    public async Task RecordCashMovementAsync_AppendsCashInAndCashOutRowsToOpenShift()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var shift = await OpenShiftAsync(service);

        var cashIn = await service.RecordCashMovementAsync(
            shift.ShiftId,
            ActorStaffUserId,
            new RecordCashMovementRequest(
                TestIds.OrganizationId,
                CashMovementTypeNames.CashIn,
                new MoneyDto("TJS", 25000),
                "cash drawer refill",
                "cash-in-001"),
            CancellationToken.None);
        var cashOut = await service.RecordCashMovementAsync(
            shift.ShiftId,
            ActorStaffUserId,
            new RecordCashMovementRequest(
                TestIds.OrganizationId,
                CashMovementTypeNames.CashOut,
                new MoneyDto("TJS", 10000),
                "petty cash",
                "cash-out-001"),
            CancellationToken.None);

        Assert.True(cashIn.Succeeded);
        Assert.True(cashOut.Succeeded);
        Assert.NotNull(cashIn.Response);
        Assert.NotNull(cashOut.Response);
        Assert.Equal(CashMovementTypeNames.CashIn, cashIn.Response.MovementType);
        Assert.Equal(CashMovementTypeNames.CashOut, cashOut.Response.MovementType);
        Assert.Equal(2, await db.CashMovements.CountAsync());
        Assert.All(await db.CashMovements.ToListAsync(), movement => Assert.Equal(shift.ShiftId, movement.ShiftId));
    }

    [Fact]
    public async Task RecordCashMovementAsync_ReplaysSameIdempotencyKeyAndRequest()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var shift = await OpenShiftAsync(service);
        var request = new RecordCashMovementRequest(
            TestIds.OrganizationId,
            CashMovementTypeNames.CashIn,
            new MoneyDto("TJS", 25000),
            "cash drawer refill",
            "cash-in-001");

        var first = await service.RecordCashMovementAsync(shift.ShiftId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.RecordCashMovementAsync(shift.ShiftId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.CashMovementId, second.Response.CashMovementId);
        Assert.Single(await db.CashMovements.ToListAsync());
    }

    [Fact]
    public async Task CloseShiftAsync_ComputesExpectedCashAndClosesTheShift()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var shift = await OpenShiftAsync(service);
        await service.RecordCashMovementAsync(
            shift.ShiftId,
            ActorStaffUserId,
            new RecordCashMovementRequest(
                TestIds.OrganizationId,
                CashMovementTypeNames.CashIn,
                new MoneyDto("TJS", 25000),
                "cash drawer refill",
                "cash-in-001"),
            CancellationToken.None);
        await service.RecordCashMovementAsync(
            shift.ShiftId,
            ActorStaffUserId,
            new RecordCashMovementRequest(
                TestIds.OrganizationId,
                CashMovementTypeNames.CashOut,
                new MoneyDto("TJS", 10000),
                "petty cash",
                "cash-out-001"),
            CancellationToken.None);

        var result = await service.CloseShiftAsync(
            shift.ShiftId,
            ActorStaffUserId,
            new CloseShiftRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", 64000),
                "short by one somoni",
                "shift-close-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(ShiftStateNames.Closed, result.Response.State);
        Assert.Equal(new MoneyDto("TJS", 65000), result.Response.ExpectedCash);
        Assert.Equal(new MoneyDto("TJS", 64000), result.Response.CountedCash);
        Assert.Equal(new MoneyDto("TJS", -1000), result.Response.Difference);
        var entity = await db.Shifts.SingleAsync();
        Assert.Equal(ShiftStateNames.Closed, entity.State);
        Assert.Equal(65000, entity.ExpectedCashMinorUnits);
        Assert.Equal(64000, entity.CountedCashMinorUnits);
        Assert.Equal(-1000, entity.DifferenceMinorUnits);
        Assert.Equal(ActorStaffUserId, entity.ClosedByStaffUserId);
    }

    [Fact]
    public async Task CloseShiftAsync_RejectsAlreadyClosedShift()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var shift = await OpenShiftAsync(service);
        var request = new CloseShiftRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 50000),
            "balanced",
            "shift-close-001");

        var first = await service.CloseShiftAsync(shift.ShiftId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CloseShiftAsync(
            shift.ShiftId,
            ActorStaffUserId,
            request with { IdempotencyKey = "shift-close-002" },
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.False(second.Conflict);
        Assert.Single(await db.Shifts.Where(candidate => candidate.State == ShiftStateNames.Closed).ToListAsync());
    }

    [Fact]
    public async Task GetOpenShiftIdAsync_ReturnsOpenShiftIdOrValidationError()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var missing = await service.GetOpenShiftIdAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);
        var shift = await OpenShiftAsync(service);
        var existing = await service.GetOpenShiftIdAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);

        Assert.False(missing.Succeeded);
        Assert.True(existing.Succeeded);
        Assert.Equal(shift.ShiftId, existing.Response);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfShiftService CreateService(PlatformDbContext db)
    {
        return new EfShiftService(db, new FixedTimeProvider(Now));
    }

    private static OpenShiftRequest OpenShiftRequest(string idempotencyKey)
    {
        return new OpenShiftRequest(
            TestIds.OrganizationId,
            new MoneyDto("TJS", 50000),
            "front register",
            idempotencyKey);
    }

    private static async Task<ShiftDto> OpenShiftAsync(EfShiftService service)
    {
        var result = await service.OpenShiftAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            OpenShiftRequest("shift-open-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        return result.Response;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
