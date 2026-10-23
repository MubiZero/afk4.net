namespace AFK4.Shared.Contracts.Updates;

/// <summary>
/// Где лежит манифест файлов пакета и чем он подписан. Манифест — дельта-путь рядом с полным MSI:
/// агент сверяет файлы на диске с манифестом и докачивает только отличающиеся. Подпись пакета
/// (<see cref="UpdatePackageSignaturePayload"/>) этим не меняется: старые агенты поле не читают и
/// ставят MSI, как раньше.
/// </summary>
public sealed record UpdateManifestDto(
    string Uri,
    string Sha256,
    long SizeBytes,
    string Signature,
    string SignatureAlgorithm);
