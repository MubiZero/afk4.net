using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            var request = HeartbeatPayloadFactory.Create(agentOptions, isLocked: true, DateTimeOffset.UtcNow);
            var response = await client.PostAsJsonAsync(
                $"/api/devices/{agentOptions.DeviceId}/heartbeat",
                request,
                stoppingToken);

            response.EnsureSuccessStatusCode();
            var heartbeat = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>(cancellationToken: stoppingToken);
            var intervalSeconds = heartbeat?.HeartbeatIntervalSeconds ?? 10;

            logger.LogInformation("Heartbeat sent for {DeviceId}. Next heartbeat in {IntervalSeconds}s.", agentOptions.DeviceId, intervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
