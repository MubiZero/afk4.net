using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfReportServiceTests
{
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid OtherBranchId = Guid.Parse("99999999-9999-4999-8999-999999999999");
    private static readonly DateTimeOffset ReportDay = DateTimeOffset.Parse("2026-05-14T00:00:00Z");

    [Fact]
    public async Task GetShiftReportAsync_ReturnsBranchRowsNewestFirstWithCashAggregates()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        var closedShiftId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var openShiftId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        SeedShift(db, closedShiftId, ShiftStateNames.Closed, ReportDay.AddHours(9), countedCash: 67000);
        SeedShift(db, openShiftId, ShiftStateNames.Open, ReportDay.AddHours(12), countedCash: 0);
        SeedShift(
            db,
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            ShiftStateNames.Closed,
            ReportDay.AddHours(13),
            countedCash: 12345,
            branchId: OtherBranchId);
        SeedCashMovement(db, closedShiftId, CashMovementTypeNames.CashIn, 5000);
        SeedCashMovement(db, closedShiftId, CashMovementTypeNames.CashOut, 1000);
        SeedPayment(db, closedShiftId, Guid.NewGuid(), PaymentMethodNames.Cash, "payment", 2400);
        SeedPayment(db, closedShiftId, Guid.NewGuid(), PaymentMethodNames.Cash, "refund", -1200);
        SeedPayment(db, closedShiftId, Guid.NewGuid(), PaymentMethodNames.CardManual, "payment", 9999);
        SeedLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.TopUp, 10000);
        SeedLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.DebtPayment, -3000);
        SeedLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.ManualCorrection, -500);
        SeedLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.GameplayCharge, -9000);
        await db.SaveChangesAsync();

        var result = await service.GetShiftReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 10),
            CancellationToken.None);

        Assert.Equal(10, result.Limit);
        Assert.Collection(
            result.Rows,
            first =>
            {
                Assert.Equal(openShiftId, first.ShiftId);
                Assert.Null(first.CountedCash);
                Assert.Null(first.Difference);
            },
            second =>
            {
                Assert.Equal(closedShiftId, second.ShiftId);
                Assert.Equal(4000, second.CashMovementsTotal.MinorUnits);
                Assert.Equal(2400, second.PosCashPaymentsTotal.MinorUnits);
                Assert.Equal(-1200, second.PosRefundsTotal.MinorUnits);
                Assert.Equal(12500, second.BillingCashImpactTotal.MinorUnits);
                Assert.Equal(67700, second.ExpectedCash.MinorUnits);
                Assert.Equal(67000, second.CountedCash?.MinorUnits);
                Assert.Equal(-700, second.Difference?.MinorUnits);
            });
    }

    [Fact]
    public async Task GetSalesReportAsync_ReturnsRowsAndTotalsForBranch()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        var shiftId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var refundedSaleId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
        var paidSaleId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        SeedShift(db, shiftId, ShiftStateNames.Open, ReportDay.AddHours(8), countedCash: 0);
        SeedSale(db, refundedSaleId, shiftId, PosSaleStateNames.Refunded, 2400, ReportDay.AddHours(11), refundedAtUtc: ReportDay.AddHours(12));
        SeedSale(db, paidSaleId, shiftId, PosSaleStateNames.Paid, 5000, ReportDay.AddHours(13));
        SeedSale(db, Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"), shiftId, PosSaleStateNames.Paid, 9999, ReportDay.AddHours(14), OtherBranchId);
        SeedSaleLine(db, refundedSaleId, quantity: 2, lineTotal: 2400);
        SeedSaleLine(db, paidSaleId, quantity: 1, lineTotal: 5000);
        SeedPayment(db, refundedSaleId, refundedSaleId, PaymentMethodNames.Cash, "payment", 2400);
        SeedPayment(db, refundedSaleId, refundedSaleId, PaymentMethodNames.Cash, "refund", -2400);
        SeedPayment(db, paidSaleId, paidSaleId, PaymentMethodNames.CardManual, "payment", 5000);
        await db.SaveChangesAsync();

        var result = await service.GetSalesReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 500),
            CancellationToken.None);

        Assert.Equal(200, result.Limit);
        Assert.Equal(7400, result.GrossSalesTotal.MinorUnits);
        Assert.Equal(-2400, result.RefundsTotal.MinorUnits);
        Assert.Equal(5000, result.NetSalesTotal.MinorUnits);
        Assert.Collection(
            result.Rows,
            first =>
            {
                Assert.Equal(paidSaleId, first.PosSaleId);
                Assert.Equal(5000, first.PaidAmount.MinorUnits);
                Assert.Equal(1, first.ItemQuantity);
            },
            second =>
            {
                Assert.Equal(refundedSaleId, second.PosSaleId);
                Assert.Equal(2400, second.PaidAmount.MinorUnits);
                Assert.Equal(-2400, second.RefundAmount.MinorUnits);
                Assert.Equal(2, second.ItemQuantity);
            });
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static void SeedShift(
        PlatformDbContext db,
        Guid shiftId,
        string state,
        DateTimeOffset openedAtUtc,
        long countedCash,
        Guid? branchId = null)
    {
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId ?? TestIds.BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            ClosedByStaffUserId = state == ShiftStateNames.Closed ? ActorStaffUserId : null,
            State = state,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = countedCash,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "front register",
            ClosingNote = state == ShiftStateNames.Closed ? "balanced" : string.Empty,
            OpenedAtUtc = openedAtUtc,
            ClosedAtUtc = state == ShiftStateNames.Closed ? openedAtUtc.AddHours(8) : null
        });
    }

    private static void SeedCashMovement(
        PlatformDbContext db,
        Guid shiftId,
        string movementType,
        long amountMinorUnits)
    {
        db.CashMovements.Add(new CashMovementEntity
        {
            CashMovementId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = shiftId,
            CreatedByStaffUserId = ActorStaffUserId,
            MovementType = movementType,
            CurrencyCode = "TJS",
            AmountMinorUnits = amountMinorUnits,
            Reason = "report seed",
            CreatedAtUtc = ReportDay.AddHours(10)
        });
    }

    private static void SeedSale(
        PlatformDbContext db,
        Guid saleId,
        Guid shiftId,
        string state,
        long totalMinorUnits,
        DateTimeOffset createdAtUtc,
        Guid? branchId = null,
        DateTimeOffset? refundedAtUtc = null)
    {
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId ?? TestIds.BranchId,
            ShiftId = shiftId,
            CreatedByStaffUserId = ActorStaffUserId,
            State = state,
            CurrencyCode = "TJS",
            TotalMinorUnits = totalMinorUnits,
            RefundReason = state == PosSaleStateNames.Refunded ? "returned" : string.Empty,
            VoidReason = string.Empty,
            CreatedAtUtc = createdAtUtc,
            PaidAtUtc = state is PosSaleStateNames.Paid or PosSaleStateNames.Refunded ? createdAtUtc.AddMinutes(1) : null,
            RefundedAtUtc = refundedAtUtc,
            VoidedAtUtc = null
        });
    }

    private static void SeedSaleLine(
        PlatformDbContext db,
        Guid saleId,
        int quantity,
        long lineTotal)
    {
        db.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            PosSaleId = saleId,
            ProductId = Guid.NewGuid(),
            ProductName = "Energy drink",
            Quantity = quantity,
            CurrencyCode = "TJS",
            UnitPriceMinorUnits = lineTotal / quantity,
            LineTotalMinorUnits = lineTotal,
            TrackStock = true,
            AllowNegativeStock = false
        });
    }

    private static void SeedPayment(
        PlatformDbContext db,
        Guid shiftId,
        Guid saleId,
        string paymentMethod,
        string paymentKind,
        long amountMinorUnits)
    {
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PosSaleId = saleId,
            ShiftId = shiftId,
            CreatedByStaffUserId = ActorStaffUserId,
            PaymentKind = paymentKind,
            Provider = "manual",
            PaymentMethod = paymentMethod,
            CurrencyCode = "TJS",
            AmountMinorUnits = amountMinorUnits,
            Note = "report seed",
            CreatedAtUtc = ReportDay.AddHours(12)
        });
    }

    private static void SeedLedgerEntry(
        PlatformDbContext db,
        Guid shiftId,
        string entryType,
        long amountMinorUnits)
    {
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = shiftId,
            PlayerAccountId = Guid.NewGuid(),
            SessionId = null,
            PlayerPackageId = null,
            EntryType = entryType,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "report seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = ReportDay.AddHours(11)
        });
    }
}
