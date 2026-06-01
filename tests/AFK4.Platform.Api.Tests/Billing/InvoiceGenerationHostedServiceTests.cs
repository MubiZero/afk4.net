using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class InvoiceGenerationHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_FirstTick_IssuesDueInvoice()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

        var services = new ServiceCollection();
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IInvoiceNotifier, RecordingInvoiceNotifier>();
        services.AddScoped<IInvoiceGenerationRunner, EfInvoiceGenerationRunner>();
        services.Configure<BillingOptions>(options => options.GenerationInterval = TimeSpan.FromHours(1));
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var orgId = Guid.NewGuid();
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = orgId, Slug = "o", Name = "O", Status = TenantStatusNames.Active,
                PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
                CreatedAtUtc = start, UpdatedAtUtc = start
            });
            db.TenantSubscriptions.Add(new TenantSubscriptionEntity
            {
                TenantSubscriptionId = Guid.NewGuid(), OrganizationId = orgId, PlanCode = "starter",
                Status = SubscriptionStatusNames.Active, CurrentPeriodStartUtc = start, CurrentPeriodEndUtc = start.AddMonths(1),
                NextInvoiceUtc = start.AddMonths(1), AmountMinorUnits = 290000, CurrencyCode = "RUB",
                BillingInterval = BillingIntervalNames.Monthly, CreatedAtUtc = start, UpdatedAtUtc = start
            });
            await db.SaveChangesAsync();
        }

        var time = new FixedTimeProvider(start.AddMonths(2));
        var hosted = new InvoiceGenerationHostedService(
            provider, time, Options.Create(new BillingOptions { GenerationInterval = TimeSpan.FromHours(1) }),
            NullLogger<InvoiceGenerationHostedService>.Instance);

        using var cts = new CancellationTokenSource();
        await hosted.StartAsync(cts.Token);

        InvoiceEntity? invoice = null;
        for (var attempt = 0; attempt < 200 && invoice is null; attempt++)
        {
            await Task.Delay(20, CancellationToken.None);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            invoice = await db.Invoices.FirstOrDefaultAsync(CancellationToken.None);
        }

        await cts.CancelAsync();
        await hosted.StopAsync(CancellationToken.None);

        Assert.NotNull(invoice);
        Assert.Equal(290000, invoice!.AmountMinorUnits);
    }
}
