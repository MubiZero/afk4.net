using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

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
}
