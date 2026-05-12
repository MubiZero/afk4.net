using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceRealtimeClient realtimeClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
        try
        {
            await realtimeClient.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Realtime device channel failed to start. Continuing with HTTP heartbeats.");
        }

        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            var request = HeartbeatPayloadFactory.Create(agentOptions, isLocked: true, DateTimeOffset.UtcNow);
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{agentOptions.DeviceId}/heartbeat")
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(agentOptions.DeviceCredentialSecret))
            {
                message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, agentOptions.DeviceCredentialSecret);
            }

            var response = await client.SendAsync(message, stoppingToken);

            response.EnsureSuccessStatusCode();
            var heartbeat = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>(cancellationToken: stoppingToken);
            var intervalSeconds = heartbeat?.HeartbeatIntervalSeconds ?? 10;

            logger.LogInformation("Heartbeat sent for {DeviceId}. Next heartbeat in {IntervalSeconds}s.", agentOptions.DeviceId, intervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await realtimeClient.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
