using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed record DownloadedUpdateArtifact(
    ComponentUpdateInstructionDto Instruction,
    string FilePath,
    long SizeBytes);
