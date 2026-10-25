using System.Text;

namespace AFK4.Shared.Contracts.Updates;

public static class UpdateManifestSignaturePayload
{
    public static byte[] Create(
        string component,
        string version,
        string channel,
        string manifestUri,
        string manifestSha256,
        long manifestSizeBytes)
    {
        var canonical = string.Join(
            "\n",
            "AFK4-UPDATE-MANIFEST-V1",
            $"component={component.Trim()}",
            $"version={version.Trim()}",
            $"channel={channel.Trim()}",
            $"manifest-uri={manifestUri.Trim()}",
            $"manifest-sha256={manifestSha256.Trim().ToLowerInvariant()}",
            $"manifest-size-bytes={manifestSizeBytes}",
            string.Empty);

        return Encoding.UTF8.GetBytes(canonical);
    }
}
