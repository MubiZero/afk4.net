using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed record UpdateCheckResult(
    DateTimeOffset ServerTimeUtc,
    IReadOnlyList<ComponentUpdateInstructionDto> Updates)
{
    public bool HasUpdates => Updates.Count > 0;
}
