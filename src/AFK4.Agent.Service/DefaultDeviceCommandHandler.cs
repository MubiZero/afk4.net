using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DefaultDeviceCommandHandler(IOptions<AgentOptions> options) : IDeviceCommandHandler
{
    public Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var result = new DeviceCommandResultDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            CommandId: command.CommandId,
            Status: "Accepted",
            Message: "Command accepted by Agent skeleton.",
            ObservedAtUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }
}
