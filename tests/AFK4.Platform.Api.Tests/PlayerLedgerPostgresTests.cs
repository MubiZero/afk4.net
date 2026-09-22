using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Reservations;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Остаток после строки выписки — на настоящей базе.
///
/// Снятие удержания и удержание за неявку пишутся одним моментом, и порядок таких строк решает
/// идентификатор. У Postgres и у .NET порядок идентификаторов разный, поэтому InMemory здесь
/// ничего не доказывает: остаток мог бы сойтись там и разойтись со строками в проде. Здесь же —
/// точность курсора: база хранит микросекунды.
/// </summary>
public sealed class PlayerLedgerPostgresTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-12T12:00:00Z");

    [PlatformAdminPostgresFact]
    public async Task Balance_AddsUpRowByRow_WhenRowsShareAMoment_PagedOneByOne()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"player_ledger_{Guid.NewGuid():N}";
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
            var playerId = Guid.NewGuid();
            await using (var seed = new PlatformDbContext(options))
            {
                // Момент с микросекундами, как у настоящих записей; удержания — в одной миллисекунде
                // с шагом в микросекунду. Курсор, обрезанный до миллисекунды, терял бы их.
                var moment = Now.AddHours(-1).AddTicks(1_234_560);
                seed.LedgerEntries.Add(Entry(organizationId, branchId, playerId, LedgerEntryTypeNames.TopUp, 10_000, Now.AddHours(-3)));
                for (var index = 0; index < 4; index++)
                {
                    var reservationId = Guid.NewGuid();
                    var hold = Entry(organizationId, branchId, playerId, LedgerEntryTypeNames.ReservationHold, -500, Now.AddHours(-2).AddTicks(4_560 + index * 10),
                        ReservationHold.Reason(reservationId));
                    seed.LedgerEntries.Add(hold);
                    seed.LedgerEntries.Add(Entry(organizationId, branchId, playerId, LedgerEntryTypeNames.Reversal, 500, moment,
                        ReservationHold.ReleaseReason(reservationId, ReservationHoldCauses.NoShow), reverses: hold.LedgerEntryId));
                    seed.LedgerEntries.Add(Entry(organizationId, branchId, playerId, LedgerEntryTypeNames.ReservationNoShowFee, -500, moment,
                        ReservationHold.NoShowFeeReason(reservationId)));
                }

                await seed.SaveChangesAsync();
            }

            var rows = new List<PlayerLedgerEntryDto>();
            string? cursor = null;
            do
            {
                await using var db = new PlatformDbContext(options);
                var page = await PlayerLedgerProjector.GetPlayerLedgerPageAsync(db, playerId, cursor, 1, CancellationToken.None);
                rows.AddRange(page.Items);
                cursor = page.NextCursor;
            }
            while (cursor is not null && rows.Count < 100);

            Assert.Equal(13, rows.Count);
            Assert.Equal(13, rows.Select(row => row.LedgerEntryId).Distinct().Count());
            Assert.Equal(8_000, rows[0].WalletBalanceAfter!.MinorUnits);
            for (var index = 0; index < rows.Count - 1; index++)
            {
                Assert.Equal(
                    rows[index].WalletBalanceAfter!.MinorUnits - rows[index].Amount.MinorUnits,
                    rows[index + 1].WalletBalanceAfter!.MinorUnits);
            }

            Assert.Equal(10_000, rows[^1].WalletBalanceAfter!.MinorUnits);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static LedgerEntryEntity Entry(
        Guid organizationId,
        Guid branchId,
        Guid playerId,
        string entryType,
        long amountMinorUnits,
        DateTimeOffset createdAtUtc,
        string reason = "test seed",
        Guid? reverses = null) => new()
    {
        LedgerEntryId = Guid.NewGuid(),
        OrganizationId = organizationId,
        BranchId = branchId,
        PlayerAccountId = playerId,
        EntryType = entryType,
        AccountType = LedgerAccountTypeNames.Wallet,
        AmountMinorUnits = amountMinorUnits,
        CurrencyCode = "TJS",
        Description = entryType,
        Reason = reason,
        ReversesLedgerEntryId = reverses,
        CreatedByStaffUserId = Guid.Empty,
        CreatedAtUtc = createdAtUtc
    };
}
