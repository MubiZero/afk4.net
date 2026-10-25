namespace AFK4.Shared.Contracts.Updates;

/// <summary>
/// Содержимое файла манифеста: полный список файлов компонента этой версии. Файл лежит в хранилище
/// пакетов, а не едет через API; подписан он через <see cref="UpdateManifestDto"/>.
/// </summary>
public sealed record UpdateManifestDocument(
    int Schema,
    string Component,
    string Version,
    string Channel,
    string BlobBaseUri,
    IReadOnlyList<UpdateManifestFileEntry> Files);

/// <summary>
/// Один файл версии. <c>Root</c> — какая папка установки (agent-service, setup-wizard,
/// player-shell, organization-admin), <c>Path</c> — путь внутри неё с «/». Тело файла лежит в
/// хранилище по <see cref="UpdateManifestLayout.BlobObjectKey"/> в сжатом виде.
/// </summary>
public sealed record UpdateManifestFileEntry(
    string Root,
    string Path,
    string Sha256,
    long SizeBytes,
    long CompressedSizeBytes);

public static class UpdateManifestLayout
{
    public const int CurrentSchema = 1;

    /// <summary>Тела файлов сжаты Brotli: он есть в .NET без сторонних пакетов.</summary>
    public const string BlobExtension = "br";

    /// <summary>Ключ тела файла в хранилище: адрес по содержимому, один и тот же для любых версий и компонентов.</summary>
    public static string BlobObjectKey(string sha256)
    {
        var hash = sha256.Trim().ToLowerInvariant();
        return $"blobs/{hash[..2]}/{hash}.{BlobExtension}";
    }
}
