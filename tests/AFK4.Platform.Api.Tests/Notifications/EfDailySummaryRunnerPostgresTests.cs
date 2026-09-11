using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Reports;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>
/// The sibling InMemory tests prove the window's arithmetic but not that PostgreSQL accepts it:
/// InMemory compares <see cref="DateTimeOffset"/> values in .NET, while Npgsql refuses to write a
/// non-zero offset to <c>timestamp with time zone</c>. A business day resolved in Asia/Dushanbe
/// produces exactly such an offset (+05:00), so only a real Postgres run catches it — in staging it
/// failed 78 consecutive times while every test stayed green.
/// </summary>
public sealed class EfDailySummaryRunnerPostgresTests
{
    private static readonly Guid OrgId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid BranchId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T06:00:00Z");
    private static readonly DateTimeOffset Yesterday = DateTimeOffset.Parse("2026-06-01T12:00:00Z");

    [PlatformAdminPostgresFact]
    public async Task RunAsync_AgainstPostgres_EnqueuesSummaryForTheLocalBusinessDay()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"daily_summary_window_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(connectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scoped.ConnectionString)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            await using var db = new PlatformDbContext(options);
            await SeedOrgWithOwnerAsync(db);
            db.PosSales.Add(PaidSale(5000, Yesterday));
            await db.SaveChangesAsync();

            var recorder = new RecordingNotificationService();
            var runner = new EfDailySummaryRunner(
                db, new EfOrganizationOwnerResolver(db), recorder, Options.Create(new BusinessDayOptions()));

            var count = await runner.RunAsync(Now, CancellationToken.None);

            Assert.Equal(1, count);
            var request = Assert.Single(recorder.Requests);
            Assert.Equal("owner@club.example", request.Recipient.EmailAddress);
            Assert.Equal("2026-06-01", request.Tokens["date"]);
            Assert.Equal("50.00", request.Tokens["revenue"]);
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task SeedOrgWithOwnerAsync(PlatformDbContext db)
    {
        var ownerId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = OrgId, Slug = "club", Name = "Demo Club", Status = "active",
            PlanCode = "starter", SubscriptionStatus = "active", LimitsJson = "{}",
            CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId, OrganizationId = OrgId, Name = "Central Branch", CreatedAtUtc = Now
        });
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ownerId, OrganizationId = OrgId, UserName = "owner", NormalizedUserName = "OWNER",
            DisplayName = "Club Owner", Email = "owner@club.example", PasswordHash = "x",
            IsActive = true, CreatedAtUtc = Now
        });
        db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
        {
            StaffRoleAssignmentId = Guid.NewGuid(), StaffUserId = ownerId,
            OrganizationId = OrgId, BranchId = BranchId, RoleName = OrganizationRoleNames.OrganizationOwner
        });
        await db.SaveChangesAsync();
    }

    private static PosSaleEntity PaidSale(long totalMinorUnits, DateTimeOffset paidAtUtc) => new()
    {
        PosSaleId = Guid.NewGuid(), OrganizationId = OrgId, BranchId = BranchId, ShiftId = Guid.NewGuid(),
        CreatedByStaffUserId = Guid.NewGuid(), State = PosSaleStateNames.Paid, CurrencyCode = "TJS",
        TotalMinorUnits = totalMinorUnits, CreatedAtUtc = paidAtUtc, PaidAtUtc = paidAtUtc
    };
}
