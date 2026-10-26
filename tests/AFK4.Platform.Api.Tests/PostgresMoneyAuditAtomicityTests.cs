using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Players;
using AFK4.Platform.Api.Shifts;
using AFK4.Platform.Api.Tests.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Деньги и запись журнала о них — одна транзакция. Раньше журнал писался отдельным сохранением
/// после коммита денег, и сбой между ними оставлял пополнение без записи. Проверка идёт на настоящем
/// PostgreSQL: in-memory провайдер транзакций не откатывает.
/// </summary>
public sealed class PostgresMoneyAuditAtomicityTests
{
    [PostgresSessionFact]
    public async Task WhenTheAuditCannotBeWritten_TheMoneyIsNotWrittenEither()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await OpenShiftAsync(database);

        await using (var db = database.CreateDbContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Billing(database, db).TopUpWalletAsync(
                database.PlayerAccountId,
                database.BranchId,
                database.StaffUserId,
                TopUp(database, "audit-fails"),
                CancellationToken.None,
                auditOnCommit: _ => throw new InvalidOperationException("audit store is down")));
        }

        await using var verify = database.CreateDbContext();
        Assert.Equal(0, await verify.LedgerEntries.CountAsync(entry => entry.PlayerAccountId == database.PlayerAccountId));
        Assert.Equal(0, await verify.AuditRecords.CountAsync(record => record.Action == AuditActionNames.TopUpWallet));
    }

    [PostgresSessionFact]
    public async Task ATopUp_CommitsItsAuditRecord_AndARetryWithTheSameKeyDoesNotRepeatIt()
    {
        await using var database = await SessionStartPostgresFixture.CreateAsync(
            Environment.GetEnvironmentVariable(PostgresSessionFactAttribute.EnvironmentVariable)!);
        await database.SeedAsync();
        await OpenShiftAsync(database);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = database.CreateDbContext();
            var result = await Billing(database, db).TopUpWalletAsync(
                database.PlayerAccountId,
                database.BranchId,
                database.StaffUserId,
                TopUp(database, "audit-once"),
                CancellationToken.None,
                auditOnCommit: _ => [new AuditRecordWriteRequest(
                    database.OrganizationId,
                    database.BranchId,
                    database.StaffUserId,
                    AuditActionNames.TopUpWallet,
                    "PlayerAccount",
                    database.PlayerAccountId.ToString("D"),
                    AuditOutcome.Succeeded,
                    "PlatformApi",
                    "{}")]);
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = database.CreateDbContext();
        Assert.Equal(1, await verify.AuditRecords.CountAsync(record => record.Action == AuditActionNames.TopUpWallet));
    }

    private static TopUpWalletRequest TopUp(SessionStartPostgresFixture database, string key) =>
        new(database.OrganizationId, new MoneyDto("TJS", 5_000), "front desk cash top-up", key);

    private static EfBillingCommandService Billing(SessionStartPostgresFixture database, PlatformDbContext db)
    {
        var time = new FixedTimeProvider(database.Now);
        return new EfBillingCommandService(
            db,
            new EfShiftService(db, time),
            time,
            new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance),
            new ReferralService(db, AlwaysEnabledOrganizationEntitlements.Instance, time),
            new AuditRecordStager(db, time, new StaffContextAccessor()));
    }

    private static async Task OpenShiftAsync(SessionStartPostgresFixture database)
    {
        await using var db = database.CreateDbContext();
        var opened = await new EfShiftService(db, new FixedTimeProvider(database.Now)).OpenShiftAsync(
            database.BranchId,
            database.StaffUserId,
            new OpenShiftRequest(database.OrganizationId, new MoneyDto("TJS", 0), "test", "open-shift"),
            CancellationToken.None);
        Assert.True(opened.Succeeded, opened.Error);
    }
}
