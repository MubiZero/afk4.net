using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DefaultDeviceCommandHandler(
    IOptions<AgentOptions> options,
    SessionLeaseValidator leaseValidator,
    ISessionLeaseStore leaseStore) : IDeviceCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var status = "Accepted";
        var message = "Command accepted by Agent skeleton.";

        if (IsSessionLeaseCommand(command.Type))
        {
            var leaseResult = TryReadAndValidateLease(command);
            if (!leaseResult.Result.IsValid)
            {
                status = "Rejected";
                message = leaseResult.Result.Error ?? "Session lease is invalid.";
            }
            else
            {
                leaseStore.Save(leaseResult.Lease!);
                message = "Session lease accepted.";
            }
        }
        else if (string.Equals(command.Type, "lock", StringComparison.OrdinalIgnoreCase))
        {
            leaseStore.Clear(ReadSessionId(command));
        }

        var result = new DeviceCommandResultDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            CommandId: command.CommandId,
            Status: status,
            Message: message,
            ObservedAtUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }

    private (SessionLeaseDto? Lease, SessionLeaseValidationResult Result) TryReadAndValidateLease(DeviceCommandDto command)
    {
        if (!command.Payload.TryGetValue("sessionLease", out var leaseJson) || string.IsNullOrWhiteSpace(leaseJson))
        {
            return (null, SessionLeaseValidationResult.Invalid("Command payload must include sessionLease."));
        }

        try
        {
            var lease = JsonSerializer.Deserialize<SessionLeaseDto>(leaseJson, JsonOptions);
            if (lease is null)
            {
                return (null, SessionLeaseValidationResult.Invalid("Command sessionLease could not be read."));
            }

            var result = leaseValidator.Validate(lease);
            return (lease, result);
        }
        catch (JsonException)
        {
            return (null, SessionLeaseValidationResult.Invalid("Command sessionLease is not valid JSON."));
        }
    }

    private static bool IsSessionLeaseCommand(string commandType)
    {
        return string.Equals(commandType, "unlock", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandType, "refresh-session-lease", StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ReadSessionId(DeviceCommandDto command)
    {
        return command.Payload.TryGetValue("sessionId", out var sessionId) &&
            Guid.TryParse(sessionId, out var parsedSessionId)
                ? parsedSessionId
                : null;
    }
}
