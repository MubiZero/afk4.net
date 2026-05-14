namespace AFK4.Update.Publisher;

public sealed record UpdatePackagePublishOptions(
    Guid OrganizationId,
    string Component,
    string Version,
    string Channel,
    string ArtifactPath,
    string? HostingRoot,
    Uri? PublicBaseUri,
    string? SigningPrivateKeyPemPath,
    string ReleaseNotes,
    string? PublishedFileName = null,
    string? RequestJsonOutputPath = null,
    string ArtifactStoreKind = UpdateArtifactStoreKindNames.FileSystem,
    Uri? ArtifactUploadUri = null,
    Uri? PublicArtifactUri = null,
    string? SigningPrivateKeyPemEnvironmentVariable = null);
