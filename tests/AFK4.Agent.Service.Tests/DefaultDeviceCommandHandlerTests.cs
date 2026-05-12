using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class DefaultDeviceCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_AcknowledgesCommandForConfiguredDevice()
    {
        var options = Options.Create(new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001"
        });

        var handler = new DefaultDeviceCommandHandler(options);
        var command = new DeviceCommandDto(
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Type: "lock",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "operator-request"
            });

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(options.Value.OrganizationId, result.OrganizationId);
        Assert.Equal(options.Value.BranchId, result.BranchId);
        Assert.Equal(options.Value.DeviceId, result.DeviceId);
        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal("Accepted", result.Status);
        Assert.Equal("Command accepted by Agent skeleton.", result.Message);
    }
}
