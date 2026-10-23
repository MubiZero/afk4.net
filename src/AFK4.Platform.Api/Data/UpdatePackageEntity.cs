namespace AFK4.Platform.Api.Data;

public sealed class UpdatePackageEntity
{
    public Guid UpdatePackageId { get; set; }

    public string Component { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string ArtifactUri { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string SignatureAlgorithm { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string State { get; set; } = string.Empty;

    public string ReleaseNotes { get; set; } = string.Empty;

    public Guid CreatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid? ValidatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset? ValidatedAtUtc { get; set; }

    public DateTimeOffset? RetiredAtUtc { get; set; }

    // Манифест файлов для дельта-обновления (необязательный: пакет без него ставится целым MSI).
    public string? ManifestUri { get; set; }

    public string? ManifestSha256 { get; set; }

    public long? ManifestSizeBytes { get; set; }

    public string? ManifestSignature { get; set; }

    public string? ManifestSignatureAlgorithm { get; set; }
}
