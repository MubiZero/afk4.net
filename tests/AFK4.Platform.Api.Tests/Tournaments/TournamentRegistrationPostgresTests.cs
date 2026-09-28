using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Tests.Identity;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Platform.Api.Tournaments;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Tournaments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Tournaments;

public sealed class TournamentRegistrationPostgresTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    // «Есть ли место» — чтение перед записью. Несколько игроков, одновременно нажавших
    // «Записаться» на последнее место, раньше все проходили проверку, и турнир на одно место
    // принимал нескольких со списанным взносом. На InMemory такой гонки не бывает вовсе, поэтому
    // тест идёт только на настоящем Postgres: место получает один, остальные — «мест нет», и
    // взнос списан ровно один раз.
    [PlatformAdminPostgresFact]
    public async Task SimultaneousRegistrations_ForTheLastSeat_AdmitExactlyOne()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"tournament_race_{Guid.NewGuid():N}";
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

            Guid tournamentId;
            var players = new List<Guid>();
            await using (var seedDb = new PlatformDbContext(options))
            {
                seedDb.Branches.Add(new BranchEntity
                {
                    BranchId = Branch, OrganizationId = Org, Slug = "main", Name = "На Рудаки", City = "Душанбе", CreatedAtUtc = Now
                });
                for (var i = 0; i < 3; i++)
                {
                    var playerAccountId = Guid.NewGuid();
                    players.Add(playerAccountId);
                    seedDb.PlayerAccounts.Add(new PlayerAccountEntity
                    {
                        PlayerAccountId = playerAccountId, OrganizationId = Org, HomeBranchId = Branch, DisplayName = $"Игрок {i}",
                        PhoneNumber = TestPhones.Next(), PreferredLocale = "ru", IsActive = true, CreatedAtUtc = Now
                    });
                    seedDb.LedgerEntries.Add(BillingEntryFactory.Create(
                        Org, Branch, playerAccountId, null, null,
                        LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
                        10_000, 0, "TJS", "top up", "seed", null, Guid.NewGuid(), Now));
                }

                await seedDb.SaveChangesAsync();
                var organizer = new EfTournamentService(seedDb, new MovableTimeProvider(Now));
                var created = await organizer.CreateAsync(Org, Guid.NewGuid(), new CreateTournamentRequest(
                    Branch, "Ночь Counter-Strike", "Пять на пять", "Counter-Strike", Now.AddDays(3), 2000, 1), CancellationToken.None);
                tournamentId = created.Value!.TournamentId;
                await organizer.PublishAsync(Org, tournamentId, CancellationToken.None);
            }

            var contexts = players.Select(_ => new PlatformDbContext(options)).ToList();
            try
            {
                var results = await Task.WhenAll(players.Select((player, index) =>
                    new EfTournamentService(contexts[index], new MovableTimeProvider(Now))
                        .RegisterAsync(Org, player, tournamentId, CancellationToken.None)));

                Assert.Single(results, result => result.Succeeded);
                Assert.All(results.Where(result => !result.Succeeded),
                    result => Assert.Equal(TournamentRefusalCodes.Full, result.Error));
            }
            finally
            {
                foreach (var context in contexts) await context.DisposeAsync();
            }

            await using var verifyDb = new PlatformDbContext(options);
            Assert.Single(await verifyDb.TournamentRegistrations.ToListAsync());
            Assert.Single(await verifyDb.LedgerEntries
                .Where(entry => entry.EntryType == LedgerEntryTypeNames.TournamentEntryFee).ToListAsync());
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
