namespace AFK4.Shared.Contracts.Updates;

public sealed record UpdatePackageDto(
    Guid UpdatePackageId,
    Guid OrganizationId,
    Guid BranchId,
    string Component,
    string Version,
    string Channel,
    string ArtifactUri,
    string Sha256,
    string Signature,
    string SignatureAlgorithm,
    long SizeBytes,
    string State,
    string ReleaseNotes,
    DateTimeOffset CreatedAtUtc);
