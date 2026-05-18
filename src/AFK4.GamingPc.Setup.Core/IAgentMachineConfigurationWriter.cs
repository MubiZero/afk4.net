using AFK4.Shared.Contracts.Devices;

namespace AFK4.GamingPc.Setup.Core;

public interface IAgentMachineConfigurationWriter
{
    void Write(SetupRequest request, DeviceEnrollmentResponse enrollment);
}
