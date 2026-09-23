using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Reports;

/// <summary>
/// Отчёты владельца на настоящей базе, с границами дня филиала.
///
/// Соседние тесты на InMemory считают арифметику периода, но не доказывают, что PostgreSQL примет
/// запрос: InMemory сравнивает <see cref="DateTimeOffset"/> в .NET, а Npgsql не пишет ненулевое
/// смещение в <c>timestamp with time zone</c>. Ежедневная сводка на этом уже падала в staging
/// 78 раз подряд при зелёных тестах. Здесь филиал живёт в Asia/Dushanbe (UTC+5), и записи стоят
/// ровно на местной полуночи и в последнюю микросекунду суток — так видно и смещение, и
/// полуоткрытый интервал, и то, в какой день тренда попадает запись.
/// </summary>
public sealed class OrganizationAdminReportPostgresTests
{
    private static readonly Guid OrgId = Guid.Parse("f1f1f1f1-f1f1-4f1f-8f1f-f1f1f1f1f1f1");
    private static readonly Guid BranchId = Guid.Parse("f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f2f2");
    private static readonly Guid CashierId = Guid.Parse("f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f3f3");
    private static readonly Guid ShiftId = Guid.Parse("f4f4f4f4-f4f4-4f4f-8f4f-f4f4f4f4f4f4");
    private static readonly DateOnly ReportDate = new(2026, 7, 29);

    // Местная полночь 29 июля в Душанбе — 19:00 UTC накануне.
    private static readonly DateTimeOffset LocalMidnight = DateTimeOffset.Parse("2026-07-28T19:00:00Z");
    private static readonly DateTimeOffset NextLocalMidnight = LocalMidnight.AddDays(1);

    [PlatformAdminPostgresFact]
    public async Task Reports_AgainstPostgres_CountTheBranchDayWithItsTrendAndPreviousDay()
    {
        await using var schema = await ReportSchema.CreateAsync();
        await using (var seed = schema.CreateDbContext())
        {
            Seed(seed);
            await seed.SaveChangesAsync();
        }

        await using var db = schema.CreateDbContext();
        var service = new OrganizationAdminReportService(db, new EfReportService(db));

        var summary = await service.GetSummaryAsync(OrgId, BranchId, ReportDate, ReportDate, CancellationToken.None);
        var revenue = await service.GetRevenueAsync(OrgId, BranchId, ReportDate, ReportDate, CancellationToken.None);
        var shiftCash = await service.GetShiftCashAsync(OrgId, BranchId, ReportDate, ReportDate, CancellationToken.None);

        Assert.Equal(LocalMidnight, summary.Period.FromUtc);
        Assert.Equal(NextLocalMidnight, summary.Period.ToUtc);
        Assert.Equal("Asia/Dushanbe", summary.Period.TimeZone);

        // Продажа ровно в местную полночь и в последнюю микросекунду суток — внутри; продажа
        // в следующую полночь — уже не этот день.
        Assert.Equal(5_000, summary.Figures.PosNetSales.MinorUnits);
        Assert.Equal(1_500, summary.Figures.GameplayRevenue.MinorUnits);
        Assert.Equal(6_500, summary.Figures.NetRevenue.MinorUnits);

        // Неделя по дням филиала: последний день тренда — те же деньги, что в цифрах сводки,
        // 23:59:59 местного 28-го — накануне, 27-е — игра.
        Assert.Equal(
            Enumerable.Range(0, 7).Select(offset => ReportDate.AddDays(offset - 6)),
            summary.Trend.Select(point => point.Date));
        Assert.Equal(
            new long[] { 0, 0, 0, 0, 1_000, 4_000, 6_500 },
            summary.Trend.Select(point => point.NetRevenue.MinorUnits));
        Assert.NotNull(summary.ActiveShift);
        Assert.Equal(ShiftId, summary.ActiveShift.ShiftId);

        Assert.Equal(6_500, revenue.NetRevenue.MinorUnits);
        Assert.Equal(4_000, revenue.Comparison.PreviousNetRevenue.MinorUnits);
        Assert.Equal(2_500, revenue.Comparison.DifferenceMinorUnits);
        Assert.Equal(62.5m, revenue.Comparison.ChangePercent);
        Assert.Equal(
            new[] { (PaymentMethodNames.Cash, 3_000L), (PaymentMethodNames.CardManual, 2_000L) },
            revenue.PaymentMethods.Select(row => (row.Key, row.Revenue.MinorUnits)));
        var cashier = Assert.Single(revenue.Operators);
        Assert.Equal(("Cashier Dilnoza", 6_500L), (cashier.Label, cashier.Revenue.MinorUnits));
        var revenueCsv = OrganizationAdminReportCsvExporter.Export(revenue);
        Assert.Contains("\"comparison\",\"previous_net\",\"TJS\",4000,2026-07-28,2026-07-28", revenueCsv);
        Assert.Contains("\"operator\",\"Cashier Dilnoza\",\"TJS\",6500,2026-07-29,2026-07-29", revenueCsv);

        // Смена открыта ровно в местную полночь: начало периода включительно.
        Assert.Equal(ShiftId, Assert.Single(shiftCash.Shifts).ShiftId);
        Assert.Contains($"\"shift\",\"{ShiftId:D}\"", OrganizationAdminReportCsvExporter.Export(shiftCash));
    }

