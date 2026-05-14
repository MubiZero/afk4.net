namespace AFK4.Shared.Contracts.Updates;

public sealed record ComponentUpdateInstructionDto(
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    string Component,
    string Version,
    string Channel,
    string ArtifactUri,
    string Sha256,
    string Signature,
    string SignatureAlgorithm,
    long SizeBytes,
    string ReleaseNotes);
