using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Features;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

/// <summary>Тарифы клуба: бесплатно до 10 ПК, дальше 10 сомони за ПК; пробный период, обещанный платёж, бесплатный вместо блокировки.</summary>
public sealed class ClubPlansTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-01T00:00:00Z");

    [Fact]
    public async Task AFreeClub_StartsItsOwnTrial_Once_AndLosesTheFreeLimits()
    {
        var fixture = await Fixture.CreateAsync(devices: 14);

        var before = (await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!;
        Assert.Equal(ClubPlanKindNames.Free, before.Kind);
        Assert.True(before.TrialAvailable);
        Assert.Equal(14, before.Devices);
        Assert.Equal(4, before.BillableDevices);

        Assert.Equal(string.Empty, await fixture.Plans.StartTrialAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None));
        var trial = (await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!;
        Assert.Equal(ClubPlanKindNames.Trial, trial.Kind);
        Assert.Equal(Start.AddDays(ClubPlanLimits.TrialDays), trial.TrialEndsAtUtc);
        Assert.False(trial.TrialAvailable);
        Assert.Equal("{\"maxBranches\":null,\"maxDevicesPerBranch\":null,\"maxConcurrentSessions\":null,\"maxStaffUsersPerBranch\":null,\"maxDevices\":null}",
            (await fixture.Db.Organizations.SingleAsync()).LimitsJson, ignoreCase: true);
        Assert.Contains(fixture.Audit.Records, record => record.Action == AuditActionNames.StartPlanTrial);
    }

    [Theory]
    [InlineData(14, OrganizationPlanCodeNames.PerPc)]
    [InlineData(8, OrganizationPlanCodeNames.Free)]
    public async Task AnEndedTrial_StaysPaid_OnlyWithMoreThanTenPcs(int devices, string expectedPlan)
    {
        var fixture = await Fixture.CreateAsync(devices);
        await fixture.Plans.StartTrialAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(1, await fixture.Plans.RunTransitionsAsync(Start.AddDays(ClubPlanLimits.TrialDays), CancellationToken.None));

        var subscription = await fixture.Db.OrganizationSubscriptions.SingleAsync();
        Assert.Equal(expectedPlan, subscription.PlanCode);
        Assert.Equal(SubscriptionStatusNames.Active, subscription.Status);
    }

    [Fact]
    public async Task TheMonthlyInvoice_ChargesEveryPcAboveTen_AndTenOrFewerMeansTheFreePlan()
    {
        var fixture = await Fixture.CreateAsync(devices: 14);
        await fixture.Plans.SwitchToPerPcAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);
        var runner = new EfInvoiceGenerationRunner(fixture.Db, Options.Create(new BillingOptions()), new RecordingInvoiceNotifier());

        Assert.Equal(1, await runner.RunAsync(Start.AddMonths(1), CancellationToken.None));
        var invoice = await fixture.Db.Invoices.SingleAsync();
        Assert.Equal(4 * ClubPlanLimits.PricePerDeviceMinorUnits, invoice.AmountMinorUnits);

        // ПК сняли — к следующему счёту их десять: платить не за что, клуб на бесплатном.
        foreach (var device in await fixture.Db.Devices.Take(4).ToListAsync()) device.EnrollmentState = DeviceEnrollmentStateNames.Removed;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(0, await runner.RunAsync(Start.AddMonths(2), CancellationToken.None));
        Assert.Equal(OrganizationPlanCodeNames.Free, (await fixture.Db.OrganizationSubscriptions.SingleAsync()).PlanCode);
    }

    [Fact]
    public async Task AnUnpaidInvoice_MovesTheClubToFree_UnlessItPromisedToPay()
    {
        var fixture = await Fixture.CreateAsync(devices: 14);
        await fixture.Plans.SwitchToPerPcAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);
        var due = Start.AddDays(7);
        fixture.Db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(), OrganizationId = fixture.OrganizationId, Number = 1, Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = Start, PeriodEndUtc = Start.AddMonths(1), IssuedAtUtc = Start, DueAtUtc = due, AmountMinorUnits = 4000,
            GrossAmountMinorUnits = 4000, CurrencyCode = "TJS", Status = InvoiceStatusNames.Overdue, Description = "test",
            CreatedAtUtc = Start, UpdatedAtUtc = Start
        });
        await fixture.Db.SaveChangesAsync();

        // Обещанный платёж — неделя без перехода; второй раз под тот же счёт — нельзя.
        fixture.Clock.Now = due.AddDays(10);
        Assert.Equal(string.Empty, await fixture.Plans.PromisePaymentAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(ClubPlanErrorCodeNames.PromiseUsed, await fixture.Plans.PromisePaymentAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(0, await fixture.Plans.RunTransitionsAsync(due.AddDays(ClubPlanLimits.FallbackAfterOverdueDays), CancellationToken.None));

        Assert.Equal(1, await fixture.Plans.RunTransitionsAsync(due.AddDays(10 + ClubPlanLimits.PromisedPaymentDays), CancellationToken.None));
        var subscription = await fixture.Db.OrganizationSubscriptions.SingleAsync();
        Assert.Equal(OrganizationPlanCodeNames.Free, subscription.PlanCode);
        // Долг переходом не прощается, а вернуться на тариф за ПК можно, только расплатившись.
        Assert.Equal(ClubPlanErrorCodeNames.OverdueInvoices, await fixture.Plans.SwitchToPerPcAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None));
        Assert.Contains(fixture.Audit.Records, record => record.Action == AuditActionNames.FallBackToFreePlan);
    }

    [Fact]
    public async Task AFallenClub_RunsOnlyTenPcs_AndTheOwnerChoosesWhich()
    {
        var fixture = await Fixture.CreateAsync(devices: 14);
        await fixture.Plans.SwitchToPerPcAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(0, (await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!.DevicesOutsidePlan);
        await fixture.AddOverdueInvoiceAsync(due: Start.AddDays(7));

        await fixture.Plans.RunTransitionsAsync(Start.AddDays(7 + ClubPlanLimits.FallbackAfterOverdueDays), CancellationToken.None);

        // Пока владелец не выбрал — работают подключённые раньше: ПК 0–9; ПК 10–13 вне тарифа.
        var plan = (await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!;
        Assert.Equal(ClubPlanKindNames.Free, plan.Kind);
        Assert.Equal(4, plan.DevicesOutsidePlan);
        var devices = await fixture.Plans.DevicesAsync(fixture.OrganizationId, CancellationToken.None);
        Assert.Equal(ClubPlanLimits.FreeDevices, devices.Limit);
        Assert.Equal(["ПК 10", "ПК 11", "ПК 12", "ПК 13"], devices.Devices.Where(device => !device.Works).Select(device => device.Name).Order());

        // Владелец держит ПК 12 и ПК 13 — остальные восемь мест добираются по старшинству.
        var kept = devices.Devices.Where(device => device.Name is "ПК 12" or "ПК 13").Select(device => device.DeviceId).ToList();
        Assert.Equal(string.Empty, await fixture.Plans.KeepDevicesAsync(fixture.OrganizationId, kept, Guid.NewGuid(), CancellationToken.None));
        var after = await fixture.Plans.DevicesAsync(fixture.OrganizationId, CancellationToken.None);
        Assert.Equal(["ПК 10", "ПК 11", "ПК 8", "ПК 9"], after.Devices.Where(device => !device.Works).Select(device => device.Name).Order());
        Assert.All(after.Devices.Where(device => device.Kept), device => Assert.True(device.Works));
        Assert.Contains(fixture.Audit.Records, record => record.Action == AuditActionNames.KeepPlanDevices);

        var all = after.Devices.Select(device => device.DeviceId).ToList();
        Assert.Equal(ClubPlanErrorCodeNames.TooManyDevices, await fixture.Plans.KeepDevicesAsync(fixture.OrganizationId, all, Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(ClubPlanErrorCodeNames.UnknownDevice,
            await fixture.Plans.KeepDevicesAsync(fixture.OrganizationId, [Guid.NewGuid()], Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ThePlan_SaysWhenTheClubFallsToFree_AndAPromiseMovesTheDate()
    {
        var fixture = await Fixture.CreateAsync(devices: 14);
        await fixture.Plans.SwitchToPerPcAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);
        Assert.Null((await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!.FallbackAtUtc);
        var due = Start.AddDays(7);
        await fixture.AddOverdueInvoiceAsync(due);

        Assert.Equal(due.AddDays(ClubPlanLimits.FallbackAfterOverdueDays),
            (await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!.FallbackAtUtc);

        fixture.Clock.Now = due.AddDays(10);
        await fixture.Plans.PromisePaymentAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(due.AddDays(10 + ClubPlanLimits.PromisedPaymentDays),
            (await fixture.Plans.DescribeAsync(fixture.OrganizationId, CancellationToken.None))!.FallbackAtUtc);
    }

    [Fact]
    public async Task TheFreePlan_ShowsPlatformAds_AndThePerPcPlanDoesNot()
    {
        var fixture = await Fixture.CreateAsync(devices: 3);
        var entitlements = new EfOrganizationEntitlements(fixture.Db);

        Assert.Contains(PlatformFeatureNames.PlatformAds, await entitlements.ListEnabledAsync(fixture.OrganizationId, CancellationToken.None));
        await fixture.Plans.SwitchToPerPcAsync(fixture.OrganizationId, Guid.NewGuid(), CancellationToken.None);
        Assert.DoesNotContain(PlatformFeatureNames.PlatformAds, await entitlements.ListEnabledAsync(fixture.OrganizationId, CancellationToken.None));
    }

    private sealed class Fixture
    {
        public required PlatformDbContext Db { get; init; }
        public required ClubPlans Plans { get; init; }
        public required RecordingAudit Audit { get; init; }
        public required FixedTimeProvider Clock { get; init; }
        public required Guid OrganizationId { get; init; }

        public async Task AddOverdueInvoiceAsync(DateTimeOffset due)
        {
            Db.Invoices.Add(new InvoiceEntity
            {
                InvoiceId = Guid.NewGuid(), OrganizationId = OrganizationId, Number = 1, Kind = InvoiceKindNames.Subscription,
                PeriodStartUtc = Start, PeriodEndUtc = Start.AddMonths(1), IssuedAtUtc = Start, DueAtUtc = due, AmountMinorUnits = 4000,
                GrossAmountMinorUnits = 4000, CurrencyCode = "TJS", Status = InvoiceStatusNames.Overdue, Description = "test",
                CreatedAtUtc = Start, UpdatedAtUtc = Start
            });
            await Db.SaveChangesAsync();
        }

        public static async Task<Fixture> CreateAsync(int devices)
        {
            var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
            var organizationId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.Free, Name = "Бесплатный", MaxDevices = ClubPlanLimits.FreeDevices,
                    CreatedAtUtc = Start, UpdatedAtUtc = Start
                },
                new SubscriptionPlanEntity
                {
                    PlanCode = OrganizationPlanCodeNames.PerPc, Name = "За ПК", PricePerDeviceMinorUnits = ClubPlanLimits.PricePerDeviceMinorUnits,
                    IncludedDevices = ClubPlanLimits.FreeDevices, CreatedAtUtc = Start, UpdatedAtUtc = Start
                });
            db.PlanFeatures.AddRange(
                new PlanFeatureEntity { PlanFeatureId = Guid.NewGuid(), PlanCode = OrganizationPlanCodeNames.Free, FeatureKey = PlatformFeatureNames.PlatformAds, IsIncluded = true },
                new PlanFeatureEntity { PlanFeatureId = Guid.NewGuid(), PlanCode = OrganizationPlanCodeNames.PerPc, FeatureKey = PlatformFeatureNames.PlatformAds, IsIncluded = false });
            db.PlatformFeatures.Add(new PlatformFeatureEntity
            {
                FeatureKey = PlatformFeatureNames.PlatformAds, Name = "Реклама", Description = "Реклама", EnabledByDefault = false, CreatedAtUtc = Start
            });
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = organizationId, Slug = "club", Name = "Клуб", Status = OrganizationStatusNames.Active,
                PlanCode = OrganizationPlanCodeNames.Free, SubscriptionStatus = SubscriptionStatusNames.Active,
                LimitsJson = "{\"MaxDevices\":10}",
                CreatedAtUtc = Start, UpdatedAtUtc = Start
            });
            db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
            {
                OrganizationSubscriptionId = Guid.NewGuid(), OrganizationId = organizationId, PlanCode = OrganizationPlanCodeNames.Free,
                Status = SubscriptionStatusNames.Active, CurrentPeriodStartUtc = Start, CurrentPeriodEndUtc = Start.AddMonths(1),
                CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly, CreatedAtUtc = Start, UpdatedAtUtc = Start
            });
            for (var index = 0; index < devices; index++)
            {
                db.Devices.Add(new DeviceEntity
                {
                    DeviceId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, MachineName = $"PC-{index}",
                    DisplayName = $"ПК {index}", DevicePublicKey = $"key-{index}", EnrolledAtUtc = Start.AddMinutes(index)
                });
            }

            // Рабочее место управляющего не игровой ПК — не считается.
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, MachineName = "ADMIN",
                DisplayName = "Стойка", DevicePublicKey = "admin", Role = DeviceRoleNames.ManagerWorkstation
            });
            db.Branches.Add(new BranchEntity { BranchId = branchId, OrganizationId = organizationId, Slug = "hall", Name = "Зал", CreatedAtUtc = Start });
            await db.SaveChangesAsync();
            var clock = new FixedTimeProvider(Start);
            var audit = new RecordingAudit();
            return new Fixture { Db = db, Plans = new ClubPlans(db, audit, clock), Audit = audit, Clock = clock, OrganizationId = organizationId };
        }
    }

    private sealed class RecordingAudit : IAuditRecordWriter
    {
        public List<AuditRecordWriteRequest> Records { get; } = [];

        public Task WriteAsync(AuditRecordWriteRequest request, CancellationToken cancellationToken)
        {
            Records.Add(request);
            return Task.CompletedTask;
        }
    }
}
