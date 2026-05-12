using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHub(
    ILogger<DeviceHub> logger,
    IDeviceCredentialValidator credentialValidator,
    IDeviceConnectionRegistry connectionRegistry,
    IDeviceCommandStore commandStore) : Hub
{
    public async Task RegisterDeviceAsync(DeviceConnectionRequest request)
    {
        if (!credentialValidator.Validate(
                request.OrganizationId,
                request.BranchId,
                request.DeviceId,
                request.CredentialSecret))
        {
            throw new HubException("Invalid device credential.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DeviceHubGroups.Device(request.DeviceId),
            Context.ConnectionAborted);

        connectionRegistry.Register(
            Context.ConnectionId,
            new DeviceConnectionIdentity(request.OrganizationId, request.BranchId, request.DeviceId));

        await Clients.Caller.SendAsync(
            DeviceRealtimeEvents.DeviceRegistered,
            request.DeviceId,
            Context.ConnectionAborted);

        logger.LogInformation(
            "Device {DeviceId} registered realtime connection {ConnectionId}.",
            request.DeviceId,
            Context.ConnectionId);
    }

    public async Task ReportCommandResultAsync(DeviceCommandResultDto result)
    {
        var identity = connectionRegistry.Get(Context.ConnectionId);
        if (identity is null ||
            identity.OrganizationId != result.OrganizationId ||
            identity.BranchId != result.BranchId ||
            identity.DeviceId != result.DeviceId)
        {
            throw new HubException("Command result device identity does not match the registered connection.");
        }

        await commandStore.ApplyResultAsync(result, Context.ConnectionAborted);

        await Clients.All.SendAsync(
            DeviceRealtimeEvents.DeviceCommandResult,
            result,
            Context.ConnectionAborted);

        logger.LogInformation(
            "Device {DeviceId} reported command {CommandId} as {Status}.",
            result.DeviceId,
            result.CommandId,
            result.Status);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connectionRegistry.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
