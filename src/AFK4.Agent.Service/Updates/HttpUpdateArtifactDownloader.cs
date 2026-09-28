using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class HttpUpdateArtifactDownloader(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : IUpdateArtifactDownloader
{
    public async Task<DownloadedUpdateArtifact> DownloadAsync(
        ComponentUpdateInstructionDto instruction,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(instruction.ArtifactUri, UriKind.Absolute, out var artifactUri))
        {
            throw new InvalidOperationException("Update artifact URI must be absolute.");
        }

        Directory.CreateDirectory(options.Value.UpdateStagingDirectory);
        var filePath = Path.Combine(
            options.Value.UpdateStagingDirectory,
            CreateArtifactFileName(instruction, artifactUri));
        if (File.Exists(filePath))
        {
            var existingLength = new FileInfo(filePath).Length;
            if (instruction.SizeBytes > 0 && existingLength == instruction.SizeBytes)
            {
                return new DownloadedUpdateArtifact(instruction, filePath, existingLength);
            }

            File.Delete(filePath);
        }

        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        // Предел — на тишину, а не на всю загрузку: медленная, но живая сеть клуба докачает пакет,
        // а соединение, которое замолчало, больше не вешает цикл обновлений до перезапуска службы.
        var stallTimeout = options.Value.UpdateDownloadStallTimeout;
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(stallTimeout);

        try
        {
            var client = httpClientFactory.CreateClient("updates");
            using var response = await client.GetAsync(
                artifactUri,
                HttpCompletionOption.ResponseHeadersRead,
                stall.Token);
            response.EnsureSuccessStatusCode();

            await using (var source = await response.Content.ReadAsStreamAsync(stall.Token))
            await using (var target = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, stall.Token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    stall.CancelAfter(stallTimeout);
                }

                await target.FlushAsync(cancellationToken);
            }

            var downloadedLength = new FileInfo(tempPath).Length;
            if (instruction.SizeBytes > 0 && downloadedLength != instruction.SizeBytes)
            {
                throw new IOException("Downloaded update artifact size does not match package metadata.");
            }

            File.Move(tempPath, filePath, overwrite: true);

            return new DownloadedUpdateArtifact(
                instruction,
                filePath,
                downloadedLength);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Повторится на следующей проверке: TimeoutException координатор считает временным сбоем.
            throw new TimeoutException($"Update download was silent for {stallTimeout}.");
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static string CreateArtifactFileName(ComponentUpdateInstructionDto instruction, Uri artifactUri)
    {
        var extension = Path.GetExtension(artifactUri.LocalPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".pkg";
        }

        return $"{Sanitize(instruction.Component)}-{Sanitize(instruction.Version)}-{instruction.UpdatePackageId:N}{extension}";
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Trim()
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray();

        return new string(chars);
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
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
