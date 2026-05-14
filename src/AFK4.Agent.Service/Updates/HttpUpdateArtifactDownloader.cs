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

        var client = httpClientFactory.CreateClient("updates");
        using var response = await client.GetAsync(
            artifactUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return new DownloadedUpdateArtifact(
            instruction,
            filePath,
            new FileInfo(filePath).Length);
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
}