    private static void Seed(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrgId, Slug = "reports-club", Name = "Reports Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}",
            CreatedAtUtc = LocalMidnight.AddDays(-30), UpdatedAtUtc = LocalMidnight.AddDays(-30)
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId, OrganizationId = OrgId, Slug = "central", Name = "Central",
            PreferredTimeZone = "Asia/Dushanbe", CreatedAtUtc = LocalMidnight.AddDays(-30)
        });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = CashierId, OrganizationId = OrgId, UserName = "dilnoza", NormalizedUserName = "DILNOZA",
            DisplayName = "Cashier Dilnoza", PasswordHash = "x", IsActive = true, CreatedAtUtc = LocalMidnight.AddDays(-30)
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = ShiftId, OrganizationId = OrgId, BranchId = BranchId, OpenedByStaffUserId = CashierId,
            State = ShiftStateNames.Open, CurrencyCode = "TJS", OpenedAtUtc = LocalMidnight
        });

        PaidSale(db, 3_000, PaymentMethodNames.Cash, LocalMidnight);
        PaidSale(db, 2_000, PaymentMethodNames.CardManual, NextLocalMidnight.AddTicks(-10));
        PaidSale(db, 9_000, PaymentMethodNames.Cash, NextLocalMidnight);
        PaidSale(db, 4_000, PaymentMethodNames.Cash, LocalMidnight.AddSeconds(-1));

        PlayedSession(db, 1_500, LocalMidnight.AddHours(15));
        PlayedSession(db, 1_000, LocalMidnight.AddDays(-2).AddHours(17));
    }

    private static void PaidSale(PlatformDbContext db, long amountMinorUnits, string paymentMethod, DateTimeOffset at)
    {
        var saleId = Guid.NewGuid();
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId, OrganizationId = OrgId, BranchId = BranchId, ShiftId = ShiftId,
            CreatedByStaffUserId = CashierId, State = PosSaleStateNames.Paid, CurrencyCode = "TJS",
            TotalMinorUnits = amountMinorUnits, CreatedAtUtc = at, PaidAtUtc = at
        });
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, PosSaleId = saleId,
            ShiftId = ShiftId, CreatedByStaffUserId = CashierId, PaymentKind = "payment", Provider = "manual",
            PaymentMethod = paymentMethod, CurrencyCode = "TJS", AmountMinorUnits = amountMinorUnits, CreatedAtUtc = at
        });
    }

    private static void PlayedSession(PlatformDbContext db, long chargeMinorUnits, DateTimeOffset startedAt)
    {
        var sessionId = Guid.NewGuid();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId, OrganizationId = OrgId, BranchId = BranchId, SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(), CreatedByStaffUserId = CashierId, PlayerKind = "guest",
            TariffRuleVersionId = "tariff-v1", State = "ended", RequestedAtUtc = startedAt,
            StartedAtUtc = startedAt, EndsAtUtc = startedAt.AddHours(1), EndedAtUtc = startedAt.AddHours(1),
            UpdatedAtUtc = startedAt.AddHours(1)
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ShiftId = ShiftId,
            PlayerAccountId = Guid.NewGuid(), SessionId = sessionId, EntryType = LedgerEntryTypeNames.GameplayCharge,
            AccountType = LedgerAccountTypeNames.Wallet, AmountMinorUnits = -chargeMinorUnits, CurrencyCode = "TJS",
            Description = LedgerEntryTypeNames.GameplayCharge, Reason = "played", CreatedByStaffUserId = CashierId,
            CreatedAtUtc = startedAt
        });
    }

    private sealed class ReportSchema : IAsyncDisposable
    {
        private readonly string rootConnectionString;
        private readonly string name;
        private readonly DbContextOptions<PlatformDbContext> options;

        private ReportSchema(string rootConnectionString, string name)
        {
            this.rootConnectionString = rootConnectionString;
            this.name = name;
            options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(rootConnectionString) { SearchPath = name }.ConnectionString)
                .Options;
        }

        public static async Task<ReportSchema> CreateAsync()
        {
            var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
            var schema = new ReportSchema(connectionString, $"owner_reports_{Guid.NewGuid():N}");
            await schema.ExecuteAsync($"CREATE SCHEMA \"{schema.name}\"");
            await using var db = schema.CreateDbContext();
            await db.Database.MigrateAsync();
            return schema;
        }

        public PlatformDbContext CreateDbContext() => new(options);

        public async ValueTask DisposeAsync() => await ExecuteAsync($"DROP SCHEMA \"{name}\" CASCADE");

        private async Task ExecuteAsync(string sql)
        {
            await using var connection = new NpgsqlConnection(rootConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}
