using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Reports;

public sealed class OrganizationAdminReportServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_CapsAttentionAndReturnsSevenDayTrendAndActiveShift()
    {
        await using var db = CreateDbContext();
        db.Branches.Add(new BranchEntity { BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Slug = "central", Name = "Central", PreferredTimeZone = "Asia/Dushanbe" });
        db.Shifts.Add(new ShiftEntity { ShiftId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, OpenedByStaffUserId = Guid.NewGuid(), State = ShiftStateNames.Open, CurrencyCode = "TJS", OpenedAtUtc = DateTimeOffset.Parse("2026-07-29T05:00:00Z") });
        db.AuditRecords.Add(new AuditRecordEntity { AuditRecordId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Action = AuditActionNames.RefundPosSale, TargetType = "PosSale", TargetId = Guid.NewGuid().ToString("D"), Outcome = AuditOutcome.Denied, SourceApp = "organization-admin", AmountMinorUnits = 2_000, CreatedAtUtc = DateTimeOffset.Parse("2026-07-29T06:00:00Z") });
        await db.SaveChangesAsync();
        var service = new OrganizationAdminReportService(db, new RevenueReportStub());

        var result = await service.GetSummaryAsync(TestIds.OrganizationId, TestIds.BranchId, new DateOnly(2026, 7, 29), new DateOnly(2026, 7, 29), CancellationToken.None);

        Assert.Equal(6, result.AttentionTotalCount);
        Assert.Equal(3, result.AttentionItems.Count);
        Assert.Equal(7, result.Trend.Count);
        Assert.NotNull(result.ActiveShift);
        Assert.True(result.ActiveShift.IsProvisional);
    }

    [Fact]
    public async Task GetRevenueAsync_UsesBranchDayAndBuildsPreviousPeriodComparison()
    {
        await using var db = CreateDbContext();
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Slug = "central",
            Name = "Central",
            PreferredTimeZone = "Asia/Dushanbe"
        });
        // Деньги — движениями по их дню: бар 6 000 − 1 000 и игра 2 000 в отчётный день, бар
        // 4 000 − 1 000 и игра 2 000 — в предыдущий.
        db.Payments.Add(Pos("payment", 6_000, DateTimeOffset.Parse("2026-07-29T07:00:00Z")));
        db.Payments.Add(Pos("refund", -1_000, DateTimeOffset.Parse("2026-07-29T07:30:00Z")));
        db.Payments.Add(Pos("payment", 4_000, DateTimeOffset.Parse("2026-07-28T07:00:00Z")));
        db.Payments.Add(Pos("refund", -1_000, DateTimeOffset.Parse("2026-07-28T07:30:00Z")));
        db.LedgerEntries.Add(Gameplay(LedgerEntryTypeNames.GameplayCharge, -2_000, DateTimeOffset.Parse("2026-07-29T08:00:00Z")));
        db.LedgerEntries.Add(Gameplay(LedgerEntryTypeNames.GameplayCharge, -2_000, DateTimeOffset.Parse("2026-07-28T08:00:00Z")));
        await db.SaveChangesAsync();
        var reports = new RevenueReportStub();
        var service = new OrganizationAdminReportService(db, reports);

        var result = await service.GetRevenueAsync(
            TestIds.OrganizationId, TestIds.BranchId,
            new DateOnly(2026, 7, 29), new DateOnly(2026, 7, 29),
            CancellationToken.None);

        Assert.Equal(DateTimeOffset.Parse("2026-07-28T19:00:00Z"), result.Period.FromUtc);
        Assert.Equal(7_000, result.NetRevenue.MinorUnits);
        Assert.Equal(5_000, result.Comparison.PreviousNetRevenue.MinorUnits);
        Assert.Equal(2_000, result.Comparison.DifferenceMinorUnits);
        Assert.Equal(40m, result.Comparison.ChangePercent);
        Assert.Equal(2, reports.Queries.Count);
        Assert.All(reports.Queries.Take(2), query => Assert.Equal(DateTimeOffset.Parse("2026-07-28T19:00:00Z"), query.FromUtc));
    }

    // Одно правило для денег на всех экранах отчёта (решение владельца 2026-09-23): по дню, когда
    // деньги прошли, за вычетом возвратов. Раньше сводка и «Выручка» брали продажи и сессии,
    // начатые в периоде, — возврат приписывался дню продажи, возврат за игру не вычитался, — а
    // тренд и разбивки считали движения по их дню, и итог дня не совпадал с точкой графика.
    [Fact]
    public async Task GameplayMoney_IsCountedByChargeDayNetOfRefunds_OnEveryReport()
    {
        await using var db = CreateDbContext();
        db.Branches.Add(new BranchEntity { BranchId = TestIds.BranchId, OrganizationId = TestIds.OrganizationId, Slug = "central", Name = "Central", PreferredTimeZone = "Asia/Dushanbe" });
        // Продажа накануне, возврат по ней — в отчётный день: возврат уменьшает отчётный день.
        db.Payments.Add(Pos("payment", 6_000, DateTimeOffset.Parse("2026-07-29T03:00:00Z")));
        db.Payments.Add(Pos("payment", 4_000, DateTimeOffset.Parse("2026-07-28T05:00:00Z")));
        db.Payments.Add(Pos("refund", -1_000, DateTimeOffset.Parse("2026-07-29T04:00:00Z")));
        // Сессия началась накануне, но доплата списана в отчётный день — это деньги отчётного дня.
        db.LedgerEntries.Add(Gameplay(LedgerEntryTypeNames.GameplayCharge, -3_000, DateTimeOffset.Parse("2026-07-29T02:00:00Z")));
        // Возврат за игру в тот же день уменьшает выручку.
        db.LedgerEntries.Add(Gameplay(LedgerEntryTypeNames.Refund, 500, DateTimeOffset.Parse("2026-07-29T09:00:00Z")));
        // Списание накануне в отчётный день не попадает.
        db.LedgerEntries.Add(Gameplay(LedgerEntryTypeNames.GameplayCharge, -9_000, DateTimeOffset.Parse("2026-07-28T10:00:00Z")));
        await db.SaveChangesAsync();
        var service = new OrganizationAdminReportService(db, new RevenueReportStub());
        var day = new DateOnly(2026, 7, 29);

        var summary = await service.GetSummaryAsync(TestIds.OrganizationId, TestIds.BranchId, day, day, CancellationToken.None);
        var revenue = await service.GetRevenueAsync(TestIds.OrganizationId, TestIds.BranchId, day, day, CancellationToken.None);

        Assert.Equal(2_500, summary.Figures.GameplayRevenue.MinorUnits);
        Assert.Equal(5_000 + 2_500, summary.Figures.NetRevenue.MinorUnits);
        Assert.Equal(summary.Figures.NetRevenue, summary.Trend[^1].NetRevenue);
        Assert.Equal(2_500, revenue.GameplayRevenue.MinorUnits);
        Assert.Equal(6_000 + 3_000, revenue.GrossRevenue.MinorUnits);
        Assert.Equal(-1_000 - 500, revenue.Refunds.MinorUnits);
        Assert.Equal(5_000, revenue.PosNetSales.MinorUnits);
        Assert.Equal(summary.Figures.NetRevenue, revenue.NetRevenue);
        // Кассиры и способы оплаты делят те же деньги, что в итоге.
        Assert.Equal(revenue.NetRevenue.MinorUnits, revenue.Operators.Sum(row => row.Revenue.MinorUnits));
        Assert.Equal(revenue.PosNetSales.MinorUnits, revenue.PaymentMethods.Sum(row => row.Revenue.MinorUnits));
    }

    private static PaymentEntity Pos(string kind, long amountMinorUnits, DateTimeOffset at) => new()
    {
        PaymentId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
        PosSaleId = Guid.NewGuid(), ShiftId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(), PaymentKind = kind,
        Provider = "manual", PaymentMethod = "cash", CurrencyCode = "TJS", AmountMinorUnits = amountMinorUnits, CreatedAtUtc = at
    };

    private static LedgerEntryEntity Gameplay(string entryType, long amountMinorUnits, DateTimeOffset at) => new()
    {
        LedgerEntryId = Guid.NewGuid(), OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
        PlayerAccountId = Guid.NewGuid(), SessionId = Guid.NewGuid(), EntryType = entryType,
        AccountType = LedgerAccountTypeNames.Wallet, AmountMinorUnits = amountMinorUnits, CurrencyCode = "TJS",
        Description = entryType, Reason = "played", CreatedByStaffUserId = Guid.NewGuid(), CreatedAtUtc = at
    };

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"organization-admin-reports-{Guid.NewGuid():N}")
            .Options;
        return new PlatformDbContext(options);
    }

    private sealed class RevenueReportStub : IReportService
    {
        public List<ReportSearchQuery> Queries { get; } = [];

        public Task<SalesReportResultDto> GetSalesReportAsync(Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            var current = query.FromUtc == DateTimeOffset.Parse("2026-07-28T19:00:00Z");
            var gross = current ? 6_000 : 4_000;
            var refunds = -1_000;
            return Task.FromResult(new SalesReportResultDto([], 200, Money(gross), Money(refunds), Money(gross + refunds), Money(0), Money(0), Money(0)));
        }

        public Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(new GameplayTimeReportResultDto([], 200, 7_200, 0, 0, Money(2_000)));
        }

        public Task<ShiftReportResultDto> GetShiftReportAsync(Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ShiftReportResultDto(Enumerable.Range(0, 5).Select(index =>
                new ShiftReportRowDto(Guid.NewGuid(), organizationId, branchId, Guid.NewGuid(), Guid.NewGuid(), ShiftStateNames.Closed,
                    Money(0), Money(0), Money(0), Money(0), Money(0), Money(10_000), Money(9_500), Money(-500),
                    DateTimeOffset.Parse("2026-07-29T01:00:00Z").AddHours(index), DateTimeOffset.Parse("2026-07-29T02:00:00Z").AddHours(index))).ToList(), 200));
        public Task<CashOperationReportResultDto> GetCashOperationReportAsync(Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ShiftRevenueListDto> GetShiftRevenueAsync(Guid organizationId, Guid branchId, ReportSearchQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ShiftRevenueDto?> GetCurrentShiftRevenueAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        private static MoneyDto Money(long value) => new("TJS", value);
    }
}
