using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchSnapshotRunnerConcurrencyPostgresTests
{
    // InMemory has no unique index at all, so the race this proves — two job ticks overlapping and
    // both computing "yesterday is missing" before either has saved — would be false-green there.
    // Same schema-per-run isolation and SaveOverlapGate pattern as PlatformIncidentConcurrencyPostgresTests
    // / PlatformSupportAccessTicketTests.RedeemTicket_TwoConcurrentRedemptions_ExactlyOneSucceeds.
    [PlatformAdminPostgresFact]
    public async Task Run_ConcurrentRunsForSameBranchAndDay_WriteExactlyOneRow()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"branch_snapshot_race_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(rootBuilder.ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var gate = new SaveOverlapGate();
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scopedBuilder.ConnectionString)
                .AddInterceptors(gate)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            var now = DateTimeOffset.Parse("2026-08-08T03:00:00Z");
            var organizationId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            await using (var seedDb = new PlatformDbContext(options))
            {
                seedDb.Organizations.Add(new OrganizationEntity
                {
                    OrganizationId = organizationId,
                    Slug = "club-" + organizationId.ToString("N")[..8],
                    Name = "Club",
                    Status = OrganizationStatusNames.Active,
                    CreatedAtUtc = now.AddMonths(-3),
                    UpdatedAtUtc = now.AddMonths(-3)
                });
                seedDb.Branches.Add(new BranchEntity
                {
                    BranchId = branchId,
                    OrganizationId = organizationId,
                    Slug = "branch-" + branchId.ToString("N")[..8],
                    Name = "Branch",
                    CreatedAtUtc = now.AddMonths(-3)
                });
                await seedDb.SaveChangesAsync();
            }

            // Two independent DbContexts (independent connections/transactions), each computing "no
            // snapshot for this branch yet" from its own read, then racing to insert the same
            // (BranchId, SnapshotDate) row — mirrors two overlapping BranchSnapshotJob ticks.
            gate.Arm();
            await using var dbForFirst = new PlatformDbContext(options);
            await using var dbForSecond = new PlatformDbContext(options);
            var runnerForFirst = new EfBranchSnapshotRunner(dbForFirst, new FixedTimeProvider(now));
            var runnerForSecond = new EfBranchSnapshotRunner(dbForSecond, new FixedTimeProvider(now));

            var results = await Task.WhenAll(
                    RunSafelyAsync(runnerForFirst, now),
                    RunSafelyAsync(runnerForSecond, now))
                .WaitAsync(TimeSpan.FromSeconds(30));

            // One run's SaveChangesAsync commits; the other's collides on the unique index and throws
            // rather than writing a duplicate row.
            Assert.Single(results, result => result.Written == 1 && result.Failed is null);
            Assert.Single(results, result => result.Written == 0 && result.Failed is not null);

            await using var verifyDb = new PlatformDbContext(options);
            var rowCount = await verifyDb.BranchDailySnapshots
                .Where(snapshot => snapshot.BranchId == branchId)
                .CountAsync();
            Assert.Equal(1, rowCount);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<(int Written, Exception? Failed)> RunSafelyAsync(IBranchSnapshotRunner runner, DateTimeOffset now)
    {
        try
        {
            var written = await runner.RunAsync(now, CancellationToken.None);
            return (written, null);
        }
        catch (DbUpdateException exception)
        {
            return (0, exception);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Blocks each SavingChangesAsync call until exactly two concurrent saves have arrived, then
    // releases both together — forces both transactions to have already missed each other's insert
    // before either is allowed to flush, instead of hoping Task.WhenAll happens to interleave two
    // real network round trips unluckily. Same pattern as
    // PlatformIncidentConcurrencyPostgresTests.SaveOverlapGate.
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
}
