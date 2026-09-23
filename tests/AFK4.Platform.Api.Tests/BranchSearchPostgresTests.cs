using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Search;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Поиск заказов — на настоящей базе.
///
/// Заказ ищется без учёта регистра, а имя гостя обычно кириллицей: «карим» за стойкой набирают
/// строчными. InMemory сравнивает строки средствами .NET и совпал бы в любом случае; в проде
/// регистр сводит сама база, и проверить это можно только на ней. Здесь же — порядок: заказы в
/// работе раньше закрытых, закрытые от свежих к старым, и соединение с чеком не размножает строки
/// у отменённого заказа, у которого чеков два.
/// </summary>
public sealed class BranchSearchPostgresTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-23T12:00:00Z");

    [PlatformAdminPostgresFact]
    public async Task Orders_AreFoundCaseInsensitively_OpenFirst_ThenClosedNewestFirst()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"branch_search_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(connectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema }.ConnectionString)
                .Options;
            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            var organizationId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var otherBranchId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var seatId = Guid.NewGuid();
            var openOrderId = Guid.NewGuid();
            var cancelledOrderId = Guid.NewGuid();
            var deliveredOrderId = Guid.NewGuid();
            await using (var seed = new PlatformDbContext(options))
            {
                seed.PlayerAccounts.Add(new PlayerAccountEntity
                {
                    PlayerAccountId = playerId,
                    OrganizationId = organizationId,
                    HomeBranchId = branchId,
                    DisplayName = "Карим Рахимов",
                    IsActive = true,
                    CreatedAtUtc = Now.AddDays(-30)
                });
                seed.Seats.Add(new SeatEntity
                {
                    SeatId = seatId,
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    ZoneId = Guid.NewGuid(),
                    Name = "PC-12",
                    SortOrder = 1,
                    CreatedAtUtc = Now.AddDays(-30)
                });
                AddOrder(seed, organizationId, branchId, playerId, seatId, deliveredOrderId, ShopOrderStatusNames.Delivered, Now.AddDays(-3), "POS-20260920-0001");
                AddOrder(seed, organizationId, branchId, playerId, seatId, cancelledOrderId, ShopOrderStatusNames.Cancelled, Now.AddDays(-1), "POS-20260922-0004",
                    refundNumber: "REF-20260922-0001");
                AddOrder(seed, organizationId, branchId, playerId, seatId, openOrderId, ShopOrderStatusNames.Placed, Now.AddMinutes(-5), "POS-20260923-0042");
                AddOrder(seed, organizationId, otherBranchId, playerId, Guid.NewGuid(), Guid.NewGuid(), ShopOrderStatusNames.Placed, Now.AddMinutes(-1), "POS-20260923-0043");
                await seed.SaveChangesAsync();
            }

            var ordersOnly = new BranchSearchScope(Seats: false, Players: false, Reservations: false, Receipts: false, Orders: true);
            await using var db = new PlatformDbContext(options);
            var service = new EfBranchSearchService(db, TimeProvider.System);

            var byGuest = await service.SearchAsync(organizationId, branchId, "карим", ordersOnly, 5, CancellationToken.None);
            var byNumber = await service.SearchAsync(organizationId, branchId, "pos-20260923", ordersOnly, 5, CancellationToken.None);
            var bySeat = await service.SearchAsync(organizationId, branchId, "pc-12", ordersOnly, 5, CancellationToken.None);

            Assert.Equal([openOrderId, cancelledOrderId, deliveredOrderId], byGuest.Select(result => result.Id));
            Assert.All(byGuest, result => Assert.Equal(BranchSearchKindNames.Order, result.Kind));
            Assert.Equal("POS-20260922-0004", byGuest[1].Number);
            var numbered = Assert.Single(byNumber);
            Assert.Equal(openOrderId, numbered.Id);
            Assert.Equal("PC-12", numbered.Title);
            Assert.Equal("Карим Рахимов", numbered.Subtitle);
            Assert.Equal(3, bySeat.Count);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static void AddOrder(
        PlatformDbContext db,
        Guid organizationId,
        Guid branchId,
        Guid playerId,
        Guid seatId,
        Guid orderId,
        string status,
        DateTimeOffset placedAtUtc,
        string receiptNumber,
        string? refundNumber = null)
    {
        var saleId = Guid.NewGuid();
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = organizationId,
            BranchId = branchId,
            ShiftId = Guid.NewGuid(),
            PlayerAccountId = playerId,
            State = refundNumber is null ? "paid" : "refunded",
            CurrencyCode = "TJS",
            TotalMinorUnits = 3200,
            CreatedAtUtc = placedAtUtc,
            PaidAtUtc = placedAtUtc
        });
        db.Receipts.Add(Receipt(organizationId, branchId, saleId, receiptNumber, "sale", placedAtUtc));
        if (refundNumber is not null)
        {
            db.Receipts.Add(Receipt(organizationId, branchId, saleId, refundNumber, "refund", placedAtUtc.AddMinutes(3)));
        }

        db.ShopOrders.Add(new ShopOrderEntity
        {
            ShopOrderId = orderId,
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = playerId,
            SessionId = Guid.NewGuid(),
            SeatId = seatId,
            Status = status,
            TotalMinorUnits = 3200,
            CurrencyCode = "TJS",
            WalletLedgerEntryId = Guid.NewGuid(),
            PosSaleId = saleId,
            PlacedAtUtc = placedAtUtc,
            Version = 1
        });
    }

    private static ReceiptEntity Receipt(
        Guid organizationId,
        Guid branchId,
        Guid saleId,
        string number,
        string type,
        DateTimeOffset createdAtUtc) => new()
    {
        ReceiptId = Guid.NewGuid(),
        OrganizationId = organizationId,
        BranchId = branchId,
        PosSaleId = saleId,
        ReceiptNumber = number,
        ReceiptType = type,
        CurrencyCode = "TJS",
        TotalMinorUnits = 3200,
        CreatedAtUtc = createdAtUtc
    };
}
