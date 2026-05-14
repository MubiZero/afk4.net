namespace AFK4.Shared.Contracts.Updates;

public sealed record CreateUpdatePackageRequest(
    Guid OrganizationId,
    string Component,
    string Version,
    string Channel,
    string ArtifactUri,
    string Sha256,
    string Signature,
    string SignatureAlgorithm,
    long SizeBytes,
    string ReleaseNotes);
