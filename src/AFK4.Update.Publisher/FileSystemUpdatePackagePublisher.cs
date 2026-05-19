using System.Security.Cryptography;
using System.Text;
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
    private readonly HttpClient httpClient;

    public FileSystemUpdatePackagePublisher(
        EcdsaUpdatePackageSigner? signer = null,
        HttpClient? httpClient = null)
    {
        this.signer = signer ?? new EcdsaUpdatePackageSigner();
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<UpdatePackagePublishResult> PublishAsync(
        UpdatePackagePublishOptions options,
        CancellationToken cancellationToken)
    {
        Validate(options);

        var artifactPath = Path.GetFullPath(options.ArtifactPath);
        var publishedArtifact = await PublishArtifactAsync(options, artifactPath, cancellationToken);
        var payload = UpdatePackageSignaturePayload.Create(
            options.Component,
            options.Version,
            options.Channel,
            publishedArtifact.ArtifactUri,
            publishedArtifact.Sha256,
            publishedArtifact.SizeBytes,
            options.ReleaseNotes);
        var privateKeyPem = await LoadSigningPrivateKeyPemAsync(options, cancellationToken);
        var signature = signer.SignPem(privateKeyPem, payload);
        var request = new CreateUpdatePackageRequest(
            options.OrganizationId,
            options.Component.Trim(),
            options.Version.Trim(),
            options.Channel.Trim(),
            publishedArtifact.ArtifactUri,
            publishedArtifact.Sha256,
            signature,
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            publishedArtifact.SizeBytes,
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

        return new UpdatePackagePublishResult(publishedArtifact.Location, requestJson, request);
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

        if (!IsSupportedArtifactStoreKind(options.ArtifactStoreKind))
        {
            throw new ArgumentException("Unsupported artifact store kind.", nameof(options));
        }

        if (options.ArtifactStoreKind == UpdateArtifactStoreKindNames.FileSystem)
        {
            if (string.IsNullOrWhiteSpace(options.HostingRoot))
            {
                throw new ArgumentException("Hosting root is required.", nameof(options));
            }

            if (options.PublicBaseUri is null)
            {
                throw new ArgumentException("Public base URI is required for file-system artifact publishing.", nameof(options));
            }
        }

        if (options.ArtifactStoreKind == UpdateArtifactStoreKindNames.HttpPut)
        {
            if (options.ArtifactUploadUri is null)
            {
                throw new ArgumentException("Artifact upload URI is required for http-put artifact publishing.", nameof(options));
            }

            if (options.PublicArtifactUri is null)
            {
                throw new ArgumentException("Public artifact URI is required for http-put artifact publishing.", nameof(options));
            }
        }

        if (options.ArtifactStoreKind == UpdateArtifactStoreKindNames.S3)
        {
            if (options.S3Endpoint is null)
            {
                throw new ArgumentException("S3 endpoint URI is required for s3 artifact publishing.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.S3Bucket))
            {
                throw new ArgumentException("S3 bucket is required for s3 artifact publishing.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.S3AccessKeyEnvironmentVariable))
            {
                throw new ArgumentException("S3 access key environment variable is required for s3 artifact publishing.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.S3SecretKeyEnvironmentVariable))
            {
                throw new ArgumentException("S3 secret key environment variable is required for s3 artifact publishing.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.S3Region))
            {
                throw new ArgumentException("S3 region is required for s3 artifact publishing.", nameof(options));
            }

            if (options.PublicBaseUri is null)
            {
                throw new ArgumentException("Public base URI is required for s3 artifact publishing.", nameof(options));
            }
        }

        var hasKeyFile = !string.IsNullOrWhiteSpace(options.SigningPrivateKeyPemPath);
        var hasKeyEnvironmentVariable = !string.IsNullOrWhiteSpace(options.SigningPrivateKeyPemEnvironmentVariable);
        if (hasKeyFile == hasKeyEnvironmentVariable)
        {
            throw new ArgumentException(
                "Specify exactly one signing key source: --signing-key or --signing-key-env-var.",
                nameof(options));
        }
    }

    private async Task<PublishedArtifact> PublishArtifactAsync(
        UpdatePackagePublishOptions options,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        return options.ArtifactStoreKind switch
        {
            UpdateArtifactStoreKindNames.FileSystem => await PublishFileSystemArtifactAsync(options, artifactPath, cancellationToken),
            UpdateArtifactStoreKindNames.HttpPut => await PublishHttpPutArtifactAsync(options, artifactPath, cancellationToken),
            UpdateArtifactStoreKindNames.S3 => await PublishS3ArtifactAsync(options, artifactPath, cancellationToken),
            _ => throw new ArgumentException("Unsupported artifact store kind.", nameof(options))
        };
    }

    private static async Task<PublishedArtifact> PublishFileSystemArtifactAsync(
        UpdatePackagePublishOptions options,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        var hostingRoot = Path.GetFullPath(options.HostingRoot!);
        var publishedFileName = GetPublishedFileName(options, artifactPath);
        var pathSegments = GetArtifactPathSegments(options, publishedFileName);
        var relativeDirectory = Path.Combine(pathSegments[..^1]);
        var targetDirectory = Path.GetFullPath(Path.Combine(hostingRoot, relativeDirectory));
        var targetPath = Path.GetFullPath(Path.Combine(targetDirectory, publishedFileName));

        EnsureInsideRoot(hostingRoot, targetPath);
        Directory.CreateDirectory(targetDirectory);
        File.Copy(artifactPath, targetPath, overwrite: false);

        var sha256 = await ComputeSha256Async(targetPath, cancellationToken);
        var sizeBytes = new FileInfo(targetPath).Length;
        var artifactUri = CreateArtifactUri(options.PublicBaseUri!, pathSegments);
        return new PublishedArtifact(targetPath, artifactUri, sha256, sizeBytes);
    }

    private async Task<PublishedArtifact> PublishHttpPutArtifactAsync(
        UpdatePackagePublishOptions options,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        var sha256 = await ComputeSha256Async(artifactPath, cancellationToken);
        var sizeBytes = new FileInfo(artifactPath).Length;

        await using var artifactStream = File.OpenRead(artifactPath);
        using var request = new HttpRequestMessage(HttpMethod.Put, options.ArtifactUploadUri)
        {
            Content = new StreamContent(artifactStream)
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new PublishedArtifact(
            $"http-put:{options.PublicArtifactUri}",
            options.PublicArtifactUri!.ToString(),
            sha256,
            sizeBytes);
    }

    private async Task<PublishedArtifact> PublishS3ArtifactAsync(
        UpdatePackagePublishOptions options,
        string artifactPath,
        CancellationToken cancellationToken)
    {
        var sha256 = await ComputeSha256Async(artifactPath, cancellationToken);
        var sizeBytes = new FileInfo(artifactPath).Length;
        var publishedFileName = GetPublishedFileName(options, artifactPath);
        var objectKey = CreateObjectKey(options, publishedFileName);
        var objectUri = CreateS3ObjectUri(options.S3Endpoint!, options.S3Bucket!, objectKey);
        var accessKey = ReadEnvironmentVariable(options.S3AccessKeyEnvironmentVariable!, "S3 access key");
        var secretKey = ReadEnvironmentVariable(options.S3SecretKeyEnvironmentVariable!, "S3 secret key");
        var amzDate = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        var dateStamp = amzDate[..8];

        await using var artifactStream = File.OpenRead(artifactPath);
        using var request = new HttpRequestMessage(HttpMethod.Put, objectUri)
        {
            Content = new StreamContent(artifactStream)
        };
        request.Headers.Host = objectUri.Authority;
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", sha256);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.Authorization = CreateS3AuthorizationHeader(
            objectUri,
            accessKey,
            secretKey,
            options.S3Region,
            dateStamp,
            amzDate,
            sha256);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var artifactUri = CreateArtifactUri(options.PublicBaseUri!, GetArtifactPathSegments(options, publishedFileName));
        return new PublishedArtifact(
            $"s3:{options.S3Bucket}/{objectKey}",
            artifactUri,
            sha256,
            sizeBytes);
    }

    private static async Task<string> LoadSigningPrivateKeyPemAsync(
        UpdatePackagePublishOptions options,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.SigningPrivateKeyPemPath))
        {
            if (!File.Exists(options.SigningPrivateKeyPemPath))
            {
                throw new FileNotFoundException(
                    "Update signing private key was not found.",
                    options.SigningPrivateKeyPemPath);
            }

            return await File.ReadAllTextAsync(options.SigningPrivateKeyPemPath, cancellationToken);
        }

        var variableName = options.SigningPrivateKeyPemEnvironmentVariable!;
        var privateKeyPem = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(privateKeyPem)
            ? throw new ArgumentException($"Signing key environment variable '{variableName}' is missing or empty.", nameof(options))
            : privateKeyPem;
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

    private static string CreateArtifactUri(Uri publicBaseUri, IReadOnlyList<string> pathSegments)
    {
        var baseUri = publicBaseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? publicBaseUri
            : new Uri(publicBaseUri.AbsoluteUri + "/");
        var relativeUri = string.Join("/", pathSegments.Select(Uri.EscapeDataString));

        return new Uri(baseUri, relativeUri).ToString();
    }

    private static string[] GetArtifactPathSegments(UpdatePackagePublishOptions options, string fileName)
    {
        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.S3KeyPrefix))
        {
            segments.AddRange(
                options.S3KeyPrefix
                    .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(SanitizePathSegment));
        }

        segments.Add(SanitizePathSegment(options.Component));
        segments.Add(SanitizePathSegment(options.Channel));
        segments.Add(SanitizePathSegment(options.Version));
        segments.Add(fileName);
        return segments.ToArray();
    }

    private static string CreateObjectKey(UpdatePackagePublishOptions options, string fileName)
    {
        return string.Join("/", GetArtifactPathSegments(options, fileName));
    }

    private static Uri CreateS3ObjectUri(Uri endpoint, string bucket, string objectKey)
    {
        var escapedPath = string.Join(
            "/",
            objectKey
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Prepend(bucket)
                .Select(Uri.EscapeDataString));
        var baseUri = endpoint.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? endpoint.AbsoluteUri
            : endpoint.AbsoluteUri + "/";

        return new Uri(baseUri + escapedPath);
    }

    private static string ReadEnvironmentVariable(string variableName, string description)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{description} environment variable '{variableName}' is missing or empty.")
            : value;
    }

    private static System.Net.Http.Headers.AuthenticationHeaderValue CreateS3AuthorizationHeader(
        Uri requestUri,
        string accessKey,
        string secretKey,
        string region,
        string dateStamp,
        string amzDate,
        string payloadHash)
    {
        const string service = "s3";
        const string algorithm = "AWS4-HMAC-SHA256";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
        var canonicalHeaders =
            $"host:{requestUri.Authority}\n" +
            $"x-amz-content-sha256:{payloadHash}\n" +
            $"x-amz-date:{amzDate}\n";
        var canonicalRequest = string.Join(
            "\n",
            "PUT",
            requestUri.AbsolutePath,
            string.Empty,
            canonicalHeaders,
            signedHeaders,
            payloadHash);
        var canonicalRequestHash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest));
        var stringToSign = string.Join(
            "\n",
            algorithm,
            amzDate,
            credentialScope,
            Convert.ToHexString(canonicalRequestHash).ToLowerInvariant());
        var signingKey = DeriveS3SigningKey(secretKey, dateStamp, region, service);
        var signature = Convert.ToHexString(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();
        var parameter = $"Credential={accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
        return new System.Net.Http.Headers.AuthenticationHeaderValue(algorithm, parameter);
    }

    private static byte[] DeriveS3SigningKey(string secretKey, string dateStamp, string region, string service)
    {
        var kSecret = Encoding.UTF8.GetBytes("AWS4" + secretKey);
        var kDate = HMACSHA256.HashData(kSecret, Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(region));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(service));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
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

    private static bool IsSupportedArtifactStoreKind(string artifactStoreKind)
    {
        return artifactStoreKind is
            UpdateArtifactStoreKindNames.FileSystem or
            UpdateArtifactStoreKindNames.HttpPut or
            UpdateArtifactStoreKindNames.S3;
    }

    private sealed record PublishedArtifact(
        string Location,
        string ArtifactUri,
        string Sha256,
        long SizeBytes);
}
