using AFK4.Shared.Contracts.Devices;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Sessions;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHub(
    ILogger<DeviceHub> logger,
    IDeviceCredentialValidator credentialValidator,
    IDeviceConnectionRegistry connectionRegistry,
    IDeviceCommandStore commandStore,
    IStaffTokenService staffTokenService,
    ISessionCommandResultProcessor sessionCommandResultProcessor) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var staffContext = await staffTokenService.ValidateAsync(
            ReadStaffBearerToken(),
            Context.ConnectionAborted);
        if (staffContext is not null)
        {
            foreach (var branchId in staffContext.BranchIds)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    DeviceHubGroups.Branch(branchId),
                    Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task RegisterDeviceAsync(DeviceConnectionRequest request)
    {
        if (!credentialValidator.ValidateApproved(
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
        await sessionCommandResultProcessor.ProcessAsync(result, Context.ConnectionAborted);

        await Clients.Group(DeviceHubGroups.Branch(result.BranchId)).SendAsync(
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

    private string? ReadStaffBearerToken()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[bearerPrefix.Length..].Trim();
        }

        var accessToken = httpContext.Request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(accessToken) ? null : accessToken.Trim();
    }
}
