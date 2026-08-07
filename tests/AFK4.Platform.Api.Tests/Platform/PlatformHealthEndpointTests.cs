using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Shared.Contracts.Platform.Health;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformHealthEndpointTests
{
    [Fact]
    public async Task GET_overview_WithPermission_ReturnsJobsAndIncidents()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PlatformJobRuns.Add(new PlatformJobRunEntity
            {
                PlatformJobRunId = Guid.NewGuid(),
                JobName = PlatformJobNames.InvoiceGeneration,
                StartedAtUtc = now.AddMinutes(-3),
                FinishedAtUtc = now.AddMinutes(-3).AddSeconds(1),
                Outcome = PlatformJobOutcomeNames.Succeeded,
                ItemsProcessed = 2
            });
            db.PlatformIncidents.Add(new PlatformIncidentEntity
            {
                PlatformIncidentId = Guid.NewGuid(),
                Kind = PlatformIncidentKindNames.NotificationQueueStuck,
                DedupKey = PlatformIncidentKindNames.NotificationQueueStuck,
                Severity = PlatformIncidentSeverityNames.Critical,
                DetailsJson = "{\"failed\":\"2\"}",
                OpenedAtUtc = now.AddHours(-1),
                LastSeenAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/platform/health/overview");
        var overview = await response.Content.ReadFromJsonAsync<PlatformHealthOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Contains(overview!.Jobs, job => job.JobName == PlatformJobNames.InvoiceGeneration && job.LastItemsProcessed == 2);
        Assert.Contains(overview.OpenIncidents, incident => incident.Kind == PlatformIncidentKindNames.NotificationQueueStuck);
    }

    [Fact]
    public async Task GET_overview_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/health/overview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_overview_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: []);

        var response = await client.GetAsync("/api/platform/health/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
