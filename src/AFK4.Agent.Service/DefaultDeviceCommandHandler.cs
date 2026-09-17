using System.Text.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DefaultDeviceCommandHandler(
    IOptions<AgentOptions> options,
    ISessionEnforcementCoordinator enforcementCoordinator,
    IShellWarningStore shellWarningStore,
    ILogger<DefaultDeviceCommandHandler> logger) : IDeviceCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Причина предупреждения с сервера — в вид, который оболочка умеет показать. Причина, которой
    /// здесь нет, до экрана не доедет: придумывать за сервер, чем именно пугать игрока, нельзя.
    /// </summary>
    private static readonly Dictionary<string, string> WarningKindByReason = new(StringComparer.OrdinalIgnoreCase)
    {
        ["time-almost-up"] = PlayerShellWarningKinds.LowTime,
        ["credit-limit"] = PlayerShellWarningKinds.CreditLimit,
        ["low-balance"] = PlayerShellWarningKinds.LowBalance
    };

    public async Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var status = "Accepted";
        var message = "Command accepted.";
        var outcome = DeviceCommandOutcomeNames.Accepted;

        if (IsSessionLeaseCommand(command.Type))
        {
            var leaseResult = TryReadAndValidateLease(command);
            if (leaseResult.Lease is null)
            {
                status = "Rejected";
                message = leaseResult.Error ?? "Session lease is invalid.";
                outcome = leaseResult.Outcome ?? DeviceCommandOutcomeNames.LeaseInvalid;
            }
            else if (string.Equals(command.Type, DeviceCommandTypeNames.Unlock, StringComparison.OrdinalIgnoreCase))
            {
                var enforcement = await enforcementCoordinator.UnlockAsync(leaseResult.Lease, cancellationToken);
                status = enforcement.Status;
                message = enforcement.Message;
                outcome = enforcement.Outcome;
            }
            else
            {
                var enforcement = await enforcementCoordinator.RefreshLeaseAsync(leaseResult.Lease, cancellationToken);
                status = enforcement.Status;
                message = enforcement.Message;
                outcome = enforcement.Outcome;
            }
        }
        else if (string.Equals(command.Type, DeviceCommandTypeNames.Lock, StringComparison.OrdinalIgnoreCase))
        {
            var enforcement = await enforcementCoordinator.LockAsync(ReadSessionId(command), cancellationToken);
            status = enforcement.Status;
            message = enforcement.Message;
            outcome = enforcement.Outcome;
        }
        else if (string.Equals(command.Type, DeviceCommandTypeNames.Warn, StringComparison.OrdinalIgnoreCase))
        {
            var reason = ReadReason(command);
            if (WarningKindByReason.TryGetValue(reason, out var kind))
            {
                shellWarningStore.Warn(ReadSessionId(command), kind);
                message = $"Warning '{reason}' is on the player screen.";
                outcome = DeviceCommandOutcomeNames.WarningShown;
            }
            else
            {
                status = "Rejected";
                message = $"Unknown warning reason '{reason}'.";
                outcome = DeviceCommandOutcomeNames.WarningReasonUnknown;
                logger.LogWarning("Warn command carried an unknown reason '{Reason}'; nothing was shown to the player.", reason);
            }
        }
        else
        {
            // Отвечать «принято» на команду, которую агент не умеет исполнять, — врать серверу:
            // в журнале она значилась бы выполненной. Оператор увидит отказ и причину.
            status = "Rejected";
            message = $"Agent does not implement command type '{command.Type}'.";
            outcome = DeviceCommandOutcomeNames.CommandNotImplemented;
            logger.LogWarning("Device command type '{CommandType}' is not implemented by this agent.", command.Type);
        }

        var result = new DeviceCommandResultDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            CommandId: command.CommandId,
            Status: status,
            Message: message,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Outcome: outcome);

        return result;
    }

    private static (SessionLeaseDto? Lease, string? Error, string? Outcome) TryReadAndValidateLease(DeviceCommandDto command)
    {
        if (!command.Payload.TryGetValue("sessionLease", out var leaseJson) || string.IsNullOrWhiteSpace(leaseJson))
        {
            return (null, "Command payload must include sessionLease.", DeviceCommandOutcomeNames.LeaseMissing);
        }

        try
        {
            var lease = JsonSerializer.Deserialize<SessionLeaseDto>(leaseJson, JsonOptions);
            if (lease is null)
            {
                return (null, "Command sessionLease could not be read.", DeviceCommandOutcomeNames.LeaseUnreadable);
            }

            return (lease, null, null);
        }
        catch (JsonException)
        {
            return (null, "Command sessionLease is not valid JSON.", DeviceCommandOutcomeNames.LeaseUnreadable);
        }
    }

    private static bool IsSessionLeaseCommand(string commandType)
    {
        return string.Equals(commandType, DeviceCommandTypeNames.Unlock, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(commandType, DeviceCommandTypeNames.RefreshSessionLease, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadReason(DeviceCommandDto command) =>
        command.Payload.TryGetValue("reason", out var reason) && !string.IsNullOrWhiteSpace(reason)
            ? reason
            : string.Empty;

    private static Guid? ReadSessionId(DeviceCommandDto command)
    {
        return command.Payload.TryGetValue("sessionId", out var sessionId) &&
            Guid.TryParse(sessionId, out var parsedSessionId)
                ? parsedSessionId
                : null;
    }
}
