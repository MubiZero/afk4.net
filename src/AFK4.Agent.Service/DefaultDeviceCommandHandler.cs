using System.Text.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DefaultDeviceCommandHandler(
    IOptions<AgentOptions> options,
    ISessionEnforcementCoordinator enforcementCoordinator) : IDeviceCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var status = "Accepted";
        var message = "Command accepted by Agent skeleton.";

        if (IsSessionLeaseCommand(command.Type))
        {
            var leaseResult = TryReadAndValidateLease(command);
            if (leaseResult.Lease is null)
            {
                status = "Rejected";
                message = leaseResult.Error ?? "Session lease is invalid.";
            }
            else if (string.Equals(command.Type, DeviceCommandTypeNames.Unlock, StringComparison.OrdinalIgnoreCase))
            {
                var enforcement = await enforcementCoordinator.UnlockAsync(leaseResult.Lease, cancellationToken);
                status = enforcement.Status;
                message = enforcement.Message;
            }
            else
            {
                var enforcement = await enforcementCoordinator.RefreshLeaseAsync(leaseResult.Lease, cancellationToken);
                status = enforcement.Status;
                message = enforcement.Message;
            }
        }
        else if (string.Equals(command.Type, DeviceCommandTypeNames.Lock, StringComparison.OrdinalIgnoreCase))
        {
            var enforcement = await enforcementCoordinator.LockAsync(ReadSessionId(command), cancellationToken);
            status = enforcement.Status;
            message = enforcement.Message;
        }

        var result = new DeviceCommandResultDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            CommandId: command.CommandId,
            Status: status,
            Message: message,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        return result;
    }

    private static (SessionLeaseDto? Lease, string? Error) TryReadAndValidateLease(DeviceCommandDto command)
    {
        if (!command.Payload.TryGetValue("sessionLease", out var leaseJson) || string.IsNullOrWhiteSpace(leaseJson))
        {
            return (null, "Command payload must include sessionLease.");
        }

        try
        {
            var lease = JsonSerializer.Deserialize<SessionLeaseDto>(leaseJson, JsonOptions);
            if (lease is null)
            {
                return (null, "Command sessionLease could not be read.");
            }

            return (lease, null);
        }
        catch (JsonException)
        {
            return (null, "Command sessionLease is not valid JSON.");
        }
    }

    private static bool IsSessionLeaseCommand(string commandType)
    {
        return string.Equals(commandType, DeviceCommandTypeNames.Unlock, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandType, DeviceCommandTypeNames.RefreshSessionLease, StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ReadSessionId(DeviceCommandDto command)
    {
        return command.Payload.TryGetValue("sessionId", out var sessionId) &&
            Guid.TryParse(sessionId, out var parsedSessionId)
                ? parsedSessionId
                : null;
    }
}
