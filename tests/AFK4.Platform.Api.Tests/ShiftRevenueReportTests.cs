using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class ShiftRevenueReportTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly DateTimeOffset Opened = DateTimeOffset.Parse("2026-06-10T09:00:00Z");
    private const string Tjs = "TJS";

    private static PlatformDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    [Fact]
    public async Task GetCurrentShiftRevenue_AggregatesEarnedAndInflow()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        SeedOpenShift(db, shiftId, startingCash: 10000);
        SeedLedger(db, shiftId, LedgerEntryTypeNames.GameplayCharge, -3100);
        SeedLedger(db, shiftId, LedgerEntryTypeNames.PostpaidDebt, 200);
        SeedLedger(db, shiftId, LedgerEntryTypeNames.TopUp, 900);
        SeedPosSale(db, shiftId, total: 1150, paid: true);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 2000);
        SeedPayment(db, shiftId, PaymentMethodNames.CardManual, "payment", 1800);
        SeedPayment(db, shiftId, PaymentMethodNames.Wallet, "payment", 500);
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetCurrentShiftRevenueAsync(OrgId, BranchId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(3300, result!.Earned.Time.MinorUnits);
        Assert.Equal(1150, result.Earned.Goods.MinorUnits);
        Assert.Equal(4450, result.Earned.Total.MinorUnits);
        Assert.Equal(2000, result.Inflow.Cash.MinorUnits);
        Assert.Equal(1800, result.Inflow.NonCash.MinorUnits);
        Assert.Equal(900, result.Inflow.WalletTopUps.MinorUnits);
        Assert.Equal(3800, result.Inflow.DirectTotal.MinorUnits);
        Assert.Equal(10000, result.Cash.Starting.MinorUnits);
        Assert.Null(result.Cash.Counted);
    }

    [Fact]
    public async Task GetCurrentShiftRevenue_RefundsReduceInflow()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        SeedOpenShift(db, shiftId, startingCash: 0);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 5000);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "refund", -1500);
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetCurrentShiftRevenueAsync(OrgId, BranchId, CancellationToken.None);

        Assert.Equal(3500, result!.Inflow.Cash.MinorUnits);
    }

    [Fact]
    public async Task GetCurrentShiftRevenue_NoOpenShift_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);

        var result = await service.GetCurrentShiftRevenueAsync(OrgId, BranchId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetShiftRevenue_ListsClosedShiftsWithReconciliation()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        SeedClosedShift(db, shiftId, startingCash: 10000, countedCash: 14000);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 4000);
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetShiftRevenueAsync(
            OrgId, BranchId, new ReportSearchQuery(null, null, 10), CancellationToken.None);

        var row = Assert.Single(result.Shifts);
        Assert.Equal(shiftId, row.ShiftId);
        Assert.Equal(14000, row.Cash.Counted!.MinorUnits);
        Assert.Equal(14000, row.Cash.Expected.MinorUnits);
        Assert.Equal(0, row.Cash.Difference!.MinorUnits);
    }

    [Fact]
    public async Task GetShiftRevenue_CashReconciliation_SubtractsCashRefund()
    {
        await using var db = CreateDbContext();
        var shiftId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        SeedClosedShift(db, shiftId, startingCash: 10000, countedCash: 13000);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "payment", 4000);
        SeedPayment(db, shiftId, PaymentMethodNames.Cash, "refund", -1000);
        await db.SaveChangesAsync();
        var service = new EfReportService(db);

        var result = await service.GetShiftRevenueAsync(
            OrgId, BranchId, new ReportSearchQuery(null, null, 10), CancellationToken.None);

        var row = Assert.Single(result.Shifts);
        Assert.Equal(13000, row.Cash.Expected.MinorUnits);   // 10000 + 4000 − 1000
        Assert.Equal(0, row.Cash.Difference!.MinorUnits);     // counted 13000 − expected 13000
        Assert.Equal(3000, row.Inflow.Cash.MinorUnits);       // 4000 − 1000 net
    }

    private static void SeedOpenShift(PlatformDbContext db, Guid shiftId, long startingCash) =>
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId, OrganizationId = OrgId, BranchId = BranchId,
            OpenedByStaffUserId = StaffId, State = ShiftStateNames.Open, CurrencyCode = Tjs,
            StartingCashMinorUnits = startingCash, OpenedAtUtc = Opened
        });

    private static void SeedClosedShift(PlatformDbContext db, Guid shiftId, long startingCash, long countedCash) =>
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId, OrganizationId = OrgId, BranchId = BranchId,
            OpenedByStaffUserId = StaffId, ClosedByStaffUserId = StaffId,
            State = ShiftStateNames.Closed, CurrencyCode = Tjs,
            StartingCashMinorUnits = startingCash, CountedCashMinorUnits = countedCash,
            OpenedAtUtc = Opened, ClosedAtUtc = Opened.AddHours(8)
        });

    private static void SeedLedger(PlatformDbContext db, Guid shiftId, string entryType, long amount) =>
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
            ShiftId = shiftId, PlayerAccountId = Guid.NewGuid(), EntryType = entryType,
            AccountType = "wallet", AmountMinorUnits = amount, CurrencyCode = Tjs,
            CreatedByStaffUserId = StaffId, CreatedAtUtc = Opened.AddHours(1)
        });

    private static void SeedPosSale(PlatformDbContext db, Guid shiftId, long total, bool paid) =>
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
            ShiftId = shiftId, CreatedByStaffUserId = StaffId, State = "paid",
            CurrencyCode = Tjs, TotalMinorUnits = total,
            CreatedAtUtc = Opened.AddHours(1), PaidAtUtc = paid ? Opened.AddHours(1) : null
        });

    private static void SeedPayment(PlatformDbContext db, Guid shiftId, string method, string kind, long amount) =>
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId,
            ShiftId = shiftId, CreatedByStaffUserId = StaffId, PaymentKind = kind,
            PaymentMethod = method, CurrencyCode = Tjs, AmountMinorUnits = amount,
            CreatedAtUtc = Opened.AddHours(1)
        });
}
