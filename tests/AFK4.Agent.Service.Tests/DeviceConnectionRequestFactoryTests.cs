using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

public sealed class DeviceConnectionRequestFactoryTests
{
    [Fact]
    public void Create_BuildsConnectionRequestFromAgentOptions()
    {
        var options = new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0"
        };

        var request = DeviceConnectionRequestFactory.Create(
            options,
            DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        Assert.Equal(options.OrganizationId, request.OrganizationId);
        Assert.Equal(options.BranchId, request.BranchId);
        Assert.Equal(options.DeviceId, request.DeviceId);
        Assert.Equal("PC-001", request.MachineName);
        Assert.Equal("0.1.0", request.AgentVersion);
        Assert.Equal("0.1.0", request.ShellVersion);
    }
}
