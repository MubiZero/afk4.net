using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfSessionBillingIntegrationTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerPackageId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task StartGuestSessionAsync_WithNoBilling_StartsGuestSessionWithoutPlayerOrLedger()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "manual-v1",
                IdempotencyKey: "start-guest-no-billing-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal("manual-v1", result.Response.Session.TariffRuleVersionId);
        Assert.Empty(db.LedgerEntries);
        var enqueued = Assert.Single(dispatcher.Enqueued);
        Assert.Equal("unlock", enqueued.Command.Type);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPrepaidWallet_DebitsWalletBeforeUnlockCommand()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db, requireGameplayChargeBeforeDispatch: true);
        var service = CreateService(db, dispatcher);

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
        Assert.NotNull(result.Response);
        var enqueued = Assert.Single(dispatcher.Enqueued);
        Assert.Equal(enqueued.Command.CommandId, result.Response.DeviceCommands[0].CommandId);
        Assert.Contains(db.LedgerEntries, entry =>
            entry.EntryType == LedgerEntryTypeNames.GameplayCharge &&
            entry.AccountType == LedgerAccountTypeNames.Wallet &&
            entry.AmountMinorUnits < 0 &&
            entry.SessionId == result.Response!.Session.SessionId);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPrepaidWallet_RejectsInsufficientFundsAndDispatchesNoDeviceCommand()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 1000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-prepaid-insufficient-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(db.Sessions);
        Assert.Empty(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge));
        Assert.Empty(dispatcher.Enqueued);
        Assert.Empty(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPrepaidWallet_RejectsMismatchedExistingLedgerCurrency()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000, currencyCode: "USD");
        var tariffVersion = await SeedTariffVersionAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-prepaid-currency-mismatch-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Requested currency must match the player ledger currency.", result.Error);
        Assert.Empty(db.Sessions);
        Assert.Empty(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge));
        Assert.Empty(dispatcher.Enqueued);
        Assert.Empty(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPrepaidWallet_RejectsMixedExistingLedgerCurrencies()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000, currencyCode: "USD");
        await SeedWalletTopUpAsync(db, 5000, currencyCode: "TJS");
        var tariffVersion = await SeedTariffVersionAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-prepaid-mixed-currency-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Player ledger contains multiple currencies.", result.Error);
        Assert.Empty(db.Sessions);
        Assert.Empty(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge));
        Assert.Empty(dispatcher.Enqueued);
        Assert.Empty(dispatcher.Calls);
    }

    [Fact]
    public async Task ExtendSessionAsync_WithPrepaidWallet_DebitsAdditionalGameplayChargeAndRefreshesLease()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 10000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);
        var start = await StartPrepaidSessionAsync(service, tariffVersion.TariffVersionId, "start-prepaid-extend-001");
        Assert.NotNull(start.Response);
        dispatcher.Clear();

        var result = await service.ExtendSessionAsync(
            start.Response.Session.SessionId,
            ActorStaffUserId,
            new ExtendSessionRequest(
                AdditionalMinutes: 30,
                TariffRuleVersionId: "ignored-manual-v2",
                IdempotencyKey: "extend-prepaid-001",
                PlayerAccountId,
                BillingModeNames.PrepaidWallet,
                tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(tariffVersion.TariffVersionId.ToString("D"), result.Response.Session.TariffRuleVersionId);
        Assert.Equal(2, await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge));
        var enqueued = Assert.Single(dispatcher.Enqueued);
        Assert.Equal(result.Response.DeviceCommands[0].CommandId, enqueued.Command.CommandId);
        var call = Assert.Single(dispatcher.Calls);
        Assert.Equal("refresh-session-lease", call.Command.Type);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPostpaidDebt_AppendsPostpaidDebtAndAllowsSession()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-postpaid-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PostpaidDebt,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Contains(db.LedgerEntries, entry =>
            entry.EntryType == LedgerEntryTypeNames.PostpaidDebt &&
            entry.AccountType == LedgerAccountTypeNames.Debt &&
            entry.AmountMinorUnits > 0 &&
            entry.SessionId == result.Response.Session.SessionId);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPackage_AppendsPackageConsumptionAndAllowsSession()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedPlayerPackageAsync(db, includedSeconds: 7200, bonusSeconds: 0);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 7200);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-package-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.Package,
                TariffVersionId: null,
                PlayerPackageId: PlayerPackageId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal($"package:{PlayerPackageId:D}", result.Response.Session.TariffRuleVersionId);
        Assert.Contains(db.LedgerEntries, entry =>
            entry.EntryType == LedgerEntryTypeNames.PackageConsumption &&
            entry.AccountType == LedgerAccountTypeNames.PackageTime &&
            entry.QuantitySeconds == -3600 &&
            entry.SessionId == result.Response.Session.SessionId &&
            entry.PlayerPackageId == PlayerPackageId);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_WithPackage_RejectsInsufficientPackageSecondsAndDispatchesNoDeviceCommand()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedPlayerPackageAsync(db, includedSeconds: 1200, bonusSeconds: 0);
        await SeedPackageGrantAsync(db, LedgerEntryTypeNames.PackagePurchase, LedgerAccountTypeNames.PackageTime, 1200);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-package-insufficient-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.Package,
                TariffVersionId: null,
                PlayerPackageId: PlayerPackageId),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(db.Sessions);
        Assert.Empty(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PackageConsumption));
        Assert.Empty(dispatcher.Enqueued);
        Assert.Empty(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_IdempotencyReplay_ReturnsOriginalResponseWithoutDuplicateLedgerEntries()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);
        var request = new StartGuestSessionRequest(
            TestIds.OrganizationId,
            SeatId,
            DurationMode: SessionDurationModes.Fixed,
            DurationMinutes: 60,
            TariffRuleVersionId: "ignored-manual-v1",
            IdempotencyKey: "start-prepaid-idempotent-001",
            PlayerAccountId: PlayerAccountId,
            BillingMode: BillingModeNames.PrepaidWallet,
            TariffVersionId: tariffVersion.TariffVersionId,
            PlayerPackageId: null);

        var first = await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.StartGuestSessionAsync(TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.Session.SessionId, second.Response.Session.SessionId);
        Assert.Single(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.GameplayCharge));
        Assert.Single(dispatcher.Enqueued);
        Assert.Single(dispatcher.Calls);
    }

    [Fact]
    public async Task StartGuestSessionAsync_EnqueuesCommandForResponseThenNotifiesOnce()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-prepaid-notify-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var enqueued = Assert.Single(dispatcher.Enqueued);
        var notified = Assert.Single(dispatcher.Calls);
        Assert.Empty(dispatcher.DispatchCalls);
        Assert.Equal(result.Response.DeviceCommands[0].CommandId, enqueued.Command.CommandId);
        Assert.Equal(enqueued.Command.CommandId, notified.Command.CommandId);
        Assert.Equal("unlock", notified.Command.Type);
    }

    [Fact]
    public async Task ExtendSessionAsync_EnqueuesCommandForResponseThenNotifiesOnce()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 10000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);
        var start = await StartPrepaidSessionAsync(service, tariffVersion.TariffVersionId, "start-prepaid-notify-extend-001");
        Assert.NotNull(start.Response);
        dispatcher.Clear();

        var result = await service.ExtendSessionAsync(
            start.Response.Session.SessionId,
            ActorStaffUserId,
            new ExtendSessionRequest(
                AdditionalMinutes: 30,
                TariffRuleVersionId: "ignored-manual-v2",
                IdempotencyKey: "extend-prepaid-notify-001",
                PlayerAccountId,
                BillingModeNames.PrepaidWallet,
                tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        var enqueued = Assert.Single(dispatcher.Enqueued);
        var notified = Assert.Single(dispatcher.Calls);
        Assert.Empty(dispatcher.DispatchCalls);
        Assert.Equal(result.Response.DeviceCommands[0].CommandId, enqueued.Command.CommandId);
        Assert.Equal(enqueued.Command.CommandId, notified.Command.CommandId);
        Assert.Equal("refresh-session-lease", notified.Command.Type);
    }

    [Fact]
    public async Task StartGuestSessionAsync_OpenTabPostpaid_StartsWithoutEndOrStartCharge()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Open,
                DurationMinutes: null,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-open-postpaid-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PostpaidDebt,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(SessionStateNames.Active, session.State);
        Assert.Null(session.EndsAtUtc);
        // Open-tab postpaid defers the charge to checkout: no debt entry at start.
        Assert.Empty(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PostpaidDebt));
        Assert.Single(dispatcher.Enqueued);
    }

    [Fact]
    public async Task StartGuestSessionAsync_OpenTabPrepaid_IsRejected()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);

        var result = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Open,
                DurationMinutes: null,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-open-prepaid-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(db.Sessions);
        Assert.Empty(dispatcher.Enqueued);
    }

    [Fact]
    public async Task ComputeCheckoutChargeAsync_OpenTabPostpaid_ReturnsAccruedAmount()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);
        var start = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Open,
                DurationMinutes: null,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-open-postpaid-charge-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PostpaidDebt,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);
        Assert.NotNull(start.Response);

        var billing = CreateBillingService(db);
        // 40 min elapsed -> max(40, min 30) = 40 -> round up to 15-min increment = 45 -> 45 * 50 = 2250.
        var charge = await billing.ComputeCheckoutChargeAsync(
            start.Response.Session.SessionId,
            Now.AddMinutes(40),
            CancellationToken.None);

        Assert.True(charge.Succeeded);
        Assert.Equal(2250, charge.AmountMinorUnits);
        Assert.Equal(tariffVersion.TariffVersionId, charge.TariffVersionId);
        Assert.Equal("TJS", charge.CurrencyCode);
    }

    [Fact]
    public async Task AppendCheckoutLedgerEntriesAsync_OpenTabPostpaid_WritesSingleDebtEntry()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        await SeedPlayerAsync(db);
        var tariffVersion = await SeedTariffVersionAsync(db);
        await SeedOpenShiftAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);
        var start = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Open,
                DurationMinutes: null,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: "start-open-postpaid-append-001",
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PostpaidDebt,
                TariffVersionId: tariffVersion.TariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);
        Assert.NotNull(start.Response);
        var sessionId = start.Response.Session.SessionId;

        var billing = CreateBillingService(db);
        var checkoutTime = Now.AddMinutes(40);
        var charge = await billing.ComputeCheckoutChargeAsync(sessionId, checkoutTime, CancellationToken.None);
        await billing.AppendCheckoutLedgerEntriesAsync(
            sessionId,
            ActorStaffUserId,
            charge,
            PlayerAccountId,
            checkoutTime,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var debt = Assert.Single(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PostpaidDebt));
        Assert.Equal(2250, debt.AmountMinorUnits);
        Assert.Equal(LedgerAccountTypeNames.Debt, debt.AccountType);
        Assert.Equal(sessionId, debt.SessionId);
    }

    [Fact]
    public async Task ComputeCheckoutChargeAsync_GuestSession_ReturnsZeroCharge()
    {
        await using var db = CreateDbContext();
        await SeedLayoutAsync(db);
        var dispatcher = new RecordingCommandDispatchService(db);
        var service = CreateService(db, dispatcher);
        var start = await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Open,
                DurationMinutes: null,
                TariffRuleVersionId: "manual-v1",
                IdempotencyKey: "start-open-guest-charge-001"),
            CancellationToken.None);
        Assert.NotNull(start.Response);

        var billing = CreateBillingService(db);
        var charge = await billing.ComputeCheckoutChargeAsync(
            start.Response.Session.SessionId,
            Now.AddMinutes(40),
            CancellationToken.None);

        Assert.True(charge.Succeeded);
        Assert.Equal(0, charge.AmountMinorUnits);
    }

    private static SessionBillingService CreateBillingService(PlatformDbContext db)
    {
        var timeProvider = new FixedTimeProvider(Now);
        return new SessionBillingService(
            db,
            new EfTariffService(db, timeProvider),
            new EfShiftService(db, timeProvider),
            timeProvider);
    }

    private static async Task<SessionCommandServiceResult> StartPrepaidSessionAsync(
        EfSessionCommandService service,
        Guid tariffVersionId,
        string idempotencyKey)
    {
        return await service.StartGuestSessionAsync(
            TestIds.BranchId,
            ActorStaffUserId,
            new StartGuestSessionRequest(
                TestIds.OrganizationId,
                SeatId,
                DurationMode: SessionDurationModes.Fixed,
                DurationMinutes: 60,
                TariffRuleVersionId: "ignored-manual-v1",
                IdempotencyKey: idempotencyKey,
                PlayerAccountId: PlayerAccountId,
                BillingMode: BillingModeNames.PrepaidWallet,
                TariffVersionId: tariffVersionId,
                PlayerPackageId: null),
            CancellationToken.None);
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PlatformDbContext(options);
    }

    private static EfSessionCommandService CreateService(
        PlatformDbContext db,
        RecordingCommandDispatchService dispatcher)
    {
        var timeProvider = new FixedTimeProvider(Now);
        var shiftService = new EfShiftService(db, timeProvider);
        return new EfSessionCommandService(
            db,
            dispatcher,
            new FakeSessionLeaseSigner(),
            timeProvider,
            new SessionBillingService(db, new EfTariffService(db, timeProvider), shiftService, timeProvider));
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

    private static async Task SeedOpenShiftAsync(PlatformDbContext db)
    {
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedWalletTopUpAsync(
        PlatformDbContext db,
        long amountMinorUnits,
        string currencyCode = "TJS")
    {
        db.LedgerEntries.Add(CreateLedgerEntry(
            LedgerEntryTypeNames.TopUp,
            LedgerAccountTypeNames.Wallet,
            amountMinorUnits,
            quantitySeconds: 0,
            sessionId: null,
            playerPackageId: null,
            currencyCode));
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

    private static async Task SeedPlayerPackageAsync(
        PlatformDbContext db,
        int includedSeconds,
        int bonusSeconds)
    {
        db.PlayerPackages.Add(new PlayerPackageEntity
        {
            PlayerPackageId = PlayerPackageId,
            PackageDefinitionId = Guid.Parse("99999999-9999-4999-8999-999999999999"),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            Name = "NIGHT 5H",
            CurrencyCode = "TJS",
            PurchasedPriceMinorUnits = 4000,
            IncludedSeconds = includedSeconds,
            BonusSeconds = bonusSeconds,
            PurchasedAtUtc = Now,
            ExpiresAtUtc = Now.AddDays(30)
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPackageGrantAsync(
        PlatformDbContext db,
        string entryType,
        string accountType,
        int quantitySeconds)
    {
        db.LedgerEntries.Add(CreateLedgerEntry(
            entryType,
            accountType,
            amountMinorUnits: 0,
            quantitySeconds,
            sessionId: null,
            PlayerPackageId));
        await db.SaveChangesAsync();
    }

    private static LedgerEntryEntity CreateLedgerEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        int quantitySeconds,
        Guid? sessionId,
        Guid? playerPackageId,
        string currencyCode = "TJS")
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = PlayerAccountId,
            SessionId = sessionId,
            PlayerPackageId = playerPackageId,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = quantitySeconds,
            CurrencyCode = currencyCode,
            Description = entryType,
            Reason = "test seed",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        };
    }

    private sealed class RecordingCommandDispatchService(
        PlatformDbContext dbContext,
        bool requireGameplayChargeBeforeDispatch = false) : IDeviceCommandDispatchService
    {
        public List<(Guid DeviceId, CreateDeviceCommandRequest Request, DeviceCommandDto Command)> DispatchCalls { get; } = [];

        public List<(Guid DeviceId, CreateDeviceCommandRequest Request, DeviceCommandDto Command)> Enqueued { get; } = [];

        public List<(Guid DeviceId, DeviceCommandDto Command)> Calls { get; } = [];

        public Task<DeviceCommandDto> DispatchAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            var command = CreateCommand(request);
            DispatchCalls.Add((deviceId, request, command));
            Calls.Add((deviceId, command));

            return Task.FromResult(command);
        }

        public Task<DeviceCommandDto> EnqueueAsync(
            Guid deviceId,
            CreateDeviceCommandRequest request,
            CancellationToken cancellationToken)
        {
            if (requireGameplayChargeBeforeDispatch)
            {
                Assert.Contains(dbContext.LedgerEntries, entry =>
                    entry.EntryType == LedgerEntryTypeNames.GameplayCharge &&
                    entry.AccountType == LedgerAccountTypeNames.Wallet &&
                    entry.AmountMinorUnits < 0);
            }

            var command = CreateCommand(request);
            Enqueued.Add((deviceId, request, command));

            return Task.FromResult(command);
        }

        public Task NotifyAsync(
            Guid deviceId,
            DeviceCommandDto command,
            CancellationToken cancellationToken)
        {
            Calls.Add((deviceId, command));

            return Task.CompletedTask;
        }

        public void Clear()
        {
            DispatchCalls.Clear();
            Enqueued.Clear();
            Calls.Clear();
        }

        private static DeviceCommandDto CreateCommand(CreateDeviceCommandRequest request)
        {
            return new DeviceCommandDto(
                CommandId: Guid.NewGuid(),
                Type: request.Type,
                CreatedAtUtc: Now,
                Payload: request.Payload);
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
