using System.IO.Compression;
using System.Security.Cryptography;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates.Delta;

public sealed record StagedUpdateFile(string Root, string Path, string StagedPath, string TargetPath, string Sha256);

public sealed record StagedUpdate(string StageDirectory, IReadOnlyList<StagedUpdateFile> Files);

/// <summary>
/// Докачивает недостающие тела файлов в папку подготовки: каждое сжатое тело (Brotli) разворачивается
/// и проверяется по SHA-256 и размеру из подписанного манифеста. Файлы в папку установки не попадают —
/// это делает отдельный шаг подмены, когда всё скачано и проверено.
/// </summary>
public sealed class UpdateBlobFetcher(IHttpClientFactory httpClientFactory, IOptions<AgentOptions> options)
{
    private const int MaxParallelDownloads = 4;

    public async Task<StagedUpdate> FetchAsync(
        UpdateDeltaPlan plan,
        string stageDirectory,
        CancellationToken cancellationToken)
    {
        var blobDirectory = Path.Combine(stageDirectory, "blobs");
        Directory.CreateDirectory(blobDirectory);
        var blobBase = new Uri(plan.Manifest.BlobBaseUri, UriKind.Absolute);

        var byHash = plan.ToFetch
            .GroupBy(file => file.Sha256.ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        await Parallel.ForEachAsync(
            byHash.Values,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken },
            async (file, token) =>
            {
                var staged = Path.Combine(blobDirectory, file.Sha256.ToLowerInvariant());
                if (await IsValidAsync(staged, file, token))
                {
                    return;
                }

                var blobUri = new Uri(blobBase, UpdateManifestLayout.BlobObjectKey(file.Sha256));
                await DownloadAsync(blobUri, staged, file, token);
            });

        var files = plan.ToFetch
            .Select(file => new StagedUpdateFile(
                file.Root,
                file.Path,
                Path.Combine(blobDirectory, file.Sha256.ToLowerInvariant()),
                file.TargetPath,
                file.Sha256.ToLowerInvariant()))
            .ToList();
        return new StagedUpdate(stageDirectory, files);
    }

    private async Task DownloadAsync(Uri blobUri, string stagedPath, UpdateDeltaFile file, CancellationToken cancellationToken)
    {
        var temp = $"{stagedPath}.{Guid.NewGuid():N}.tmp";
        var stallTimeout = options.Value.UpdateDownloadStallTimeout;
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(stallTimeout);

        try
        {
            using var response = await httpClientFactory.CreateClient("updates")
                .GetAsync(blobUri, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            response.EnsureSuccessStatusCode();

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long written = 0;
            using (var compressed = await response.Content.ReadAsStreamAsync(stall.Token))
            using (var brotli = new BrotliStream(compressed, CompressionMode.Decompress))
            using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await brotli.ReadAsync(buffer, stall.Token)) > 0)
                {
                    written += read;
                    if (written > file.SizeBytes)
                    {
                        // Тело распаковывается в больше, чем обещал подписанный манифест: бомба или подмена.
                        throw new InvalidDataException($"Blob for '{file.Path}' is larger than its manifest size.");
                    }

                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    stall.CancelAfter(stallTimeout);
                }
            }

            if (written != file.SizeBytes
                || !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Blob for '{file.Path}' does not match its manifest hash.");
            }

            File.Move(temp, stagedPath, overwrite: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Update file download was silent for {stallTimeout}.");
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static async Task<bool> IsValidAsync(string stagedPath, UpdateDeltaFile file, CancellationToken cancellationToken)
    {
        if (!File.Exists(stagedPath) || new FileInfo(stagedPath).Length != file.SizeBytes)
        {
            return false;
        }

        using var stream = File.OpenRead(stagedPath);
        return string.Equals(
            Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)),
            file.Sha256,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
