using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Update.Publisher;

/// <summary>Папка установки, из которой собирается манифест: имя корня (agent-service, setup-wizard…) и каталог публикации.</summary>
public sealed record UpdateManifestRoot(string Name, string Directory);

/// <summary>Тело файла, готовое к загрузке: сжатая копия во временной папке и её хеш (его подписывает запрос S3).</summary>
public sealed record UpdateManifestBlob(string Sha256, string CompressedPath, string CompressedSha256, long CompressedSizeBytes);

public sealed record UpdateManifestBuild(
    IReadOnlyList<UpdateManifestFileEntry> Files,
    IReadOnlyList<UpdateManifestBlob> Blobs);

/// <summary>
/// Обходит папки публикации и готовит манифест: хеш каждого файла и его сжатое тело. Одинаковые
/// по содержимому файлы (рантайм .NET лежит в нескольких программах) получают одно тело.
/// </summary>
public static class UpdateManifestBuilder
{
    // Качество 9, а не 11: на ~330 МБ первой публикации разница в размере мала, а время втрое меньше.
    private const int BrotliQuality = 9;
    private const int BrotliWindow = 22;

    public static async Task<UpdateManifestBuild> BuildAsync(
        IReadOnlyList<UpdateManifestRoot> roots,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(workDirectory);
        var files = new List<UpdateManifestFileEntry>();
        var blobs = new Dictionary<string, UpdateManifestBlob>(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            var rootDirectory = Path.GetFullPath(root.Directory);
            if (!Directory.Exists(rootDirectory))
            {
                throw new DirectoryNotFoundException($"Manifest root '{root.Name}' directory was not found: {rootDirectory}");
            }

            var paths = Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories)
                .Select(path => (Path: path, Relative: Path.GetRelativePath(rootDirectory, path).Replace('\\', '/')))
                .OrderBy(entry => entry.Relative, StringComparer.Ordinal);

            foreach (var (path, relative) in paths)
            {
                var sha256 = await ComputeSha256Async(path, cancellationToken);
                var size = new FileInfo(path).Length;
                if (!blobs.TryGetValue(sha256, out var blob))
                {
                    blob = await CompressAsync(path, sha256, workDirectory, cancellationToken);
                    blobs[sha256] = blob;
                }

                files.Add(new UpdateManifestFileEntry(root.Name, relative, sha256, size, blob.CompressedSizeBytes));
            }
        }

        return new UpdateManifestBuild(files, blobs.Values.OrderBy(blob => blob.Sha256, StringComparer.Ordinal).ToList());
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static async Task<UpdateManifestBlob> CompressAsync(
        string sourcePath,
        string sha256,
        string workDirectory,
        CancellationToken cancellationToken)
    {
        var compressedPath = Path.Combine(workDirectory, $"{sha256}.{UpdateManifestLayout.BlobExtension}");
        await using (var input = File.OpenRead(sourcePath))
        await using (var output = File.Create(compressedPath))
        {
            await CompressBrotliAsync(input, output, cancellationToken);
        }

        var compressedSha256 = await ComputeSha256Async(compressedPath, cancellationToken);
        return new UpdateManifestBlob(sha256, compressedPath, compressedSha256, new FileInfo(compressedPath).Length);
    }

    private static async Task CompressBrotliAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var encoder = new BrotliEncoder(BrotliQuality, BrotliWindow);
        var inputBuffer = ArrayPool<byte>.Shared.Rent(1 << 20);
        var outputBuffer = ArrayPool<byte>.Shared.Rent(1 << 20);
        try
        {
            int read;
            while ((read = await input.ReadAsync(inputBuffer.AsMemory(), cancellationToken)) > 0)
            {
                var offset = 0;
                while (offset < read)
                {
                    encoder.Compress(inputBuffer.AsSpan(offset, read - offset), outputBuffer, out var consumed, out var written, isFinalBlock: false);
                    offset += consumed;
                    if (written > 0)
                    {
                        await output.WriteAsync(outputBuffer.AsMemory(0, written), cancellationToken);
                    }
                }
            }

            System.Buffers.OperationStatus status;
            do
            {
                status = encoder.Compress(ReadOnlySpan<byte>.Empty, outputBuffer, out _, out var written, isFinalBlock: true);
                if (written > 0)
                {
                    await output.WriteAsync(outputBuffer.AsMemory(0, written), cancellationToken);
                }
            }
            while (status == System.Buffers.OperationStatus.DestinationTooSmall);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(inputBuffer);
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }
    }
}
