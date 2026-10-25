using System.Security.Cryptography;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates.Delta;

/// <summary>Файл, которого на диске нет или который отличается от манифеста: его надо докачать.</summary>
public sealed record UpdateDeltaFile(
    string Root,
    string Path,
    string Sha256,
    long SizeBytes,
    long CompressedSizeBytes,
    string TargetPath);

public sealed record UpdateDeltaPlan(
    UpdateManifestDocument Manifest,
    IReadOnlyList<UpdateDeltaFile> ToFetch,
    int UnchangedCount,
    long DownloadBytes,
    long ChangedBytes)
{
    /// <summary>Сколько разных тел надо скачать: одинаковые файлы разных программ делят одно тело.</summary>
    public int UniqueBlobCount => ToFetch.Select(file => file.Sha256).Distinct(StringComparer.Ordinal).Count();

    /// <summary>
    /// Дельта оправдана, когда качать надо заметно меньше целого пакета. Ничего качать не надо — тоже
    /// не дельта: версия не сдвинется (файлы те же), и целый MSI поставит её как раньше.
    /// </summary>
    public bool IsWorthApplying(long fullPackageSizeBytes, double maxDownloadRatio) =>
        ToFetch.Count > 0 && DownloadBytes <= fullPackageSizeBytes * maxDownloadRatio;
}

/// <summary>Сверяет файлы на диске с манифестом (по SHA-256) и считает, что нужно докачать. Ничего не пишет и не удаляет.</summary>
public static class UpdateDeltaPlanner
{
    /// <returns>План или null, если какой-то корень манифеста на этой машине не установлен.</returns>
    public static async Task<UpdateDeltaPlan?> PlanAsync(
        UpdateManifestDocument manifest,
        IUpdateRootResolver rootResolver,
        CancellationToken cancellationToken)
    {
        var roots = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in manifest.Files.Select(file => file.Root).Distinct(StringComparer.Ordinal))
        {
            var directory = rootResolver.Resolve(root);
            if (directory is null)
            {
                return null;
            }

            roots[root] = directory;
        }

        var toFetch = new List<UpdateDeltaFile>();
        var unchanged = 0;
        foreach (var file in manifest.Files)
        {
            if (!UpdateRelativePath.TryResolve(roots[file.Root], file.Path, out var target))
            {
                return null;
            }

            if (await MatchesAsync(target, file, cancellationToken))
            {
                unchanged++;
                continue;
            }

            toFetch.Add(new UpdateDeltaFile(file.Root, file.Path, file.Sha256, file.SizeBytes, file.CompressedSizeBytes, target));
        }

        var downloadBytes = toFetch
            .GroupBy(file => file.Sha256, StringComparer.Ordinal)
            .Sum(group => group.First().CompressedSizeBytes);
        return new UpdateDeltaPlan(manifest, toFetch, unchanged, downloadBytes, toFetch.Sum(file => file.SizeBytes));
    }

    private static async Task<bool> MatchesAsync(string path, UpdateManifestFileEntry file, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length != file.SizeBytes)
            {
                return false;
            }

            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 81920, useAsync: true);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            return string.Equals(hash, file.Sha256, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Файл не прочитать (занят или закрыт ACL): считаем отличающимся — он будет заменён при подмене.
            return false;
        }
    }
}
