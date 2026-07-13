using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopCommercePostgresTests
{
    [Fact]
    public async Task Fixture_NonTestDatabase_IsRejectedBeforeConnection()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ShopCommercePostgresFixture.CreateAsync(
                "Host=127.0.0.1;Port=1;Database=afk4_production;Username=postgres;Timeout=1"));

        Assert.Contains("ending in _test", exception.Message, StringComparison.Ordinal);
    }

    [PostgresCommerceFact]
    public async Task ConcurrentPlacement_ForLastUnit_AllowsExactlyOnePaidOrder()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgresCommerceFactAttribute.EnvironmentVariable)!;
        await using var database = await ShopCommercePostgresFixture.CreateAsync(connectionString);
        await database.SeedLastUnitScenarioAsync(stock: 1, walletMinorUnits: 10_000);

        var results = await Task.WhenAll(
            database.PlaceInIndependentScopeAsync("last-unit-a"),
            database.PlaceInIndependentScopeAsync("last-unit-b"));

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == "out_of_stock");
        Assert.Equal(2, database.InitialSuccessfulSettlementCount);
        Assert.True(database.SerializationRetryCount >= 1);

        await using var verificationDb = database.CreateDbContext();
        var order = Assert.Single(await verificationDb.ShopOrders.AsNoTracking().ToListAsync());
        var sale = Assert.Single(await verificationDb.PosSales.AsNoTracking().ToListAsync());
        Assert.Equal(sale.PosSaleId, order.PosSaleId);
        Assert.Equal(ShopOrderStatusNames.Placed, order.Status);
        Assert.Equal(500, order.TotalMinorUnits);
        Assert.Equal("TJS", order.CurrencyCode);

        Assert.Equal(PosSaleStateNames.Paid, sale.State);
        Assert.Equal(500, sale.TotalMinorUnits);
        Assert.Equal("TJS", sale.CurrencyCode);
        Assert.Equal(database.PlayerAccountId, sale.PlayerAccountId);
        Assert.Equal(database.SessionId, sale.SessionId);

        var orderLine = Assert.Single(await verificationDb.ShopOrderLines.AsNoTracking().ToListAsync());
        var saleLine = Assert.Single(await verificationDb.PosSaleLines.AsNoTracking().ToListAsync());
        Assert.Equal(database.ProductId, orderLine.ProductId);
        Assert.Equal(1, orderLine.Quantity);
        Assert.Equal(500, orderLine.UnitPriceMinorUnits);
        Assert.Equal(500, orderLine.LineTotalMinorUnits);
        Assert.Equal(database.ProductId, saleLine.ProductId);
        Assert.Equal(1, saleLine.Quantity);
        Assert.Equal(500, saleLine.UnitPriceMinorUnits);
        Assert.Equal(100, saleLine.UnitCostMinorUnits);
        Assert.Equal(500, saleLine.LineTotalMinorUnits);
        Assert.Equal("TJS", saleLine.CurrencyCode);

        var payment = Assert.Single(await verificationDb.Payments.AsNoTracking().ToListAsync());
        Assert.Equal(sale.PosSaleId, payment.PosSaleId);
        Assert.Equal(sale.ShiftId, payment.ShiftId);
        Assert.Equal("payment", payment.PaymentKind);
        Assert.Equal("wallet", payment.Provider);
        Assert.Equal(PaymentMethodNames.Wallet, payment.PaymentMethod);
        Assert.Equal(500, payment.AmountMinorUnits);
        Assert.Equal("TJS", payment.CurrencyCode);

        var receipt = Assert.Single(await verificationDb.Receipts.AsNoTracking().ToListAsync());
        Assert.Equal(sale.PosSaleId, receipt.PosSaleId);
        Assert.Equal("sale", receipt.ReceiptType);
        Assert.Equal(500, receipt.TotalMinorUnits);
        Assert.Equal("TJS", receipt.CurrencyCode);

        var walletDebit = Assert.Single(
            await verificationDb.LedgerEntries.AsNoTracking()
                .Where(entry => entry.EntryType == LedgerEntryTypeNames.WalletPayment)
                .ToListAsync());
        Assert.Equal(order.WalletLedgerEntryId, walletDebit.LedgerEntryId);
        Assert.Equal(sale.ShiftId, walletDebit.ShiftId);
        Assert.Equal(database.PlayerAccountId, walletDebit.PlayerAccountId);
        Assert.Equal(LedgerAccountTypeNames.Wallet, walletDebit.AccountType);
        Assert.Equal(-500, walletDebit.AmountMinorUnits);
        Assert.Equal("TJS", walletDebit.CurrencyCode);
        Assert.Equal("shop_order", walletDebit.Description);
        Assert.StartsWith("shop-order-place:", walletDebit.Reason, StringComparison.Ordinal);
        Assert.Equal(payment.Note, walletDebit.Reason);

        var saleMovement = Assert.Single(
            await verificationDb.StockMovements.AsNoTracking()
                .Where(movement => movement.MovementType == StockMovementTypeNames.Sale)
                .ToListAsync());
        Assert.Equal(database.ProductId, saleMovement.ProductId);
        Assert.Equal(-1, saleMovement.QuantityDelta);
        Assert.Equal(100, saleMovement.UnitCostMinorUnits);
        Assert.Equal("TJS", saleMovement.CurrencyCode);
        Assert.Equal(walletDebit.Reason, saleMovement.Reason);

        Assert.Equal(2, await verificationDb.LedgerEntries.AsNoTracking().CountAsync());
        Assert.Equal(2, await verificationDb.StockMovements.AsNoTracking().CountAsync());
        Assert.Single(await verificationDb.BillingCommandIdempotency.AsNoTracking().ToListAsync());
    }
}
