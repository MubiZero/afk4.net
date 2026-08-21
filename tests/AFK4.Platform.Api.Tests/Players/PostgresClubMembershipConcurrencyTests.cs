using System.Data.Common;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Tests.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AFK4.Platform.Api.Tests.Players;

/// <summary>
/// Два одновременных первых действия одного человека в одном клубе.
///
/// Порознь это классическая гонка: обе стороны читают «счёта нет», обе его создают, и у человека
/// в клубе оказывается два кошелька — деньги в одном, бронь в другом. In-memory провайдер такого
/// не покажет: уникальные индексы он не исполняет, поэтому проверять это можно только на
/// настоящей PostgreSQL.
///
/// Барьер ниже заставляет обе стороны прочитать таблицу счетов ДО того, как любая из них вставит
/// строку, — то самое переплетение, ради которого тест и написан.
/// </summary>
public sealed class PostgresClubMembershipConcurrencyTests
{
    [PostgresSessionFact]
    public async Task TwoFirstActionsAtOnce_LeaveExactlyOneAccount()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();

        var platformPersonId = Guid.NewGuid();
        await using (var seed = database.CreateDbContext())
        {
            seed.PlatformPersons.Add(new PlatformPersonEntity
            {
                PlatformPersonId = platformPersonId,
                PhoneNumber = "+992900000601",
                DisplayName = "Фаррух",
                PhoneVerifiedAtUtc = database.Now,
                IsActive = true,
                CreatedAtUtc = database.Now,
                UpdatedAtUtc = database.Now
            });
            await seed.SaveChangesAsync();
        }

        using var barrier = new Barrier(2);
        await using var firstDb = database.CreateDbContext(new AccountReadBarrier(barrier));
        await using var secondDb = database.CreateDbContext(new AccountReadBarrier(barrier));

        var results = await Task.WhenAll(
            EnsureAsync(firstDb, database, platformPersonId),
            EnsureAsync(secondDb, database, platformPersonId)).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Error));
        Assert.Equal(
            results[0].Account!.PlayerAccountId,
            results[1].Account!.PlayerAccountId);

        await using var verify = database.CreateDbContext();
        Assert.Equal(1, await verify.PlayerAccounts.CountAsync(
            account => account.PlatformPersonId == platformPersonId));
        // Счёт открылся нулём в обоих случаях: гонка не оставила после себя движения денег.
        Assert.False(await verify.LedgerEntries.AnyAsync(
            entry => entry.PlayerAccountId == results[0].Account!.PlayerAccountId));
    }

    private static Task<PlayerClubMembershipResult> EnsureAsync(
        PlatformDbContext db,
        SessionStartPostgresFixture database,
        Guid platformPersonId) =>
        Task.Run(() => new EfPlayerClubMembershipService(db, new FixedTimeProvider(database.Now))
            .EnsureAsync(platformPersonId, database.OrganizationId, database.BranchId, CancellationToken.None));

    /// <summary>
    /// Отпускает обе стороны только после того, как каждая прочитала таблицу счетов: до вставки,
    /// но уже внутри своей работы.
    /// </summary>
    private sealed class AccountReadBarrier(Barrier barrier) : DbCommandInterceptor
    {
        private bool tripped;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!tripped && command.CommandText.Contains("player_accounts", StringComparison.OrdinalIgnoreCase))
            {
                tripped = true;
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("Вторая сторона не дошла до чтения счетов.");
                }
            }

            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
