using System.Security.Cryptography;
using System.Text;

namespace AFK4.Update.Publisher;

public static class UpdatePackageSignaturePayload
{
    public static byte[] Create(
        string component,
        string version,
        string channel,
        string artifactUri,
        string sha256,
        long sizeBytes,
        string releaseNotes)
    {
        var releaseNotesHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(releaseNotes.Trim())))
            .ToLowerInvariant();
        var canonical = string.Join(
            "\n",
            "AFK4-UPDATE-PACKAGE-V1",
            $"component={component.Trim()}",
            $"version={version.Trim()}",
            $"channel={channel.Trim()}",
            $"artifact-uri={artifactUri.Trim()}",
            $"sha256={sha256.Trim().ToLowerInvariant()}",
            $"size-bytes={sizeBytes}",
            $"release-notes-sha256={releaseNotesHash}",
            string.Empty);

        return Encoding.UTF8.GetBytes(canonical);
    }
}
