using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates.Delta;

/// <summary>Скачивает манифест файлов пакета и отдаёт его только после полной проверки (хеш, подпись, содержимое).</summary>
public sealed class UpdateManifestClient(IHttpClientFactory httpClientFactory, IOptions<AgentOptions> options)
{
    // Манифест — несколько сотен КБ (650 файлов ≈ 120 КБ). Потолок защищает от подсунутого гигантского файла.
    private const int MaxManifestBytes = 8 * 1024 * 1024;

    public async Task<UpdateManifestVerification> FetchAsync(
        ComponentUpdateInstructionDto instruction,
        CancellationToken cancellationToken)
    {
        if (instruction.Manifest is not { } manifest)
        {
            return UpdateManifestVerification.Invalid("The package has no file manifest.");
        }

        if (!Uri.TryCreate(manifest.Uri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return UpdateManifestVerification.Invalid("Update manifest URI must be absolute HTTPS.");
        }

        if (manifest.SizeBytes <= 0 || manifest.SizeBytes > MaxManifestBytes)
        {
            return UpdateManifestVerification.Invalid("Update manifest size is out of range.");
        }

        var stallTimeout = options.Value.UpdateDownloadStallTimeout;
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(stallTimeout);

        try
        {
            using var response = await httpClientFactory.CreateClient("updates")
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            response.EnsureSuccessStatusCode();

            using var source = await response.Content.ReadAsStreamAsync(stall.Token);
            using var buffer = new MemoryStream((int)manifest.SizeBytes);
            var chunk = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(chunk, stall.Token)) > 0)
            {
                if (buffer.Length + read > MaxManifestBytes)
                {
                    return UpdateManifestVerification.Invalid("Update manifest is larger than allowed.");
                }

                buffer.Write(chunk, 0, read);
                stall.CancelAfter(stallTimeout);
            }

            return UpdateManifestVerifier.Verify(
                instruction, manifest, buffer.ToArray(), options.Value.UpdatePackageSigningPublicKeyPem);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Update manifest download was silent for {stallTimeout}.");
        }
    }
}
