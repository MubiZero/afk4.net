using System.Security.Cryptography;
using System.Text.Json;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Update.Publisher;

public sealed class FileSystemUpdatePackagePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly EcdsaUpdatePackageSigner signer;

    public FileSystemUpdatePackagePublisher()
        : this(new EcdsaUpdatePackageSigner())
    {
    }

    public FileSystemUpdatePackagePublisher(EcdsaUpdatePackageSigner signer)
    {
        this.signer = signer;
    }

    public async Task<UpdatePackagePublishResult> PublishAsync(
        UpdatePackagePublishOptions options,
        CancellationToken cancellationToken)
    {
        Validate(options);

        var artifactPath = Path.GetFullPath(options.ArtifactPath);
        var hostingRoot = Path.GetFullPath(options.HostingRoot);
        var publishedFileName = GetPublishedFileName(options, artifactPath);
        var relativeDirectory = Path.Combine(
            SanitizePathSegment(options.Component),
            SanitizePathSegment(options.Channel),
            SanitizePathSegment(options.Version));
        var targetDirectory = Path.GetFullPath(Path.Combine(hostingRoot, relativeDirectory));
        var targetPath = Path.GetFullPath(Path.Combine(targetDirectory, publishedFileName));

        EnsureInsideRoot(hostingRoot, targetPath);
        Directory.CreateDirectory(targetDirectory);
        File.Copy(artifactPath, targetPath, overwrite: false);

        var sha256 = await ComputeSha256Async(targetPath, cancellationToken);
        var sizeBytes = new FileInfo(targetPath).Length;
        var artifactUri = CreateArtifactUri(options.PublicBaseUri, relativeDirectory, publishedFileName);
        var payload = UpdatePackageSignaturePayload.Create(
            options.Component,
            options.Version,
            options.Channel,
            artifactUri,
            sha256,
            sizeBytes,
            options.ReleaseNotes);
        var signature = await signer.SignAsync(options.SigningPrivateKeyPemPath, payload, cancellationToken);
        var request = new CreateUpdatePackageRequest(
            options.OrganizationId,
            options.Component.Trim(),
            options.Version.Trim(),
            options.Channel.Trim(),
            artifactUri,
            sha256,
            signature,
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            sizeBytes,
            options.ReleaseNotes.Trim());
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        if (!string.IsNullOrWhiteSpace(options.RequestJsonOutputPath))
        {
            var outputPath = Path.GetFullPath(options.RequestJsonOutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            await File.WriteAllTextAsync(outputPath, requestJson, cancellationToken);
        }

        return new UpdatePackagePublishResult(targetPath, requestJson, request);
    }

    private static void Validate(UpdatePackagePublishOptions options)
    {
        if (options.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("Organization id is required.", nameof(options));
        }

        if (!IsSupportedComponent(options.Component))
        {
            throw new ArgumentException("Unsupported update component.", nameof(options));
        }

        if (!IsSupportedChannel(options.Channel))
        {
            throw new ArgumentException("Unsupported update channel.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            throw new ArgumentException("Package version is required.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.ReleaseNotes))
        {
            throw new ArgumentException("Release notes are required.", nameof(options));
        }

        if (!File.Exists(options.ArtifactPath))
        {
            throw new FileNotFoundException("Update artifact was not found.", options.ArtifactPath);
        }

        if (string.IsNullOrWhiteSpace(options.HostingRoot))
        {
            throw new ArgumentException("Hosting root is required.", nameof(options));
        }
    }

    private static string GetPublishedFileName(UpdatePackagePublishOptions options, string artifactPath)
    {
        var fileName = string.IsNullOrWhiteSpace(options.PublishedFileName)
            ? Path.GetFileName(artifactPath)
            : Path.GetFileName(options.PublishedFileName);

        return string.IsNullOrWhiteSpace(fileName)
            ? throw new ArgumentException("Published file name is required.", nameof(options))
            : fileName;
    }

    private static void EnsureInsideRoot(string hostingRoot, string targetPath)
    {
        var rootWithSeparator = hostingRoot.EndsWith(Path.DirectorySeparatorChar)
            ? hostingRoot
            : hostingRoot + Path.DirectorySeparatorChar;

        if (!targetPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Published artifact path must stay inside the hosting root.");
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string CreateArtifactUri(Uri publicBaseUri, string relativeDirectory, string fileName)
    {
        var baseUri = publicBaseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? publicBaseUri
            : new Uri(publicBaseUri.AbsoluteUri + "/");
        var relativeUri = string.Join(
            "/",
            relativeDirectory.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString).Append(Uri.EscapeDataString(fileName)));

        return new Uri(baseUri, relativeUri).ToString();
    }

    private static string SanitizePathSegment(string value)
    {
        var chars = value.Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '-')
            .ToArray();

        return chars.Length == 0
            ? throw new ArgumentException("Path segment cannot be empty.")
            : new string(chars);
    }

    private static bool IsSupportedComponent(string component)
    {
        return component is
            UpdateComponentNames.OperatorApp or
            UpdateComponentNames.AgentService or
            UpdateComponentNames.PlayerShell;
    }

    private static bool IsSupportedChannel(string channel)
    {
        return channel is
            UpdateChannelNames.Stable or
            UpdateChannelNames.Beta or
            UpdateChannelNames.Internal;
    }
}
