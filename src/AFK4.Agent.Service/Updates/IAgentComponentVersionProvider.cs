using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public interface IAgentComponentVersionProvider
{
    IReadOnlyList<DeviceComponentVersionDto> GetInstalledComponents();
}
