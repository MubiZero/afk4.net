using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Commerce;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shop;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Platform.Api.Tests;

public sealed class BillingShiftIntegrationTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T13:00:00Z");

    [Fact]
    public async Task TopUpWalletAsync_RequiresAnOpenShift()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        var shiftService = CreateShiftService(db);
        var billing = new EfBillingCommandService(db, shiftService, new FixedTimeProvider(Now), new LoyaltyAccrualService(db));

        var result = await billing.TopUpWalletAsync(
            PlayerAccountId,
            TestIds.BranchId,
            ActorStaffUserId,
            new TopUpWalletRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", 5000),
                "front desk cash top-up",
                "topup-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Conflict);
        Assert.Contains("open shift", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.LedgerEntries.ToListAsync());
    }

    [Fact]
    public async Task TopUpWalletAsync_WritesShiftIdOnLedgerEntry()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        var shiftService = CreateShiftService(db);
        var shift = await OpenShiftAsync(shiftService);
        var billing = new EfBillingCommandService(db, shiftService, new FixedTimeProvider(Now), new LoyaltyAccrualService(db));

        var result = await billing.TopUpWalletAsync(
            PlayerAccountId,
            TestIds.BranchId,
            ActorStaffUserId,
            new TopUpWalletRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", 5000),
                "front desk cash top-up",
                "topup-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var entry = await db.LedgerEntries.SingleAsync();
        Assert.Equal(shift.ShiftId, entry.ShiftId);
    }

    [Fact]
    public async Task PurchasePackageAsync_WritesShiftIdOnPackagePurchaseLedgerEntries()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var packageDefinition = await SeedPackageDefinitionAsync(db);
        var shiftService = CreateShiftService(db);
        var shift = await OpenShiftAsync(shiftService);
        var packages = new EfPackageService(db, shiftService, new FixedTimeProvider(Now));

        var result = await packages.PurchasePackageAsync(
            PlayerAccountId,
            TestIds.BranchId,
            ActorStaffUserId,
            new PurchasePackageRequest(TestIds.OrganizationId, packageDefinition.PackageDefinitionId, "package-purchase-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var packageEntries = await db.LedgerEntries
            .Where(entry => entry.EntryType != LedgerEntryTypeNames.TopUp)
            .ToListAsync();
        Assert.NotEmpty(packageEntries);
        Assert.All(packageEntries, entry => Assert.Equal(shift.ShiftId, entry.ShiftId));
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPrepaidWallet_WritesShiftIdOnGameplayCharge()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        var shiftService = CreateShiftService(db);
        var shift = await OpenShiftAsync(shiftService);
        var dispatcher = new RecordingCommandDispatchService();
        var service = CreateSessionService(db, dispatcher, shiftService);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-prepaid-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var charge = await db.LedgerEntries.SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge);
        Assert.Equal(shift.ShiftId, charge.ShiftId);
    }

    [Fact]
    public async Task LinkedShopPaymentAndRefund_ProjectIntoCurrentShiftExactlyOnce()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 10_000);
        var shift = await OpenShiftAsync(CreateShiftService(db));
        var productId = Guid.NewGuid();
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            PlayerAccountId = PlayerAccountId,
            PlayerKind = "registered",
            State = SessionStateNames.Active,
            TariffRuleVersionId = "v1",
            Version = 1
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = productId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            CategoryId = Guid.NewGuid(),
            Name = "Cola",
            Sku = "COLA-SHIFT",
            CurrencyCode = "TJS",
            PriceMinorUnits = 500,
            AvgCostMinorUnits = 100,
            TrackStock = true,
            IsActive = true,
            AvailableInShell = true,
            CreatedAtUtc = Now
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ProductId = productId,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = 1,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 100,
            Reason = "seed",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        var commerce = CreateCommerceCoordinator(db);
        var reportService = new EfReportService(db);

        var placed = await commerce.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 1)], "shift-shop-place"),
            CancellationToken.None);

        Assert.True(placed.Succeeded);
        var payment = Assert.Single(await db.Payments.Where(candidate => candidate.PaymentKind == "payment").ToListAsync());
        Assert.Equal(shift.ShiftId, payment.ShiftId);
        Assert.Equal(500, payment.AmountMinorUnits);
        var afterPlacement = await reportService.GetCurrentShiftRevenueAsync(
            TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);
        Assert.Equal(500, afterPlacement!.Earned.Goods.MinorUnits);

        var cancelled = await commerce.CancelByPlayerAsync(
            PlayerAccountId, placed.Order!.Id, CancellationToken.None);

        Assert.True(cancelled.Succeeded);
        var refund = Assert.Single(await db.Payments.Where(candidate => candidate.PaymentKind == "refund").ToListAsync());
        Assert.Equal(shift.ShiftId, refund.ShiftId);
        Assert.Equal(-500, refund.AmountMinorUnits);
        Assert.Single(await db.Payments.Where(candidate => candidate.PaymentKind == "payment").ToListAsync());
        var afterRefund = await reportService.GetCurrentShiftRevenueAsync(
            TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None);
        Assert.Equal(0, afterRefund!.Earned.Goods.MinorUnits);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfShiftService CreateShiftService(PlatformDbContext db)
    {
        return new EfShiftService(db, new FixedTimeProvider(Now));
    }

    private static EfSessionCommandService CreateSessionService(
        PlatformDbContext db,
        RecordingCommandDispatchService dispatcher,
        IOpenShiftResolver openShiftResolver)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new EfSessionCommandService(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            timeProvider,
            new SessionBillingService(db, new EfTariffService(db, timeProvider), openShiftResolver, timeProvider),
            new RecordingSessionLifecycleNotifier());
    }

    private static EfShopCommerceCoordinator CreateCommerceCoordinator(PlatformDbContext db)
    {
        var notifier = new NoOpShopOrderNotifier();
        var settlement = new EfShopPosSettlementService(
            db,
            new EfWalletSettlementService(db),
            new EfInventoryCostService(db),
            new ReceiptNumberGenerator(db));
        var workflow = new EfShopOrderWorkflow(
            db,
            new FixedTimeProvider(Now),
            notifier,
            new LoyaltyAccrualService(db));
        return new EfShopCommerceCoordinator(
            db,
            workflow,
            settlement,
            new FixedTimeProvider(Now),
            notifier,
            NullLogger<EfShopCommerceCoordinator>.Instance);
    }

    private static async Task<ShiftDto> OpenShiftAsync(EfShiftService service)
    {
        var result = await service.OpenShiftAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new OpenShiftRequest(
                TestIds.OrganizationId,
                new MoneyDto("TJS", 50000),
                "front register",
                $"shift-open-{Guid.NewGuid():N}"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        return result.Response;
    }

    private static async Task SeedPlayerAsync(PlatformDbContext db)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private sealed class NoOpShopOrderNotifier : IShopOrderNotifier
    {
        public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static async Task SeedWalletTopUpAsync(PlatformDbContext db, long amountMinorUnits)
    {
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.TopUp,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = LedgerEntryTypeNames.TopUp,
            Reason = "test seed",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task<PackageDefinitionEntity> SeedPackageDefinitionAsync(PlatformDbContext db)
    {
        var definition = new PackageDefinitionEntity
        {
            PackageDefinitionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "NIGHT 5H",
            CurrencyCode = "TJS",
            PriceMinorUnits = 4000,
            IncludedSeconds = 18000,
            BonusSeconds = 1800,
            ExpiresAfterDays = 30,
            IsActive = true,
            CreatedAtUtc = Now
        };
        db.PackageDefinitions.Add(definition);
        await db.SaveChangesAsync();

        return definition;
    }

    private static async Task SeedLayoutAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Org",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = ZoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = TestIds.DeviceId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            EnrolledAtUtc = Now,
            IsOnline = true,
            IsLocked = true
        });
        db.DeviceSeatAssignments.Add(new DeviceSeatAssignmentEntity
        {
            DeviceSeatAssignmentId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
            AttachedAtUtc = Now
        });

        await db.SaveChangesAsync();
    }

    private static async Task<TariffVersionEntity> SeedTariffVersionAsync(PlatformDbContext db)
    {
        var tariff = new TariffEntity
        {
            TariffId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = Now
        };
        var version = new TariffVersionEntity
        {
            TariffVersionId = Guid.NewGuid(),
            TariffId = tariff.TariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = Now.AddMinutes(-1),
            RetiredAtUtc = null,
            CreatedAtUtc = Now
        };

        db.Tariffs.Add(tariff);
        db.TariffVersions.Add(version);
        await db.SaveChangesAsync();

        return version;
    }

    private sealed class RecordingCommandDispatchService : IDeviceCommandDispatchService
    {
        public Task<DeviceCommandDto> DispatchAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateCommand(request));
        }

        public Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateCommand(request));
        }

        public Task NotifyAsync(
            Guid deviceId,
            DeviceCommandDto command,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static DeviceCommandDto CreateCommand(CreateDeviceCommandRequest request)
        {
            return new DeviceCommandDto(Guid.NewGuid(), request.Type, Now, request.Payload);
        }
    }

    private sealed class FakeSessionLeaseSigner : ISessionLeaseSigner
    {
        public SessionLeaseDto Sign(
            Guid SessionId,
            Guid OrganizationId,
            Guid BranchId,
            Guid SeatId,
            Guid DeviceId,
            string State,
            int Sequence,
            DateTimeOffset IssuedAtUtc,
            DateTimeOffset ExpiresAtUtc)
        {
            return new SessionLeaseDto(
                SessionId,
                OrganizationId,
                BranchId,
                SeatId,
                DeviceId,
                State,
                Sequence,
                IssuedAtUtc,
                ExpiresAtUtc,
                EcdsaSessionLeaseSigner.SignatureAlgorithm,
                $"fake-signature-{Sequence}");
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
