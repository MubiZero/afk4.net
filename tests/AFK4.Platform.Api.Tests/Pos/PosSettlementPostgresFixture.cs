using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Pos;

public sealed class PosSettlementPostgresFixture : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-14T12:00:00Z");
    private readonly string connectionString;
    private readonly string schemaName;
    private readonly ServiceProvider services;
    private readonly SaveOverlapGate overlapGate;
    private readonly SwitchableReceiptNumberGenerator receiptNumberGenerator;

    private PosSettlementPostgresFixture(
        string connectionString,
        string schemaName,
        ServiceProvider services,
        SaveOverlapGate overlapGate,
        SwitchableReceiptNumberGenerator receiptNumberGenerator)
    {
        this.connectionString = connectionString;
        this.schemaName = schemaName;
        this.services = services;
        this.overlapGate = overlapGate;
        this.receiptNumberGenerator = receiptNumberGenerator;
    }

    public Guid OrganizationId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public Guid BranchId { get; } = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public Guid PlayerAccountId { get; } = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public Guid ShiftId { get; } = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public Guid StaffUserId { get; } = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public Guid SaleId { get; } = Guid.Parse("66666666-6666-4666-8666-666666666666");
    public Guid ProductId { get; } = Guid.Parse("77777777-7777-4777-8777-777777777777");

    public static async Task<PosSettlementPostgresFixture> CreateAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database?.EndsWith("_test", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException("POS PostgreSQL tests require a database name ending in _test.");
        }

        var schemaName = $"pos_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.SearchPath = schemaName;
        var scopedConnectionString = builder.ConnectionString;
        var overlapGate = new SaveOverlapGate();
        var receiptGenerator = new SwitchableReceiptNumberGenerator();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(scopedConnectionString).AddInterceptors(overlapGate));
        collection.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        collection.AddScoped<IWalletSettlementService, EfWalletSettlementService>();
        collection.AddScoped<IInventoryCostService, EfInventoryCostService>();
        collection.AddSingleton(receiptGenerator);
        collection.AddScoped<IReceiptNumberGenerator>(provider =>
            new DelegatingReceiptNumberGenerator(
                provider.GetRequiredService<PlatformDbContext>(),
                provider.GetRequiredService<SwitchableReceiptNumberGenerator>()));
        collection.AddScoped<IPosSettlementService, EfPosSettlementService>();
        var services = collection.BuildServiceProvider(validateScopes: true);

        var fixture = new PosSettlementPostgresFixture(
            scopedConnectionString,
            schemaName,
            services,
            overlapGate,
            receiptGenerator);
        try
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            await db.Database.MigrateAsync();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PlatformDbContext(options);
    }

    public async Task SeedDraftMixedSaleAsync(bool withReceiptNumberCollision = false)
    {
        await using var db = CreateDbContext();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Name = "PostgreSQL POS Test",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Slug = "postgres-pos-test",
            Name = "Central",
            PreferredLocale = "ru",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = OrganizationId,
            HomeBranchId = BranchId,
            DisplayName = "Player",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = ShiftId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            OpenedByStaffUserId = StaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            OpenedAtUtc = Now.AddHours(-1)
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = ProductId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            CategoryId = Guid.NewGuid(),
            Name = "Tracked Cola",
            Sku = "POS-RACE-COLA",
            CurrencyCode = "TJS",
            PriceMinorUnits = 5_000,
            AvgCostMinorUnits = 1_500,
            TrackStock = true,
            AllowNegativeStock = false,
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = SaleId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = ShiftId,
            CreatedByStaffUserId = StaffUserId,
            PlayerAccountId = PlayerAccountId,
            State = PosSaleStateNames.Draft,
            CurrencyCode = "TJS",
            TotalMinorUnits = 10_000,
            CreatedAtUtc = Now
        });
        db.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            PosSaleId = SaleId,
            ProductId = ProductId,
            ProductName = "Tracked Cola",
            Quantity = 2,
            CurrencyCode = "TJS",
            UnitPriceMinorUnits = 5_000,
            UnitCostMinorUnits = 1_500,
            LineTotalMinorUnits = 10_000,
            TracksStock = true,
            AllowNegativeStock = false
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ProductId = ProductId,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = 10,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 1_500,
            Reason = "test seed",
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now.AddMinutes(-5)
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = ShiftId,
            PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.TopUp,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 20_000,
            CurrencyCode = "TJS",
            Description = "seed",
            Reason = "test seed",
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now.AddMinutes(-10)
        });

        if (withReceiptNumberCollision)
        {
            db.Receipts.Add(new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                PosSaleId = SaleId,
                ReceiptNumber = SwitchableReceiptNumberGenerator.CollisionNumber,
                ReceiptType = "sale",
                CurrencyCode = "TJS",
                TotalMinorUnits = 1,
                Locale = "ru",
                CreatedAtUtc = Now.AddMinutes(-1)
            });
            receiptNumberGenerator.UseCollisionNumber = true;
        }

        await db.SaveChangesAsync();
    }

    public void ArmOverlap() => overlapGate.Arm();

    public async Task<BillingCommandServiceResult<PosSaleDto>> SettleInIndependentScopeAsync(string idempotencyKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPosSettlementService>().SettleAsync(
            SaleId,
            StaffUserId,
            new SettlePosSaleRequest(
                OrganizationId,
                [
                    new PaymentPartDto(PaymentMethodNames.Wallet, new MoneyDto("TJS", 4_000)),
                    new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", 6_000))
                ],
                "postgres mixed settlement",
                idempotencyKey),
            CancellationToken.None);
    }

    public async Task<BillingCommandServiceResult<PosSaleDto>> RefundInIndependentScopeAsync(string idempotencyKey)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPosSettlementService>().RefundAsync(
            SaleId,
            StaffUserId,
            new RefundPosSaleRequest(OrganizationId, "postgres mixed refund", idempotencyKey),
            CancellationToken.None);
    }

    public async Task<IReadOnlyList<string>> GetAppliedMigrationsAsync()
    {
        await using var db = CreateDbContext();
        return (await db.Database.GetAppliedMigrationsAsync()).ToList();
    }

    public async Task<Guid> InsertStandaloneLedgerEntryAsync()
    {
        var id = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = id,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = ShiftId,
            PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.WalletPayment,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = -1,
            CurrencyCode = "TJS",
            Description = "constraint seed",
            Reason = "constraint seed",
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return id;
    }

    public async Task InsertPaymentWithLedgerLinkAsync(Guid ledgerEntryId)
    {
        await using var db = CreateDbContext();
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PosSaleId = SaleId,
            ShiftId = ShiftId,
            CreatedByStaffUserId = StaffUserId,
            PaymentKind = "payment",
            Provider = "wallet",
            PaymentMethod = PaymentMethodNames.Wallet,
            LedgerEntryId = ledgerEntryId,
            CurrencyCode = "TJS",
            AmountMinorUnits = 1,
            Note = "constraint proof",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = null };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP SCHEMA IF EXISTS \"{schemaName}\" CASCADE";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class SaveOverlapGate : SaveChangesInterceptor
    {
        private TaskCompletionSource? bothSavesReached;
        private int saveCount;
        private int armed;

        public void Arm()
        {
            bothSavesReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref saveCount, 0);
            Volatile.Write(ref armed, 1);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 0)
            {
                return result;
            }

            var reached = Interlocked.Increment(ref saveCount);
            if (reached == 2)
            {
                Volatile.Write(ref armed, 0);
                bothSavesReached!.TrySetResult();
            }

            await bothSavesReached!.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }

    private sealed class SwitchableReceiptNumberGenerator
    {
        public const string CollisionNumber = "POS-COLLISION-0001";
        public bool UseCollisionNumber { get; set; }
    }

    private sealed class DelegatingReceiptNumberGenerator(
        PlatformDbContext db,
        SwitchableReceiptNumberGenerator switcher) : IReceiptNumberGenerator
    {
        private readonly ReceiptNumberGenerator inner = new(db);

        public Task<string> GenerateAsync(
            Guid organizationId,
            Guid branchId,
            string receiptType,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            switcher.UseCollisionNumber
                ? Task.FromResult(SwitchableReceiptNumberGenerator.CollisionNumber)
                : inner.GenerateAsync(organizationId, branchId, receiptType, now, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
