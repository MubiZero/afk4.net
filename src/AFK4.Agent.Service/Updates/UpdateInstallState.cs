using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed record UpdateInstallState(
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    string Component,
    string InstalledVersion,
    string TargetVersion,
    string ArtifactPath,
    string Status,
    string Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static UpdateInstallState Create(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        string status,
        string message,
        DateTimeOffset observedAtUtc)
    {
        return new UpdateInstallState(
            instruction.UpdateRolloutId,
            instruction.UpdatePackageId,
            instruction.Component,
            InstalledVersion: "unknown",
            TargetVersion: instruction.Version,
            artifact.FilePath,
            status,
            message,
            observedAtUtc,
            observedAtUtc);
    }

    public UpdateInstallState WithStatus(string status, string message, DateTimeOffset observedAtUtc)
    {
        return this with
        {
            Status = status,
            Message = message,
            UpdatedAtUtc = observedAtUtc
        };
    }

    public UpdateInstallState WithInstalledVersion(string installedVersion)
    {
        return this with
        {
            InstalledVersion = installedVersion
        };
    }
}
