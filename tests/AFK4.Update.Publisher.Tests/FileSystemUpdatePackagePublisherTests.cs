using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Update.Publisher.Tests;

public sealed class FileSystemUpdatePackagePublisherTests : IDisposable
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-update-publisher-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PublishAsync_CopiesArtifactAndCreatesSignedPackageRequest()
    {
        Directory.CreateDirectory(tempRoot);
        var artifactPath = Path.Combine(tempRoot, "agent.msi");
        var artifactBytes = new byte[] { 0x41, 0x46, 0x4b, 0x34 };
        await File.WriteAllBytesAsync(artifactPath, artifactBytes);
        var privateKeyPath = CreatePrivateKeyPem(out var publicKeyPem);
        var hostingRoot = Path.Combine(tempRoot, "hosted");
        var outputPath = Path.Combine(tempRoot, "request.json");
        var publisher = new FileSystemUpdatePackagePublisher();

        var result = await publisher.PublishAsync(
            new UpdatePackagePublishOptions(
                OrganizationId,
                UpdateComponentNames.AgentService,
                "1.2.3",
                UpdateChannelNames.Internal,
                artifactPath,
                hostingRoot,
                new Uri("https://updates.afk4.test/packages/"),
                privateKeyPath,
                "Internal Agent update.",
                RequestJsonOutputPath: outputPath),
            CancellationToken.None);

        var expectedArtifactPath = Path.Combine(hostingRoot, "agent-service", "internal", "1.2.3", "agent.msi");
        Assert.Equal(expectedArtifactPath, result.PublishedArtifactPath);
        Assert.True(File.Exists(expectedArtifactPath));
        Assert.Equal(artifactBytes, await File.ReadAllBytesAsync(expectedArtifactPath));
        Assert.Equal("https://updates.afk4.test/packages/agent-service/internal/1.2.3/agent.msi", result.Request.ArtifactUri);
        Assert.Equal("1.2.3", result.Request.Version);
        Assert.Equal(artifactBytes.Length, result.Request.SizeBytes);
        Assert.Equal("021cfd436a0a2cfa7bd8362a4c2daac58fd9fd6b5b0c985d825fb56f1e9fa21a", result.Request.Sha256);
        Assert.Equal(UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363, result.Request.SignatureAlgorithm);
        Assert.True(File.Exists(outputPath));

        var requestFromFile = JsonSerializer.Deserialize<CreateUpdatePackageRequest>(
            await File.ReadAllTextAsync(outputPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(requestFromFile);
        Assert.Equal(result.Request.Sha256, requestFromFile.Sha256);
        Assert.True(VerifySignature(publicKeyPem, result.Request));
    }

    [Fact]
    public async Task PublishAsync_RejectsUnsupportedChannelBeforeCopyingArtifact()
    {
        Directory.CreateDirectory(tempRoot);
        var artifactPath = Path.Combine(tempRoot, "agent.msi");
        await File.WriteAllTextAsync(artifactPath, "artifact");
        var privateKeyPath = CreatePrivateKeyPem(out _);
        var hostingRoot = Path.Combine(tempRoot, "hosted");
        var publisher = new FileSystemUpdatePackagePublisher();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(
            new UpdatePackagePublishOptions(
                OrganizationId,
                UpdateComponentNames.AgentService,
                "1.2.3",
                "nightly",
                artifactPath,
                hostingRoot,
                new Uri("https://updates.afk4.test/packages/"),
                privateKeyPath,
                "Nightly update."),
            CancellationToken.None));

        Assert.Equal("Unsupported update channel. (Parameter 'options')", exception.Message);
        Assert.False(Directory.Exists(hostingRoot));
    }

    [Fact]
    public async Task PublishAsync_WithHttpPutArtifactStore_UploadsArtifactAndUsesPublicArtifactUri()
    {
        Directory.CreateDirectory(tempRoot);
        var artifactPath = Path.Combine(tempRoot, "agent.zip");
        var artifactBytes = new byte[] { 0x10, 0x20, 0x30 };
        await File.WriteAllBytesAsync(artifactPath, artifactBytes);
        var privateKeyPath = CreatePrivateKeyPem(out _);
        var handler = new RecordingHttpMessageHandler();
        var publisher = new FileSystemUpdatePackagePublisher(httpClient: new HttpClient(handler));

        var result = await publisher.PublishAsync(
            new UpdatePackagePublishOptions(
                OrganizationId,
                UpdateComponentNames.AgentService,
                "1.2.4",
                UpdateChannelNames.Stable,
                artifactPath,
                HostingRoot: string.Empty,
                PublicBaseUri: null,
                privateKeyPath,
                "Stable Agent update.",
                ArtifactStoreKind: UpdateArtifactStoreKindNames.HttpPut,
                ArtifactUploadUri: new Uri("https://storage.example.test/upload-token"),
                PublicArtifactUri: new Uri("https://cdn.example.test/agent-service/stable/1.2.4/agent.zip")),
            CancellationToken.None);

        Assert.Equal("https://cdn.example.test/agent-service/stable/1.2.4/agent.zip", result.Request.ArtifactUri);
        Assert.Equal("https://storage.example.test/upload-token", handler.LastRequestUri?.ToString());
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal(artifactBytes, handler.LastBody);
        Assert.Equal("http-put:https://cdn.example.test/agent-service/stable/1.2.4/agent.zip", result.PublishedArtifactPath);
    }

    [Fact]
    public async Task PublishAsync_WithEnvironmentSigningKey_DoesNotRequireKeyFile()
    {
        Directory.CreateDirectory(tempRoot);
        var artifactPath = Path.Combine(tempRoot, "shell.zip");
        await File.WriteAllTextAsync(artifactPath, "shell");
        _ = CreatePrivateKeyPem(out _);
        var privateKeyPem = await File.ReadAllTextAsync(Path.Combine(tempRoot, "update-signing-key.pem"));
        const string envVarName = "AFK4_TEST_UPDATE_SIGNING_KEY_PEM";
        Environment.SetEnvironmentVariable(envVarName, privateKeyPem);
        try
        {
            var publisher = new FileSystemUpdatePackagePublisher();

            var result = await publisher.PublishAsync(
                new UpdatePackagePublishOptions(
                    OrganizationId,
                    UpdateComponentNames.PlayerShell,
                    "2.0.0",
                    UpdateChannelNames.Internal,
                    artifactPath,
                    Path.Combine(tempRoot, "hosted"),
                    new Uri("https://updates.afk4.test/packages/"),
                    SigningPrivateKeyPemPath: null,
                    "Internal Shell update.",
                    SigningPrivateKeyPemEnvironmentVariable: envVarName),
                CancellationToken.None);

            Assert.Equal(UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363, result.Request.SignatureAlgorithm);
            Assert.False(string.IsNullOrWhiteSpace(result.Request.Signature));
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void Parse_ReadsRequiredCommandLineArguments()
    {
        var options = UpdatePackagePublishCommand.Parse(
        [
            "--organization-id", OrganizationId.ToString("D"),
            "--component", UpdateComponentNames.PlayerShell,
            "--version", "2.0.0",
            "--channel", UpdateChannelNames.Beta,
            "--artifact", "shell.msix",
            "--hosting-root", "C:\\updates",
            "--public-base-uri", "https://updates.afk4.test/packages/",
            "--signing-key", "key.pem",
            "--release-notes", "Beta shell update.",
            "--published-file-name", "shell-2.0.0.msix",
            "--output", "request.json"
        ]);

        Assert.Equal(OrganizationId, options.OrganizationId);
        Assert.Equal(UpdateComponentNames.PlayerShell, options.Component);
        Assert.Equal("shell-2.0.0.msix", options.PublishedFileName);
        Assert.Equal("request.json", options.RequestJsonOutputPath);
    }

    [Fact]
    public void Parse_ReadsHttpPutAndEnvironmentSigningKeyArguments()
    {
        var options = UpdatePackagePublishCommand.Parse(
        [
            "--organization-id", OrganizationId.ToString("D"),
            "--component", UpdateComponentNames.AgentService,
            "--version", "3.0.0",
            "--channel", UpdateChannelNames.Stable,
            "--artifact", "agent.zip",
            "--artifact-store", UpdateArtifactStoreKindNames.HttpPut,
            "--artifact-upload-uri", "https://storage.example.test/upload-token",
            "--artifact-public-uri", "https://cdn.example.test/agent-service/stable/3.0.0/agent.zip",
            "--signing-key-env-var", "AFK4_UPDATE_SIGNING_KEY_PEM",
            "--release-notes", "Stable Agent update."
        ]);

        Assert.Equal(UpdateArtifactStoreKindNames.HttpPut, options.ArtifactStoreKind);
        Assert.Equal("https://storage.example.test/upload-token", options.ArtifactUploadUri?.ToString());
        Assert.Equal("https://cdn.example.test/agent-service/stable/3.0.0/agent.zip", options.PublicArtifactUri?.ToString());
        Assert.Equal("AFK4_UPDATE_SIGNING_KEY_PEM", options.SigningPrivateKeyPemEnvironmentVariable);
        Assert.Null(options.SigningPrivateKeyPemPath);
    }

    private string CreatePrivateKeyPem(out string publicKeyPem)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var privateKeyPath = Path.Combine(tempRoot, "update-signing-key.pem");
        File.WriteAllText(privateKeyPath, ecdsa.ExportECPrivateKeyPem());
        return privateKeyPath;
    }

    private static bool VerifySignature(string publicKeyPem, CreateUpdatePackageRequest request)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(publicKeyPem);
        var payload = UpdatePackageSignaturePayload.Create(
            request.Component,
            request.Version,
            request.Channel,
            request.ArtifactUri,
            request.Sha256,
            request.SizeBytes,
            request.ReleaseNotes);

        return ecdsa.VerifyData(
            payload,
            Convert.FromBase64String(request.Signature),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public byte[]? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastBody = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}
