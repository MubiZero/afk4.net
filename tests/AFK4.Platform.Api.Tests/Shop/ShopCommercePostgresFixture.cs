using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Commerce;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Payments;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopCommercePostgresFixture : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-13T12:00:00Z");
    private readonly string connectionString;
    private readonly string schemaName;
    private readonly ServiceProvider services;
    private readonly SettlementOverlapGate overlapGate;
    private readonly SerializationRetryLogger retryLogger;
    private readonly SaveOverlapGate saveOverlapGate;

    private ShopCommercePostgresFixture(
        string connectionString,
        string schemaName,
        ServiceProvider services,
        SettlementOverlapGate overlapGate,
        SerializationRetryLogger retryLogger,
        SaveOverlapGate saveOverlapGate)
    {
        this.connectionString = connectionString;
        this.schemaName = schemaName;
        this.services = services;
        this.overlapGate = overlapGate;
        this.retryLogger = retryLogger;
        this.saveOverlapGate = saveOverlapGate;
    }

    public Guid OrganizationId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");
    public Guid BranchId { get; } = Guid.Parse("22222222-2222-4222-8222-222222222222");
    public Guid PlayerAccountId { get; } = Guid.Parse("33333333-3333-4333-8333-333333333333");
    public Guid SessionId { get; } = Guid.Parse("44444444-4444-4444-8444-444444444444");
    public Guid SeatId { get; } = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public Guid StaffUserId { get; } = Guid.Parse("66666666-6666-4666-8666-666666666666");
    public Guid ProductId { get; } = Guid.Parse("77777777-7777-4777-8777-777777777777");
    public int InitialSuccessfulSettlementCount => overlapGate.InitialSuccessfulSettlementCount;
    public int SerializationRetryCount => retryLogger.SerializationRetryCount;

    public static async Task<ShopCommercePostgresFixture> CreateAsync(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database?.EndsWith("_test", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidOperationException("Commerce PostgreSQL tests require a database name ending in _test.");
        }

        var schemaName = $"commerce_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE SCHEMA \"{schemaName}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.SearchPath = schemaName;
        var scopedConnectionString = builder.ConnectionString;
        var overlapGate = new SettlementOverlapGate();
        var retryLogger = new SerializationRetryLogger();
        var saveOverlapGate = new SaveOverlapGate();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContext<PlatformDbContext>(options =>
            options.UseNpgsql(scopedConnectionString).AddInterceptors(saveOverlapGate));
        collection.AddSingleton(TimeProvider.System);
        collection.AddSingleton<IShopOrderNotifier, NoOpShopOrderNotifier>();
        collection.AddScoped<IWalletSettlementService, EfWalletSettlementService>();
        collection.AddScoped<IInventoryCostService, EfInventoryCostService>();
        collection.AddScoped<IInventoryService, EfInventoryService>();
        collection.AddScoped<IPaymentProvider, ManualPaymentProvider>();
        collection.AddScoped<IPosService, EfPosService>();
        collection.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
        collection.AddScoped<EfShopPosSettlementService>();
        collection.AddScoped<IShopPosSettlementService>(provider =>
            new OverlappingShopPosSettlementService(
                provider.GetRequiredService<EfShopPosSettlementService>(),
                overlapGate));
        collection.AddScoped<ILoyaltyAccrualService, LoyaltyAccrualService>();
        collection.AddScoped<IShopOrderWorkflow, EfShopOrderWorkflow>();
        collection.AddScoped<IShopCommerceCoordinator, EfShopCommerceCoordinator>();
        collection.AddSingleton<ILogger<EfShopCommerceCoordinator>>(retryLogger);
        var services = collection.BuildServiceProvider(validateScopes: true);

        var fixture = new ShopCommercePostgresFixture(
            scopedConnectionString,
            schemaName,
            services,
            overlapGate,
            retryLogger,
            saveOverlapGate);
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

    public async Task SeedLastUnitScenarioAsync(int stock, long walletMinorUnits)
    {
        await using var db = CreateDbContext();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Name = "PostgreSQL Commerce Test",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Slug = "postgres-commerce-test",
            Name = "Central",
            PreferredLocale = "ru",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = OrganizationId,
            HomeBranchId = BranchId,
            DisplayName = "Alex",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = SeatId,
            PlayerAccountId = PlayerAccountId,
            State = SessionStateNames.Active,
            PlayerKind = "registered",
            TariffRuleVersionId = "v1",
            Version = 1
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
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
            Name = "Last Cola",
            Sku = "LAST-COLA",
            CurrencyCode = "TJS",
            PriceMinorUnits = 500,
            AvgCostMinorUnits = 100,
            TrackStock = true,
            AllowNegativeStock = false,
            IsActive = true,
            AvailableInShell = true,
            CreatedAtUtc = Now
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ProductId = ProductId,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = stock,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 100,
            Reason = "test seed",
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = Now.AddMinutes(-5)
        });
        db.LedgerEntries.Add(BillingEntryFactory.Create(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            null,
            LedgerEntryTypeNames.TopUp,
            LedgerAccountTypeNames.Wallet,
            walletMinorUnits,
            0,
            "TJS",
            "seed",
            "test seed",
            null,
            StaffUserId,
            Now.AddMinutes(-10)));
        await db.SaveChangesAsync();
    }

    public async Task<ShopOrderActionResult> PlaceInIndependentScopeAsync(string idempotencyKey)
    {
        await using var scope = services.CreateAsyncScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<IShopCommerceCoordinator>();
        return await coordinator.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(ProductId, 1)], idempotencyKey),
            CancellationToken.None);
    }

    public async Task<(Guid CategoryId, Guid ShiftId)> SeedFirstHistoryCurrencyScenarioAsync()
    {
        var categoryId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        await using var db = CreateDbContext();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrganizationId,
            Name = "PostgreSQL Currency Race Test",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Slug = "postgres-currency-race",
            Name = "Central",
            PreferredLocale = "ru",
            CreatedAtUtc = Now
        });
        db.PosProductCategories.Add(new PosProductCategoryEntity
        {
            CategoryId = categoryId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            Name = "Drinks",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = shiftId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            OpenedByStaffUserId = StaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            OpeningNote = "currency race",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now.AddHours(-1)
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = ProductId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            CategoryId = categoryId,
            Name = "First Sale Cola",
            Sku = "FIRST-SALE-COLA",
            CurrencyCode = "TJS",
            PriceMinorUnits = 500,
            TrackStock = false,
            AllowNegativeStock = false,
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (categoryId, shiftId);
    }

    public void ArmSaveOverlap() => saveOverlapGate.Arm();

    public async Task<BillingCommandServiceResult<PosSaleDto>> CreateSaleInIndependentScopeAsync(
        Guid shiftId,
        string idempotencyKey)
    {
        await using var scope = services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPosService>();
        return await service.CreateSaleAsync(
            BranchId,
            StaffUserId,
            new CreatePosSaleRequest(
                OrganizationId,
                shiftId,
                [new PosSaleLineDto(ProductId, string.Empty, 1, new MoneyDto("TJS", 0), new MoneyDto("TJS", 0))],
                idempotencyKey),
            CancellationToken.None);
    }

    public async Task<BillingCommandServiceResult<PosProductDto>> UpdateProductCurrencyInIndependentScopeAsync(
        Guid categoryId,
        string currencyCode)
    {
        await using var scope = services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        return await service.UpdateProductAsync(
            BranchId,
            ProductId,
            StaffUserId,
            new UpdateProductRequest(
                OrganizationId,
                categoryId,
                "First Sale Cola",
                "FIRST-SALE-COLA",
                new MoneyDto(currencyCode, 600),
                TrackStock: false,
                AllowNegativeStock: false,
                IsActive: true),
            CancellationToken.None);
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

    private sealed class NoOpShopOrderNotifier : IShopOrderNotifier
    {
        public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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

    private sealed class OverlappingShopPosSettlementService(
        EfShopPosSettlementService inner,
        SettlementOverlapGate overlapGate) : IShopPosSettlementService
    {
        public async Task<ShopPosSettlementResult> CreatePaidWalletSaleAsync(
            ShopPosSaleRequest request,
            CancellationToken cancellationToken)
        {
            var result = await inner.CreatePaidWalletSaleAsync(request, cancellationToken);
            if (result.Succeeded)
            {
                await overlapGate.WaitForBothInitialSuccessfulSettlementsAsync(cancellationToken);
            }

            return result;
        }

        public Task<ShopPosSettlementResult> RefundPaidWalletSaleAsync(
            ShopPosRefundRequest request,
            CancellationToken cancellationToken) =>
            inner.RefundPaidWalletSaleAsync(request, cancellationToken);
    }

    private sealed class SettlementOverlapGate
    {
        private readonly TaskCompletionSource bothInitialSettlementsReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int successfulSettlementCount;

        public int InitialSuccessfulSettlementCount =>
            Math.Min(Volatile.Read(ref successfulSettlementCount), 2);

        public async Task WaitForBothInitialSuccessfulSettlementsAsync(CancellationToken cancellationToken)
        {
            var arrival = Interlocked.Increment(ref successfulSettlementCount);
            if (arrival > 2)
            {
                return;
            }

            if (arrival == 2)
            {
                bothInitialSettlementsReached.TrySetResult();
            }

            await bothInitialSettlementsReached.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private sealed class SerializationRetryLogger : ILogger<EfShopCommerceCoordinator>
    {
        private int serializationRetryCount;

        public int SerializationRetryCount => Volatile.Read(ref serializationRetryCount);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning &&
                formatter(state, exception).StartsWith(
                    "Retrying serialized shop placement attempt",
                    StringComparison.Ordinal))
            {
                Interlocked.Increment(ref serializationRetryCount);
            }
        }
    }
}
