using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformIncidentConcurrencyPostgresTests
{
    // InMemory has no partial unique index at all, so the race this proves — two observers detecting
    // the same problem at once and racing to insert — would be false-green there. Same schema-per-run
    // isolation and SaveOverlapGate pattern as PlatformSupportAccessTicketTests.
    // RedeemTicket_TwoConcurrentRedemptions_ExactlyOneSucceeds.
    [PlatformAdminPostgresFact]
    public async Task OpenOrTouch_TwoConcurrentCallsSameDedupKey_ExactlyOneOpensNewIncident()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"platform_incident_race_{Guid.NewGuid():N}";
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

            var now = DateTimeOffset.Parse("2026-08-07T08:00:00Z");
            const string dedupKey = "job_overdue:daily_summary";

            // Two independent DbContexts (independent connections/transactions) detecting the SAME
            // incident at once — mirrors two periodic-job runs overlapping.
            gate.Arm();
            await using var dbForFirst = new PlatformDbContext(options);
            await using var dbForSecond = new PlatformDbContext(options);
            var serviceForFirst = new EfPlatformIncidentService(dbForFirst, new FixedTimeProvider(now));
            var serviceForSecond = new EfPlatformIncidentService(dbForSecond, new FixedTimeProvider(now));

            var results = await Task.WhenAll(
                serviceForFirst.OpenOrTouchAsync(
                    PlatformIncidentKindNames.JobOverdue, dedupKey, PlatformIncidentSeverityNames.Warning,
                    "{}", CancellationToken.None),
                serviceForSecond.OpenOrTouchAsync(
                    PlatformIncidentKindNames.JobOverdue, dedupKey, PlatformIncidentSeverityNames.Warning,
                    "{}", CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Single(results, result => result.IsNew);
            Assert.Single(results, result => !result.IsNew);

            await using var verifyDb = new PlatformDbContext(options);
            var openCount = await verifyDb.PlatformIncidents
                .Where(incident => incident.DedupKey == dedupKey && incident.ResolvedAtUtc == null)
                .CountAsync();
            Assert.Equal(1, openCount);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
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
    // PlatformSupportAccessTicketTests.SaveOverlapGate.
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
