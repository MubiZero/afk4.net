using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public interface IAgentRestartScheduler
{
    Task<UpdateRestartResult> ScheduleRestartAsync(
        ComponentUpdateInstructionDto instruction,
        UpdateInstallState state,
        CancellationToken cancellationToken);
}
