using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformIncidentServiceTests
{
    [Fact]
    public async Task SecondDetection_DoesNotOpenSecondIncident()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var first = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{\"minutes\":200}", CancellationToken.None);
        var second = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{\"minutes\":260}", CancellationToken.None);

        Assert.True(first.IsNew);
        Assert.False(second.IsNew);
        Assert.Equal(1, await db.PlatformIncidents.CountAsync(incident => incident.ResolvedAtUtc == null));
    }

    [Fact]
    public async Task ResolveMissing_ClosesIncidentsNoLongerDetected()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:billing_outbox",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        var resolved = await service.ResolveMissingAsync(
            [PlatformIncidentKindNames.JobFailing], ["job_failing:daily_summary"], CancellationToken.None);

        Assert.Equal("job_failing:billing_outbox", Assert.Single(resolved).DedupKey);
        var open = await service.ListOpenAsync(CancellationToken.None);
        Assert.Equal("job_failing:daily_summary", Assert.Single(open).DedupKey);
    }

    // Regression for a review finding: ResolveMissingAsync used to sweep every open incident
    // regardless of kind, so a caller that only evaluated one kind this pass would silently close
    // incidents of a kind it never checked. The health watchdog (next task) is expected to pass every
    // kind it evaluated each run, but that invariant has to be enforced here, not just documented.
    [Fact]
    public async Task ResolveMissing_LeavesUnevaluatedKindsOpenEvenWhenTheirKeyIsMissing()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:billing_outbox",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        var resolved = await service.ResolveMissingAsync(
            [PlatformIncidentKindNames.JobFailing], [], CancellationToken.None);

        Assert.Equal("job_failing:billing_outbox", Assert.Single(resolved).DedupKey);
        var open = await service.ListOpenAsync(CancellationToken.None);
        Assert.Equal("job_overdue:daily_summary", Assert.Single(open).DedupKey);
    }

    [Fact]
    public async Task ReopeningAfterResolve_CreatesNewIncident()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:auto_protection",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.ResolveMissingAsync([PlatformIncidentKindNames.JobOverdue], [], CancellationToken.None);
        var reopened = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:auto_protection",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        Assert.True(reopened.IsNew);
    }

    // Regression for a review finding: OpenOrTouchAsync's race-recovery catch used to key off
    // `EntityState.Added` alone, which is true after ANY failed SaveChangesAsync on the freshly-added
    // row — a dropped connection, a deadlock, a DetailsJson value over its 1000-char column limit.
    // Such an unrelated failure must propagate, not get reinterpreted as "another writer already won
    // the dedup race" (which would then mask the real error behind a confusing SingleAsync failure).
    // The Postgres concurrency test can't distinguish a correct catch from this bug — its only
    // possible SaveChangesAsync failure IS the dedup-index collision — so this needs its own,
    // deterministic simulation of an unrelated unique-constraint violation on the same table.
    [Fact]
    public async Task OpenOrTouch_UnrelatedSaveFailure_PropagatesInsteadOfBeingTreatedAsDedupRace()
    {
        var interceptor = new AlwaysCollidingOnUnrelatedConstraintInterceptor();
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
            .Options);
        var service = new EfPlatformIncidentService(db, TimeProvider.System);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None));
    }

    /// <summary>Throws a DbUpdateException wrapping a unique-violation PostgresException on an
    /// unrelated constraint for every SaveChangesAsync call — stand-in for "the failure is a real
    /// unique-constraint collision, just not the DedupKey index this service knows how to recover
    /// from". Modeled on EfInvoiceServiceTests.AlwaysCollidingOnNumberInterceptor.</summary>
    private sealed class AlwaysCollidingOnUnrelatedConstraintInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<PlatformIncidentEntity>()
                    .Any(entry => entry.State == EntityState.Added) == true)
            {
                throw new DbUpdateException("simulated unrelated collision", new PostgresException(
                    messageText: "duplicate key value violates unique constraint \"IX_platform_incidents_SomeOtherColumn\"",
                    severity: "ERROR",
                    invariantSeverity: "ERROR",
                    sqlState: PostgresErrorCodes.UniqueViolation,
                    detail: null,
                    hint: null,
                    position: 0,
                    internalPosition: 0,
                    internalQuery: null,
                    where: null,
                    schemaName: null,
                    tableName: "platform_incidents",
                    columnName: null,
                    dataTypeName: null,
                    constraintName: "IX_platform_incidents_SomeOtherColumn",
                    file: null,
                    line: null,
                    routine: null));
            }

            return new ValueTask<InterceptionResult<int>>(result);
        }
    }
}
