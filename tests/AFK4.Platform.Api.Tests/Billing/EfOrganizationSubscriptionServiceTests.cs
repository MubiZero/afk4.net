using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfOrganizationSubscriptionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedOrgAndPlansAsync(PlatformDbContext db, string planCode = "starter")
    {
        db.SubscriptionPlans.AddRange(
            new SubscriptionPlanEntity { PlanCode = "starter", Name = "Starter", PriceMinorUnits = 290000, CurrencyCode = "TJS", BillingInterval = "monthly", MaxBranches = 1, MaxDevicesPerBranch = 30, MaxConcurrentSessions = 40, MaxStaffUsersPerBranch = 10, IsActive = true, SortOrder = 1, CreatedAtUtc = Now, UpdatedAtUtc = Now },
            new SubscriptionPlanEntity { PlanCode = "scale", Name = "Scale", PriceMinorUnits = 1990000, CurrencyCode = "TJS", BillingInterval = "monthly", MaxBranches = 10, MaxDevicesPerBranch = 120, MaxConcurrentSessions = 200, MaxStaffUsersPerBranch = 50, IsActive = true, SortOrder = 3, CreatedAtUtc = Now, UpdatedAtUtc = Now });
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId,
            Slug = "demo",
            Name = "Demo",
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            SubscriptionStatus = SubscriptionStatusNames.Active,
            LimitsJson = "{}",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return orgId;
    }

    [Fact]
    public async Task GetAsync_LazilyCreatesSubscriptionFromCatalogPlan()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));

        var result = await service.GetAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("starter", result.Value!.PlanCode);
        Assert.Equal(290000, result.Value.AmountMinorUnits);
        Assert.Equal(Now, result.Value.CurrentPeriodStartUtc);
        Assert.Equal(Now.AddMonths(1), result.Value.CurrentPeriodEndUtc);
        Assert.Equal(1, await db.OrganizationSubscriptions.CountAsync());
    }

    [Fact]
    public async Task GetAsync_UnknownOrg_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpgradeMidCycle_IssuesProrationInvoiceAndSyncsOrg()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var time = new FixedTimeProvider(Now);
        var service = new EfOrganizationSubscriptionService(db, time);
        await service.GetAsync(orgId, CancellationToken.None); // create subscription (period 05-31 -> 06-30)

        time.Now = Now.AddDays(15);
        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "scale", BillingInterval: null, Status: null, CancelAtPeriodEnd: null, AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("scale", result.Value!.PlanCode);
        Assert.Equal(1990000, result.Value.AmountMinorUnits);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceKindNames.Proration, invoice.Kind);
        // starter→scale, 15 of 30 days remaining: (1990000-290000)/30 * 15 = 850000
        Assert.Equal(850000, invoice.AmountMinorUnits);

        var org = await db.Organizations.SingleAsync(o => o.OrganizationId == orgId);
        Assert.Equal("scale", org.PlanCode);
        var limits = JsonSerializer.Deserialize<OrganizationLimitsDto>(org.LimitsJson)!;
        Assert.Equal(10, limits.MaxBranches);
    }

    [Fact]
    public async Task UpdateAsync_Downgrade_DoesNotIssueInvoice()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "scale");
        var time = new FixedTimeProvider(Now);
        var service = new EfOrganizationSubscriptionService(db, time);
        await service.GetAsync(orgId, CancellationToken.None);

        time.Now = Now.AddDays(15);
        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "starter", BillingInterval: null, Status: null, CancelAtPeriodEnd: null, AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(290000, result.Value!.AmountMinorUnits);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_StatusChange_SyncsOrgSubscriptionStatus()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: SubscriptionStatusNames.PastDue, CancelAtPeriodEnd: true, AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.CancelAtPeriodEnd);
        var org = await db.Organizations.SingleAsync(o => o.OrganizationId == orgId);
        Assert.Equal(SubscriptionStatusNames.PastDue, org.SubscriptionStatus);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPlan_ReturnsBadRequest()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "ghost", BillingInterval: null, Status: null, CancelAtPeriodEnd: null, AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_BothDiscountFormsSet_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountAmountMinorUnits: 50000), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task UpdateAsync_PercentOutOfRange_IsRejected(int percent)
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: percent), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_PlanChange_KeepsDiscount()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);
        await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountReason: "Договорённость на запуск"), CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "scale", BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(30, result.Value!.DiscountPercent);
        Assert.Equal(1990000, result.Value.AmountMinorUnits);
    }

    [Fact]
    public async Task UpdateAsync_ClearDiscount_RemovesAllDiscountFields()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);
        await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountUntilUtc: Now.AddMonths(3), DiscountReason: "Запуск"), CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            ClearDiscount: true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.DiscountPercent);
        Assert.Null(result.Value.DiscountAmountMinorUnits);
        Assert.Null(result.Value.DiscountUntilUtc);
        Assert.Null(result.Value.DiscountReason);
    }

    // Regression: DiscountReason had no length cap, unlike its siblings (InvoiceEntity.VoidReason is
    // 512, InvoiceEntity.Description is 240). Matches the new column HasMaxLength(512).
    [Fact]
    public async Task UpdateAsync_DiscountReasonTooLong_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountReason: new string('r', 513)), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    // Regression: ClearDiscount: true silently dropped a non-empty DiscountReason sent alongside it,
    // even though the sibling combination (ClearDiscount + an amount/percent) is an explicit
    // BadRequest. A caller who fat-fingers both in the same request should get the same treatment.
    [Fact]
    public async Task UpdateAsync_ClearDiscountWithReason_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);
        await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountReason: "Запуск"), CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            ClearDiscount: true, DiscountReason: "should not be silently ignored"), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
        // Nothing should have been cleared by the rejected call.
        var afterRejection = await service.GetAsync(orgId, CancellationToken.None);
        Assert.Equal(30, afterRejection.Value!.DiscountPercent);
    }

    // Regression for round 2 of a code-review finding on task 7: EfOrganizationSubscriptionService.
    // UpdateAsync had its own private NextInvoiceNumberAsync — a third copy of MAX(Number)+1 — for the
    // proration invoice it issues on a plan change, saved with a plain SaveChangesAsync and no retry.
    // A tariff change landing at the same moment as any other invoice write (here: an admin issuing a
    // one-off charge for a different club; in production this is just as easily the nightly
    // subscription-invoice tick) could race on the unique index on Invoices.Number and surface a raw
    // 500 on a money-affecting admin action. Both paths now go through the shared InvoiceNumbering
    // helper. The InMemory provider every other test in this file uses does not enforce unique indexes,
    // so this needs a real PostgreSQL instance with a deterministic overlap — same pattern as
    // EfInvoiceServiceTests.CreateAsync_TwoConcurrentOneOffInvoices_BothSucceedWithDistinctNumbers.
    [PlatformAdminPostgresFact]
    public async Task UpdateAsync_ProrationInvoiceRacesWithConcurrentOneOffInvoice_BothSucceedWithDistinctNumbers()
    {
        var connectionString = Environment.GetEnvironmentVariable(PlatformAdminPostgresFactAttribute.EnvironmentVariable)!;
        var rootBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var schema = $"invoice_numbering_race_subscription_{Guid.NewGuid():N}";
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

            Guid subscriptionOrgId, oneOffOrgId;
            await using (var seedDb = new PlatformDbContext(options))
            {
                seedDb.SubscriptionPlans.AddRange(
                    new SubscriptionPlanEntity
                    {
                        PlanCode = "starter", Name = "Starter", PriceMinorUnits = 290000, CurrencyCode = "TJS",
                        BillingInterval = "monthly", MaxBranches = 1, MaxDevicesPerBranch = 30,
                        MaxConcurrentSessions = 40, MaxStaffUsersPerBranch = 10, IsActive = true, SortOrder = 1,
                        CreatedAtUtc = Now, UpdatedAtUtc = Now
                    },
                    new SubscriptionPlanEntity
                    {
                        PlanCode = "scale", Name = "Scale", PriceMinorUnits = 1990000, CurrencyCode = "TJS",
                        BillingInterval = "monthly", MaxBranches = 10, MaxDevicesPerBranch = 120,
                        MaxConcurrentSessions = 200, MaxStaffUsersPerBranch = 50, IsActive = true, SortOrder = 3,
                        CreatedAtUtc = Now, UpdatedAtUtc = Now
                    });

                subscriptionOrgId = Guid.NewGuid();
                oneOffOrgId = Guid.NewGuid();
                seedDb.Organizations.AddRange(
                    new OrganizationEntity
                    {
                        OrganizationId = subscriptionOrgId, Slug = $"club-{subscriptionOrgId:N}", Name = "Тариф",
                        Status = OrganizationStatusNames.Active, PlanCode = "starter",
                        SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
                        CreatedAtUtc = Now, UpdatedAtUtc = Now
                    },
                    new OrganizationEntity
                    {
                        OrganizationId = oneOffOrgId, Slug = $"club-{oneOffOrgId:N}", Name = "Разовый",
                        Status = OrganizationStatusNames.Active, PlanCode = "starter",
                        SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
                        CreatedAtUtc = Now, UpdatedAtUtc = Now
                    });
                await seedDb.SaveChangesAsync();

                // Lazily materialize the subscription (mirrors GetAsync) so UpdateAsync has a period to
                // prorate against, before the gate is armed.
                var subscriptionSeedService = new EfOrganizationSubscriptionService(seedDb, new FixedTimeProvider(Now));
                await subscriptionSeedService.GetAsync(subscriptionOrgId, CancellationToken.None);
            }

            // Two independent DbContexts (independent connections/transactions): one changes a club's
            // tariff mid-cycle (issuing a proration invoice), the other issues an unrelated one-off
            // charge for a different club — mirrors an admin action landing at the same instant as
            // another invoice write.
            gate.Arm();
            await using var dbForSubscriptionUpdate = new PlatformDbContext(options);
            await using var dbForOneOff = new PlatformDbContext(options);
            var notifier = new RecordingInvoiceNotifier();
            var billingOptions = Options.Create(new BillingOptions());
            var subscriptionServiceForUpdate = new EfOrganizationSubscriptionService(
                dbForSubscriptionUpdate, new FixedTimeProvider(Now.AddDays(15)));
            var invoiceServiceForOneOff = new EfInvoiceService(
                dbForOneOff, new EfInvoiceGenerationRunner(dbForOneOff, billingOptions, notifier),
                notifier, new FixedTimeProvider(Now), billingOptions);

            var updateTask = subscriptionServiceForUpdate.UpdateAsync(subscriptionOrgId, new UpdateSubscriptionRequest(
                PlanCode: "scale", BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
                AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);
            var oneOffTask = invoiceServiceForOneOff.CreateAsync(oneOffOrgId, new CreateInvoiceRequest(
                InvoiceKindNames.OneOff, 150000, "Настройка оборудования", null), CancellationToken.None);

            await Task.WhenAll(updateTask, oneOffTask).WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(updateTask.Result.Succeeded);
            Assert.True(oneOffTask.Result.Succeeded);

            await using var verifyDb = new PlatformDbContext(options);
            var numbers = await verifyDb.Invoices.AsNoTracking()
                .Select(invoice => invoice.Number).ToListAsync();
            Assert.Equal(numbers.Count, numbers.Distinct().Count());
        }
        finally
        {
            await using var drop = root.CreateCommand();
            drop.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await drop.ExecuteNonQueryAsync();
        }
    }

    // Blocks each SavingChangesAsync call until exactly two concurrent saves have arrived, then
    // releases both together — forces both transactions to have already computed the same
    // MAX(Number) before either is allowed to flush its insert. Same pattern as
    // EfInvoiceServiceTests.SaveOverlapGate / PlatformSupportAccessTicketTests.SaveOverlapGate.
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
