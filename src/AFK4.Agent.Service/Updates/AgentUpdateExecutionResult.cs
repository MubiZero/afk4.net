namespace AFK4.Agent.Service.Updates;

public sealed record AgentUpdateExecutionResult(
    int OfferedCount,
    int AppliedCount,
    int FailedCount);
