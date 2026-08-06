using AFK4.Platform.Api.Configuration;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessTicketTests
{
    [Fact]
    public async Task RedeemTicket_TwiceSequentially_SecondCallFailsBecauseFirstAlreadyUsedIt()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var organizationId = await SeedOrganizationAsync(db);
        var issue = await service.IssueAsync(
            Guid.NewGuid(),
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Смена не открывается у клуба", 30),
            CancellationToken.None);

        Assert.NotNull(issue);

        var first = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);
        var second = await service.RedeemTicketAsync(issue.Ticket, CancellationToken.None);

        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!.SessionToken));
        Assert.Null(second);
    }

    // The organization-admin shell is built around branches — support seeing an organization without
    // its branch list can't render the shell at all. Also proves scoping: a branch belonging to a
    // different organization must never leak into this grant's session.
    [Fact]
    public async Task RedeemTicket_ReturnsOnlyTheGrantOrganizationsBranches()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var organizationId = await SeedOrganizationAsync(db);
        var otherOrganizationId = await SeedOrganizationAsync(db);
        var now = DateTimeOffset.UtcNow;
        var branchOne = new BranchEntity
        {
            BranchId = Guid.NewGuid(), OrganizationId = organizationId, Slug = "branch-one",
            Name = "Филиал на Рудаки", City = "Душанбе", CreatedAtUtc = now
        };
        var branchTwo = new BranchEntity
        {
            BranchId = Guid.NewGuid(), OrganizationId = organizationId, Slug = "branch-two",
            Name = "Филиал на Айни", City = "Душанбе", CreatedAtUtc = now
        };
        var otherOrgBranch = new BranchEntity
        {
            BranchId = Guid.NewGuid(), OrganizationId = otherOrganizationId, Slug = "other-branch",
            Name = "Чужой филиал", City = "Худжанд", CreatedAtUtc = now
        };
        db.Branches.AddRange(branchOne, branchTwo, otherOrgBranch);
        await db.SaveChangesAsync();

        var issue = await service.IssueAsync(
            Guid.NewGuid(),
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Смена не открывается у клуба", 30),
            CancellationToken.None);
        var session = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal(
            new[] { branchTwo.BranchId, branchOne.BranchId }.OrderBy(id => id),
            session!.Branches.Select(branch => branch.BranchId).OrderBy(id => id));
        Assert.DoesNotContain(session.Branches, branch => branch.BranchId == otherOrgBranch.BranchId);
    }

    [Fact]
    public async Task AuthenticateSession_AfterRevocation_Fails()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var adminId = Guid.NewGuid();
        var organizationId = await SeedOrganizationAsync(db);
        var issue = await service.IssueAsync(
            adminId,
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Устройство не видно в списке", 30),
            CancellationToken.None);
        var session = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);

        var before = await service.AuthenticateSessionAsync(
            session!.SessionToken, "organization.branch_settings.manage", CancellationToken.None);
        await service.RevokeAsync(issue.Grant.GrantId, adminId, CancellationToken.None);
        var after = await service.AuthenticateSessionAsync(
            session.SessionToken, "organization.branch_settings.manage", CancellationToken.None);

        Assert.NotNull(before);
        Assert.Null(after);
    }

    // Regression for a code-review finding: RedeemTicketAsync used to be read-then-write (read the
    // grant with TicketUsedAtUtc == null, then a separate SaveChangesAsync marks it used). Two
    // concurrent redemptions of the SAME ticket could both pass the read before either commits, and
    // both would get back a working session — the "strictly one-time ticket" requirement broken under
    // concurrency, with unique indexes on the hashes powerless to stop it (they don't collide: each
    // redemption mints its own random session token). The InMemory provider every other test in this
    // file uses has no transactions and no snapshot isolation, so it cannot exercise this race at all —
    // it needs a real PostgreSQL instance with a deterministic overlap (a SaveChangesInterceptor gate,
    // same pattern as PlatformAdminDirectoryServiceTests.ConcurrentUpdate_...), so both transactions are
    // guaranteed to have read the ticket as unused before either is allowed to flush its write.
    [PlatformAdminPostgresFact]
    public async Task RedeemTicket_TwoConcurrentRedemptions_ExactlyOneSucceeds()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"platform_support_ticket_race_{Guid.NewGuid():N}";
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

            var now = DateTimeOffset.Parse("2026-08-06T08:00:00Z");
            var supportAccessOptions = Options.Create(
                new SupportAccessOptions { OrganizationAdminBaseUrl = "https://admin.test" });

            Guid organizationId;
            await using (var seedDb = new PlatformDbContext(options))
            {
                organizationId = Guid.NewGuid();
                seedDb.Organizations.Add(new OrganizationEntity
                {
                    OrganizationId = organizationId,
                    Slug = $"club-{organizationId:N}",
                    Name = "Тестовый клуб",
                    Status = "active",
                    PlanCode = "starter",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
                await seedDb.SaveChangesAsync();
            }

            string ticket;
            await using (var issueDb = new PlatformDbContext(options))
            {
                var issueService = new PlatformSupportAccessGrantService(
                    issueDb, new FixedTimeProvider(now), supportAccessOptions);
                var issue = await issueService.IssueAsync(
                    Guid.NewGuid(),
                    new CreatePlatformSupportAccessGrantRequest(organizationId, "Гонка на обмен билета поддержки", 30),
                    CancellationToken.None);
                Assert.NotNull(issue);
                ticket = issue!.Ticket;
            }

            // Two independent DbContexts (independent connections/transactions) redeeming the SAME
            // ticket at once — mirrors two browser tabs racing to exchange one support-access link.
            gate.Arm();
            await using var dbForFirst = new PlatformDbContext(options);
            await using var dbForSecond = new PlatformDbContext(options);
            var serviceForFirst = new PlatformSupportAccessGrantService(
                dbForFirst, new FixedTimeProvider(now), supportAccessOptions);
            var serviceForSecond = new PlatformSupportAccessGrantService(
                dbForSecond, new FixedTimeProvider(now), supportAccessOptions);

            var results = await Task.WhenAll(
                serviceForFirst.RedeemTicketAsync(ticket, CancellationToken.None),
                serviceForSecond.RedeemTicketAsync(ticket, CancellationToken.None))
                .WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Single(results, result => result is not null);
            Assert.Single(results, result => result is null);

            await using var verifyDb = new PlatformDbContext(options);
            var grant = await verifyDb.PlatformSupportAccessGrants.AsNoTracking().SingleAsync();
            Assert.NotNull(grant.SessionTokenHash);
            Assert.NotNull(grant.TicketUsedAtUtc);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> SeedOrganizationAsync(PlatformDbContext db)
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = $"club-{organizationId:N}",
            Name = "Тестовый клуб",
            Status = "active",
            PlanCode = "starter",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return organizationId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Blocks each SavingChangesAsync call until exactly two concurrent saves have arrived, then
    // releases both together — forces both transactions to have already read the ticket as unused
    // before either is allowed to flush its "mark used" write, instead of hoping Task.WhenAll happens
    // to interleave two real network round trips unluckily. Same pattern as
    // PlatformAdminDirectoryServiceTests.SaveOverlapGate.
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
