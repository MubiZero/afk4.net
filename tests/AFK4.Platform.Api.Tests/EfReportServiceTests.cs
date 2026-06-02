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
        SeedShiftLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.TopUp, 10000);
        SeedShiftLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.DebtPayment, -3000);
        SeedShiftLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.ManualCorrection, -500);
        SeedShiftLedgerEntry(db, closedShiftId, LedgerEntryTypeNames.GameplayCharge, -9000);
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

    [Fact]
    public async Task GetGameplayTimeReportAsync_ReturnsSessionRowsWithLedgerAggregates()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        var endedSessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var activeSessionId = Guid.Parse("55555555-5555-4555-8555-555555555555");
        SeedSession(db, endedSessionId, "registered", ReportDay.AddHours(10), ReportDay.AddHours(12));
        SeedSession(db, activeSessionId, "guest", ReportDay.AddHours(13), null, ReportDay.AddHours(15));
        SeedSession(
            db,
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            "guest",
            ReportDay.AddHours(14),
            ReportDay.AddHours(15),
            branchId: OtherBranchId);
        SeedSessionLedgerEntry(db, endedSessionId, LedgerEntryTypeNames.GameplayCharge, -12000);
        SeedSessionLedgerEntry(db, endedSessionId, LedgerEntryTypeNames.PostpaidDebt, 3000);
        SeedSessionLedgerEntry(db, endedSessionId, LedgerEntryTypeNames.PackageConsumption, 0, quantitySeconds: -1800);
        SeedSessionLedgerEntry(db, endedSessionId, LedgerEntryTypeNames.BonusConsumption, 0, quantitySeconds: -600);
        await db.SaveChangesAsync();

        var result = await service.GetGameplayTimeReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 10),
            CancellationToken.None);

        Assert.Equal(14400, result.TotalDurationSeconds);
        Assert.Equal(1800, result.TotalPackageSeconds);
        Assert.Equal(600, result.TotalBonusSeconds);
        Assert.Equal(15000, result.GameplayRevenueTotal.MinorUnits);
        Assert.Collection(
            result.Rows,
            first =>
            {
                Assert.Equal(activeSessionId, first.SessionId);
                Assert.Equal(7200, first.DurationSeconds);
                Assert.Equal(0, first.GameplayRevenue.MinorUnits);
            },
            second =>
            {
                Assert.Equal(endedSessionId, second.SessionId);
                Assert.Equal(7200, second.DurationSeconds);
                Assert.Equal(1800, second.PackageSeconds);
                Assert.Equal(600, second.BonusSeconds);
                Assert.Equal(15000, second.GameplayRevenue.MinorUnits);
            });
    }

    [Fact]
    public async Task GetCashOperationReportAsync_ReturnsCashImpactRowsAndTotals()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        var shiftId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var saleId = Guid.Parse("77777777-7777-4777-8777-777777777777");
        SeedShift(db, shiftId, ShiftStateNames.Open, ReportDay.AddHours(8), countedCash: 0);
        SeedCashMovement(db, shiftId, CashMovementTypeNames.CashIn, 5000, ReportDay.AddHours(9));
        SeedCashMovement(db, shiftId, CashMovementTypeNames.CashOut, 1000, ReportDay.AddHours(10));
        SeedPayment(db, shiftId, saleId, PaymentMethodNames.Cash, "payment", 2400, ReportDay.AddHours(11));
        SeedPayment(db, shiftId, saleId, PaymentMethodNames.Cash, "refund", -1200, ReportDay.AddHours(12));
        SeedPayment(db, shiftId, saleId, PaymentMethodNames.CardManual, "payment", 9999, ReportDay.AddHours(13));
        SeedShiftLedgerEntry(db, shiftId, LedgerEntryTypeNames.TopUp, 10000, createdAtUtc: ReportDay.AddHours(14));
        SeedShiftLedgerEntry(db, shiftId, LedgerEntryTypeNames.DebtPayment, -3000, createdAtUtc: ReportDay.AddHours(15));
        SeedShiftLedgerEntry(db, shiftId, LedgerEntryTypeNames.ManualCorrection, -500, createdAtUtc: ReportDay.AddHours(16));
        SeedShiftLedgerEntry(db, shiftId, LedgerEntryTypeNames.Refund, -700, createdAtUtc: ReportDay.AddHours(17));
        await db.SaveChangesAsync();

        var result = await service.GetCashOperationReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 20),
            CancellationToken.None);

        Assert.Equal(20400, result.CashInTotal.MinorUnits);
        Assert.Equal(-3400, result.CashOutTotal.MinorUnits);
        Assert.Equal(17000, result.NetCashTotal.MinorUnits);
        Assert.Equal(8, result.Rows.Count);
        Assert.Equal(LedgerEntryTypeNames.Refund, result.Rows[0].OperationType);
        Assert.Equal(-700, result.Rows[0].CashImpact.MinorUnits);
        Assert.DoesNotContain(result.Rows, row => row.OperationType == PaymentMethodNames.CardManual);
    }

    [Fact]
    public async Task GetOperatorActionReportAsync_GroupsAuditRowsByActorActionAndOutcome()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ActorStaffUserId,
            OrganizationId = TestIds.OrganizationId,
            UserName = "manager",
            NormalizedUserName = "MANAGER",
            DisplayName = "Manager One",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAtUtc = ReportDay
        });
        SeedAuditRecord(db, ActorStaffUserId, "sessions.start", "succeeded", ReportDay.AddHours(9));
        SeedAuditRecord(db, ActorStaffUserId, "sessions.start", "succeeded", ReportDay.AddHours(10));
        SeedAuditRecord(db, ActorStaffUserId, "sessions.end", "denied", ReportDay.AddHours(11));
        SeedAuditRecord(db, null, "system.cleanup", "succeeded", ReportDay.AddHours(12));
        SeedAuditRecord(db, ActorStaffUserId, "sessions.start", "succeeded", ReportDay.AddHours(13), branchId: OtherBranchId);
        await db.SaveChangesAsync();

        var result = await service.GetOperatorActionReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 10),
            CancellationToken.None);

        Assert.Equal(4, result.TotalActionCount);
        Assert.Collection(
            result.Rows,
            first =>
            {
                Assert.Null(first.ActorStaffUserId);
                Assert.Equal("System", first.ActorDisplayName);
                Assert.Equal("system.cleanup", first.Action);
                Assert.Equal(1, first.Count);
            },
            second =>
            {
                Assert.Equal(ActorStaffUserId, second.ActorStaffUserId);
                Assert.Equal("Manager One", second.ActorDisplayName);
                Assert.Equal("sessions.end", second.Action);
                Assert.Equal("denied", second.Outcome);
                Assert.Equal(1, second.Count);
            },
            third =>
            {
                Assert.Equal(ActorStaffUserId, third.ActorStaffUserId);
                Assert.Equal("sessions.start", third.Action);
                Assert.Equal(2, third.Count);
            });
    }

    [Fact]
    public async Task GetOperatorActionReportAsync_FilterByActor_ReturnsOnlyThatActorsRows()
    {
        var otherActorId = Guid.Parse("66666666-6666-4666-8666-666666666666");
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        SeedAuditRecord(db, ActorStaffUserId, "money_action.refund", "succeeded", ReportDay.AddHours(9));
        SeedAuditRecord(db, ActorStaffUserId, "money_action.refund", "succeeded", ReportDay.AddHours(10));
        SeedAuditRecord(db, otherActorId, "money_action.refund", "succeeded", ReportDay.AddHours(11));
        await db.SaveChangesAsync();

        var result = await service.GetOperatorActionReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 10, ActorStaffUserId: ActorStaffUserId),
            CancellationToken.None);

        Assert.Equal(2, result.TotalActionCount);
        var row = Assert.Single(result.Rows);
        Assert.Equal(ActorStaffUserId, row.ActorStaffUserId);
        Assert.Equal("money_action.refund", row.Action);
        Assert.Equal(2, row.Count);
    }

    [Fact]
    public async Task GetOperatorActionReportAsync_FilterByAmountRange_ExcludesOutOfRangeAndAmountlessRows()
    {
        await using var db = CreateDbContext();
        var service = new EfReportService(db);
        SeedAuditRecord(db, ActorStaffUserId, "money_action.refund", "succeeded", ReportDay.AddHours(9), amountMinorUnits: 3000);
        SeedAuditRecord(db, ActorStaffUserId, "money_action.refund", "succeeded", ReportDay.AddHours(10), amountMinorUnits: 8000);
        SeedAuditRecord(db, ActorStaffUserId, "sessions.start", "succeeded", ReportDay.AddHours(11));
        await db.SaveChangesAsync();

        var result = await service.GetOperatorActionReportAsync(
            TestIds.OrganizationId,
            TestIds.BranchId,
            new ReportSearchQuery(ReportDay, ReportDay.AddDays(1), 10, MinAmountMinorUnits: 5000),
            CancellationToken.None);

        Assert.Equal(1, result.TotalActionCount);
        var row = Assert.Single(result.Rows);
        Assert.Equal("money_action.refund", row.Action);
        Assert.Equal(1, row.Count);
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
        long amountMinorUnits,
        DateTimeOffset? createdAtUtc = null)
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
            CreatedAtUtc = createdAtUtc ?? ReportDay.AddHours(10)
        });
    }

    private static void SeedSession(
        PlatformDbContext db,
        Guid sessionId,
        string playerKind,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? endedAtUtc,
        DateTimeOffset? endsAtUtc = null,
        Guid? branchId = null)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId ?? TestIds.BranchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = ActorStaffUserId,
            PlayerKind = playerKind,
            PlayerAccountId = playerKind == "registered" ? Guid.NewGuid() : null,
            TariffRuleVersionId = "tariff-v1",
            State = endedAtUtc is null ? "active" : "ended",
            RequestedAtUtc = startedAtUtc.AddMinutes(-1),
            StartedAtUtc = startedAtUtc,
            EndsAtUtc = endsAtUtc ?? endedAtUtc,
            EndedAtUtc = endedAtUtc,
            CurrentLeaseId = null,
            UpdatedAtUtc = endedAtUtc ?? startedAtUtc
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
        long amountMinorUnits,
        DateTimeOffset? createdAtUtc = null)
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
            CreatedAtUtc = createdAtUtc ?? ReportDay.AddHours(12)
        });
    }

    private static void SeedShiftLedgerEntry(
        PlatformDbContext db,
        Guid shiftId,
        string entryType,
        long amountMinorUnits,
        int quantitySeconds = 0,
        DateTimeOffset? createdAtUtc = null)
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
            QuantitySeconds = quantitySeconds,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "report seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = createdAtUtc ?? ReportDay.AddHours(11)
        });
    }

    private static void SeedSessionLedgerEntry(
        PlatformDbContext db,
        Guid sessionId,
        string entryType,
        long amountMinorUnits,
        int quantitySeconds = 0)
    {
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = Guid.NewGuid(),
            PlayerAccountId = Guid.NewGuid(),
            SessionId = sessionId,
            PlayerPackageId = null,
            EntryType = entryType,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "report seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = ReportDay.AddHours(11)
        });
    }

    private static void SeedAuditRecord(
        PlatformDbContext db,
        Guid? actorStaffUserId,
        string action,
        string outcome,
        DateTimeOffset createdAtUtc,
        Guid? branchId = null,
        long? amountMinorUnits = null)
    {
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = branchId ?? TestIds.BranchId,
            ActorStaffUserId = actorStaffUserId,
            Action = action,
            TargetType = "ReportSeed",
            TargetId = null,
            Outcome = outcome,
            SourceApp = "test",
            DetailsJson = "{}",
            AmountMinorUnits = amountMinorUnits,
            CreatedAtUtc = createdAtUtc
        });
    }
}
