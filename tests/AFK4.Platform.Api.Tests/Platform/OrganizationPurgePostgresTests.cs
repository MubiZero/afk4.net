using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Offboarding;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class OrganizationPurgePostgresTests
{
    // Порядок удаления полутора десятков таблиц держится на внешних ключах, а InMemory-провайдер
    // не проверяет их вовсе: там «стирание» пройдёт при любом порядке, включая заведомо неверный.
    // Значит проверить порядок можно ТОЛЬКО на настоящем Postgres — иначе зелёный тест означает
    // ровно ничего.
    [PlatformAdminPostgresFact]
    public async Task Purge_RunsAgainstRealForeignKeys_AndLeavesTheMoneyTrail()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var schema = $"purge_{Guid.NewGuid():N}";
        await using var root = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString).ConnectionString);
        await root.OpenAsync();
        await using (var create = root.CreateCommand())
        {
            create.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var scopedBuilder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<PlatformDbContext>()
                .UseNpgsql(scopedBuilder.ConnectionString)
                .Options;

            await using (var migrationDb = new PlatformDbContext(options))
            {
                await migrationDb.Database.MigrateAsync();
            }

            var now = DateTimeOffset.Parse("2026-08-10T08:00:00Z");
            var organizationId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var seatId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var staffUserId = Guid.NewGuid();
            var playerId = Guid.NewGuid();
            var saleId = Guid.NewGuid();

            await using (var seedDb = new PlatformDbContext(options))
            {
                seedDb.Organizations.Add(new OrganizationEntity
                {
                    OrganizationId = organizationId,
                    Slug = "leaving-club",
                    Name = "Уходящий клуб",
                    Status = OrganizationStatusNames.DeletionPending,
                    PlanCode = "starter",
                    SubscriptionStatus = SubscriptionStatusNames.Trial,
                    PurgeEligibleAtUtc = now.AddDays(-1),
                    CreatedAtUtc = now.AddYears(-1),
                    UpdatedAtUtc = now
                });
                seedDb.Branches.Add(new BranchEntity
                {
                    BranchId = branchId,
                    OrganizationId = organizationId,
                    Name = "Главный",
                    CreatedAtUtc = now
                });
                seedDb.Zones.Add(new ZoneEntity
                {
                    ZoneId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    Name = "Зал"
                });
                seedDb.Seats.Add(new SeatEntity
                {
                    SeatId = seatId,
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    ZoneId = Guid.NewGuid(),
                    Name = "PC-1",
                    CreatedAtUtc = now
                });
                seedDb.Devices.Add(new DeviceEntity
                {
                    DeviceId = deviceId,
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    MachineName = "PC-1",
                    DisplayName = "PC-1",
                    EnrolledAtUtc = now
                });
                seedDb.StaffUsers.Add(new StaffUserEntity
                {
                    StaffUserId = staffUserId,
                    OrganizationId = organizationId,
                    UserName = "operator@club.test",
                    NormalizedUserName = "OPERATOR@CLUB.TEST",
                    DisplayName = "Оператор",
                    IsActive = true,
                    CreatedAtUtc = now
                });
                seedDb.PlayerAccounts.Add(new PlayerAccountEntity
                {
                    PlayerAccountId = playerId,
                    OrganizationId = organizationId,
                    HomeBranchId = branchId,
                    DisplayName = "Игрок",
                    IsActive = true,
                    CreatedAtUtc = now
                });
                seedDb.Sessions.Add(new SessionEntity
                {
                    SessionId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    SeatId = seatId,
                    DeviceId = deviceId,
                    CreatedByStaffUserId = staffUserId,
                    PlayerAccountId = playerId,
                    State = "closed",
                    RequestedAtUtc = now.AddHours(-2),
                    StartedAtUtc = now.AddHours(-2),
                    EndedAtUtc = now.AddHours(-1)
                });
                seedDb.PosSales.Add(new PosSaleEntity
                {
                    PosSaleId = saleId,
                    OrganizationId = organizationId,
                    BranchId = branchId,
                    ShiftId = Guid.NewGuid(),
                    CreatedByStaffUserId = staffUserId,
                    State = "paid",
                    CurrencyCode = "TJS",
                    TotalMinorUnits = 1500,
                    CreatedAtUtc = now
                });
                seedDb.PosSaleLines.Add(new PosSaleLineEntity
                {
                    PosSaleLineId = Guid.NewGuid(),
                    PosSaleId = saleId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Кола",
                    CurrencyCode = "TJS",
                    Quantity = 1,
                    UnitPriceMinorUnits = 1500,
                    LineTotalMinorUnits = 1500
                });
                seedDb.Invoices.Add(new InvoiceEntity
                {
                    InvoiceId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    Number = 1,
                    PeriodStartUtc = now.AddMonths(-1),
                    PeriodEndUtc = now,
                    AmountMinorUnits = 100000,
                    CurrencyCode = "TJS",
                    Status = "paid",
                    IssuedAtUtc = now.AddMonths(-1),
                    DueAtUtc = now,
                    CreatedAtUtc = now.AddMonths(-1),
                    UpdatedAtUtc = now
                });
                seedDb.AuditRecords.Add(new AuditRecordEntity
                {
                    AuditRecordId = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    ActorPlatformAdminUserId = Guid.NewGuid(),
                    Action = "platform.organizations.status.update",
                    TargetType = "Organization",
                    Outcome = "Succeeded",
                    SourceApp = "platform",
                    CreatedAtUtc = now
                });
                await seedDb.SaveChangesAsync();
            }

            await using (var purgeDb = new PlatformDbContext(options))
            {
                var service = new OrganizationPurgeService(purgeDb, new FixedClock(now));
                var result = await service.PurgeAsync(organizationId, "leaving-club", CancellationToken.None);
                Assert.True(result.Succeeded, $"Стирание отказано: {result.ErrorCode}");
            }

            await using (var verifyDb = new PlatformDbContext(options))
            {
                Assert.False(await verifyDb.PlayerAccounts.AnyAsync(row => row.OrganizationId == organizationId));
                Assert.False(await verifyDb.StaffUsers.AnyAsync(row => row.OrganizationId == organizationId));
                Assert.False(await verifyDb.Sessions.AnyAsync(row => row.OrganizationId == organizationId));
                Assert.False(await verifyDb.PosSales.AnyAsync(row => row.OrganizationId == organizationId));
                Assert.False(await verifyDb.PosSaleLines.AnyAsync(line => line.PosSaleId == saleId));
                Assert.False(await verifyDb.Devices.AnyAsync(row => row.OrganizationId == organizationId));
                Assert.False(await verifyDb.Branches.AnyAsync(row => row.OrganizationId == organizationId));

                var organization = await verifyDb.Organizations.SingleAsync(row => row.OrganizationId == organizationId);
                Assert.Equal(OrganizationStatusNames.Purged, organization.Status);
                Assert.True(await verifyDb.Invoices.AnyAsync(row => row.OrganizationId == organizationId));
                Assert.True(await verifyDb.AuditRecords.AnyAsync(row => row.OrganizationId == organizationId));
            }

            await using (var repeatDb = new PlatformDbContext(options))
            {
                // Повторный вызов обязан объясниться, а не упасть: панель могла не успеть обновиться.
                var service = new OrganizationPurgeService(repeatDb, new FixedClock(now));
                var again = await service.PurgeAsync(organizationId, "leaving-club", CancellationToken.None);
                Assert.Equal(OffboardingErrorCodes.OrganizationPurged, again.ErrorCode);
            }
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
