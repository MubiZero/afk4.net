using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Pos;

public sealed class PostgresPosSettlementConcurrencyTests
{
    [Fact]
    public async Task Fixture_NonTestDatabase_IsRejectedBeforeConnection()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PosSettlementPostgresFixture.CreateAsync(
                "Host=127.0.0.1;Port=1;Database=afk4_production;Username=postgres;Timeout=1"));

        Assert.Contains("ending in _test", exception.Message, StringComparison.Ordinal);
    }

    [PostgresPosFact]
    public async Task ConcurrentSettlement_CreatesOnePaidSaleAndOneEffectSet()
    {
        await using var database = await CreateDatabaseAsync();
        await database.SeedDraftMixedSaleAsync();
        database.ArmOverlap();

        var results = await Task.WhenAll(
            database.SettleInIndependentScopeAsync("concurrent-settlement-a"),
            database.SettleInIndependentScopeAsync("concurrent-settlement-b"));

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded);

        await using var db = database.CreateDbContext();
        var sale = await db.PosSales.AsNoTracking().SingleAsync();
        Assert.Equal(PosSaleStateNames.Paid, sale.State);
        var paymentParts = await db.Payments.AsNoTracking()
            .Where(payment => payment.PaymentKind == "payment")
            .OrderBy(payment => payment.PaymentMethod)
            .ToListAsync();
        Assert.Equal(
            [(PaymentMethodNames.Cash, 6_000L), (PaymentMethodNames.Wallet, 4_000L)],
            paymentParts.Select(payment => (payment.PaymentMethod, payment.AmountMinorUnits)).ToList());
        Assert.Equal(1, await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.WalletPayment));
        Assert.Equal(1, await db.StockMovements.CountAsync(movement => movement.MovementType == StockMovementTypeNames.Sale));
        Assert.Equal(1, await db.Receipts.CountAsync(receipt => receipt.ReceiptType == "sale"));
        Assert.Equal(1, await db.BillingCommandIdempotency.CountAsync());
    }

    [PostgresPosFact]
    public async Task ConcurrentMixedRefund_CreatesOneReversalSet()
    {
        await using var database = await CreateDatabaseAsync();
        await database.SeedDraftMixedSaleAsync();
        Assert.True((await database.SettleInIndependentScopeAsync("refund-seed-settlement")).Succeeded);
        database.ArmOverlap();

        var results = await Task.WhenAll(
            database.RefundInIndependentScopeAsync("concurrent-refund-a"),
            database.RefundInIndependentScopeAsync("concurrent-refund-b"));

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => !result.Succeeded);

        await using var db = database.CreateDbContext();
        var sale = await db.PosSales.AsNoTracking().SingleAsync();
        Assert.Equal(PosSaleStateNames.Refunded, sale.State);
        var refundParts = await db.Payments.AsNoTracking()
            .Where(payment => payment.PaymentKind == "refund")
            .OrderBy(payment => payment.PaymentMethod)
            .ToListAsync();
        Assert.Equal(
            [(PaymentMethodNames.Cash, -6_000L), (PaymentMethodNames.Wallet, -4_000L)],
            refundParts.Select(payment => (payment.PaymentMethod, payment.AmountMinorUnits)).ToList());
        Assert.Equal(1, await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.Reversal));
        Assert.Equal(1, await db.StockMovements.CountAsync(movement => movement.MovementType == StockMovementTypeNames.Refund));
        Assert.Equal(1, await db.Receipts.CountAsync(receipt => receipt.ReceiptType == "refund"));
        Assert.Equal(2, await db.BillingCommandIdempotency.CountAsync());
    }

    [PostgresPosFact]
    public async Task ReceiptConstraintFailure_RollsBackEverySettlementEffect()
    {
        await using var database = await CreateDatabaseAsync();
        await database.SeedDraftMixedSaleAsync(withReceiptNumberCollision: true);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SettleInIndependentScopeAsync("rollback-settlement"));

        await using var db = database.CreateDbContext();
        var sale = await db.PosSales.AsNoTracking().SingleAsync();
        Assert.Equal(PosSaleStateNames.Draft, sale.State);
        Assert.Empty(await db.Payments.AsNoTracking().ToListAsync());
        Assert.Empty(await db.LedgerEntries.AsNoTracking()
            .Where(entry => entry.EntryType == LedgerEntryTypeNames.WalletPayment)
            .ToListAsync());
        Assert.Empty(await db.StockMovements.AsNoTracking()
            .Where(movement => movement.MovementType == StockMovementTypeNames.Sale)
            .ToListAsync());
        Assert.Equal(1, await db.Receipts.CountAsync());
        Assert.Empty(await db.BillingCommandIdempotency.AsNoTracking().ToListAsync());
    }

    [PostgresPosFact]
    public async Task PaymentLedgerLink_MigrationEnforcesForeignKeyAndUniqueness()
    {
        await using var database = await CreateDatabaseAsync();
        await database.SeedDraftMixedSaleAsync();

        Assert.Contains(
            "20260714120000_LinkPaymentsToLedgerEntries",
            await database.GetAppliedMigrationsAsync());

        var foreignKey = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.InsertPaymentWithLedgerLinkAsync(Guid.NewGuid()));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, FindPostgresException(foreignKey).SqlState);

        var ledgerEntryId = await database.InsertStandaloneLedgerEntryAsync();
        await database.InsertPaymentWithLedgerLinkAsync(ledgerEntryId);
        var unique = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.InsertPaymentWithLedgerLinkAsync(ledgerEntryId));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, FindPostgresException(unique).SqlState);
    }

    private static Task<PosSettlementPostgresFixture> CreateDatabaseAsync() =>
        PosSettlementPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresPosFactAttribute.EnvironmentVariable)!);

    private static PostgresException FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        throw new Xunit.Sdk.XunitException($"Expected a PostgreSQL exception, got {exception.GetType().Name}.");
    }
}
