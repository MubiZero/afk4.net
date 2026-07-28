using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class AgentComponentVersionProvider(IOptions<AgentOptions> options) : IAgentComponentVersionProvider
{
    public IReadOnlyList<DeviceComponentVersionDto> GetInstalledComponents()
    {
        var agentOptions = options.Value;
        var components = new List<DeviceComponentVersionDto>();

        if (!string.IsNullOrWhiteSpace(agentOptions.AgentVersion))
        {
            components.Add(new DeviceComponentVersionDto(UpdateComponentNames.AgentService, agentOptions.AgentVersion));
        }

        if (agentOptions.DeviceRole == DeviceRoleNames.GamingPc &&
            !string.IsNullOrWhiteSpace(agentOptions.ShellVersion))
        {
            components.Add(new DeviceComponentVersionDto(UpdateComponentNames.PlayerShell, agentOptions.ShellVersion));
        }

        if (agentOptions.DeviceRole == DeviceRoleNames.ManagerWorkstation &&
            !string.IsNullOrWhiteSpace(agentOptions.OrganizationAdminVersion))
        {
            components.Add(new DeviceComponentVersionDto(UpdateComponentNames.OrganizationAdmin, agentOptions.OrganizationAdminVersion));
        }

        return components;
    }
}
