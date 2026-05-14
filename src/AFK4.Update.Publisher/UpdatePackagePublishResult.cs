using AFK4.Shared.Contracts.Updates;

namespace AFK4.Update.Publisher;

public sealed record UpdatePackagePublishResult(
    string PublishedArtifactPath,
    string RequestJson,
    CreateUpdatePackageRequest Request);
