using System.Net.Http.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

/// <summary>Сказать серверу, что техник вернул ПК в зал кнопкой на самом ПК.</summary>
public interface IMaintenanceReturnClient
{
    Task ReturnAsync(CancellationToken cancellationToken);
}

public sealed class HttpMaintenanceReturnClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IMaintenanceReturnClient
{
    public async Task ReturnAsync(CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        using var message = new HttpRequestMessage(HttpMethod.Post, DeviceMaintenanceRoutes.Return(agentOptions.DeviceId))
        {
            Content = JsonContent.Create(new DeviceMaintenanceReturnRequest(
                agentOptions.OrganizationId,
                agentOptions.BranchId,
                agentOptions.DeviceId))
        };

        // Ключ берётся из хранилища, а не из конфига: после смены в конфиге лежит старый.
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// «Вернуть в зал» с полосы обслуживания (спека оболочки, §6.5). Сначала сервер, потом машина:
/// если сервер не узнал, ПК закрылся бы здесь, а карта и сердцебиение через десять секунд открыли
/// бы его обратно. Без связи честно отказываем — вернуть ПК можно из Панели.
/// </summary>
public sealed class MaintenanceReturn(
    IMaintenanceReturnClient client,
    IMaintenanceMode maintenanceMode,
    IAgentRuntimeStateStore runtimeStateStore,
    ILogger<MaintenanceReturn> logger)
{
    public async Task<ShellPipeReplyDto> ReturnAsync(ShellPipeRequestDto request, CancellationToken cancellationToken)
    {
        if (runtimeStateStore.Current.State != PlayerShellStateNames.Maintenance)
        {
            return new ShellPipeReplyDto(request.RequestId, Ok: true);
        }

        try
        {
            await client.ReturnAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Returning the PC to the floor did not reach the platform.");
            return new ShellPipeReplyDto(
                request.RequestId,
                Ok: false,
                ShellPipeErrorCodeNames.PlatformUnreachable,
                "The platform could not be reached.");
        }

        await maintenanceMode.LeaveAsync(cancellationToken);
        return new ShellPipeReplyDto(request.RequestId, Ok: true);
    }
}
