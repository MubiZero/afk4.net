using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Update.Publisher.Tests;

public sealed class UpdateManifestPublishingTests : IDisposable
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-manifest-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Builder_HashesEveryFile_ReusesOneBlobForIdenticalContent_AndCompressesToBrotli()
    {
        var service = CreateTree("service", ("App.dll", "app-v1"), ("sub/System.Runtime.dll", "runtime"));
        var wizard = CreateTree("wizard", ("Wizard.dll", "wizard-v1"), ("System.Runtime.dll", "runtime"));

        var build = await UpdateManifestBuilder.BuildAsync(
            [new UpdateManifestRoot("agent-service", service), new UpdateManifestRoot("setup-wizard", wizard)],
            Path.Combine(tempRoot, "work"),
            CancellationToken.None);

        Assert.Equal(
            ["agent-service:App.dll", "agent-service:sub/System.Runtime.dll", "setup-wizard:System.Runtime.dll", "setup-wizard:Wizard.dll"],
            build.Files.Select(file => $"{file.Root}:{file.Path}"));
        Assert.Equal(3, build.Blobs.Count);

        var runtimeSha = Sha256Hex("runtime");
        Assert.Equal(2, build.Files.Count(file => file.Sha256 == runtimeSha));
        Assert.Single(build.Blobs, blob => blob.Sha256 == runtimeSha);
        Assert.All(build.Files, file => Assert.True(file.SizeBytes > 0 && file.CompressedSizeBytes > 0));

        var appBlob = build.Blobs.Single(blob => blob.Sha256 == Sha256Hex("app-v1"));
        await using var compressed = File.OpenRead(appBlob.CompressedPath);
        await using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
        using var restored = new StreamReader(brotli);
        Assert.Equal("app-v1", await restored.ReadToEndAsync());
        Assert.Equal(appBlob.CompressedSha256, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(appBlob.CompressedPath))).ToLowerInvariant());
    }

    [Fact]
    public async Task Publish_UploadsOnlyMissingBlobs_AndSignsTheManifestItUploaded()
    {
        var service = CreateTree("service", ("App.dll", "app-v2"), ("Runtime.dll", "runtime"));
        var alreadyStored = Sha256Hex("runtime");
        var handler = new FakeStoreHandler(existingBlobHashes: [alreadyStored]);
        var privateKeyPath = CreatePrivateKeyPem(out var publicKeyPem);

        var result = await Publish(handler, privateKeyPath, [new UpdateManifestRoot("agent-service", service)]);

        var blobUploads = handler.Puts.Where(put => put.Path.Contains("/blobs/", StringComparison.Ordinal)).ToList();
        Assert.Single(blobUploads);
        Assert.Contains(Sha256Hex("app-v2"), blobUploads[0].Path, StringComparison.Ordinal);
        Assert.EndsWith(".br", blobUploads[0].Path, StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Puts, put => put.Path.Contains(alreadyStored, StringComparison.Ordinal));

        var manifestUpload = Assert.Single(handler.Puts, put => put.Path.EndsWith("-manifest.json", StringComparison.Ordinal));
        var manifest = result.Request.Manifest!;
        Assert.Equal(Sha256Hex(manifestUpload.Body), manifest.Sha256);
        Assert.Equal(manifestUpload.Body.Length, manifest.SizeBytes);
        Assert.EndsWith("-manifest.json", manifest.Uri, StringComparison.Ordinal);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(publicKeyPem);
        Assert.True(ecdsa.VerifyData(
            UpdateManifestSignaturePayload.Create("agent-service", "1.2.3", "internal", manifest.Uri, manifest.Sha256, manifest.SizeBytes),
            Convert.FromBase64String(manifest.Signature),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        var document = JsonSerializer.Deserialize<UpdateManifestDocument>(manifestUpload.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(UpdateManifestLayout.CurrentSchema, document.Schema);
        Assert.Equal("agent-service", document.Component);
        Assert.Equal("https://updates.example.test/afk4-updates/", document.BlobBaseUri);
        Assert.Equal(["App.dll", "Runtime.dll"], document.Files.Select(file => file.Path));
    }

    [Fact]
    public async Task Publish_WithoutManifestRoots_DoesNotAttachAManifest()
    {
        var handler = new FakeStoreHandler(existingBlobHashes: []);

        var result = await Publish(handler, CreatePrivateKeyPem(out _), manifestRoots: null);

        Assert.Null(result.Request.Manifest);
        Assert.DoesNotContain(handler.Puts, put => put.Path.Contains("/blobs/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Publish_RejectsManifestRootsOutsideS3()
    {
        var service = CreateTree("service", ("App.dll", "x"));
        var artifact = Path.Combine(tempRoot, "agent.msi");
        await File.WriteAllBytesAsync(artifact, [1, 2, 3]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => new FileSystemUpdatePackagePublisher().PublishAsync(
            new UpdatePackagePublishOptions(
                OrganizationId, UpdateComponentNames.AgentService, "1.2.3", UpdateChannelNames.Internal, artifact,
                Path.Combine(tempRoot, "hosting"), new Uri("https://updates.example.test/"), CreatePrivateKeyPem(out _), "notes",
                ManifestRoots: [new UpdateManifestRoot("agent-service", service)]),
            CancellationToken.None));

        Assert.Contains("only for s3", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsManifestRoots()
    {
        var options = UpdatePackagePublishCommand.Parse(
        [
            "--organization-id", OrganizationId.ToString("D"),
            "--component", UpdateComponentNames.AgentService,
            "--version", "3.0.1",
            "--channel", UpdateChannelNames.Internal,
            "--artifact", "agent.msi",
            "--release-notes", "notes",
            "--manifest-roots", "agent-service=C:\\publish\\agent; setup-wizard=C:\\publish\\wizard"
        ]);

        Assert.Equal(
            [("agent-service", "C:\\publish\\agent"), ("setup-wizard", "C:\\publish\\wizard")],
            options.ManifestRoots!.Select(root => (root.Name, root.Directory)));
    }

    private async Task<UpdatePackagePublishResult> Publish(
        FakeStoreHandler handler,
        string privateKeyPath,
        IReadOnlyList<UpdateManifestRoot>? manifestRoots)
    {
        Directory.CreateDirectory(tempRoot);
        var artifact = Path.Combine(tempRoot, "agent.msi");
        await File.WriteAllBytesAsync(artifact, [1, 2, 3, 4]);
        Environment.SetEnvironmentVariable("AFK4_TEST_MANIFEST_ACCESS", "access");
        Environment.SetEnvironmentVariable("AFK4_TEST_MANIFEST_SECRET", "secret");
        try
        {
            return await new FileSystemUpdatePackagePublisher(httpClient: new HttpClient(handler)).PublishAsync(
                new UpdatePackagePublishOptions(
                    OrganizationId,
                    UpdateComponentNames.AgentService,
                    "1.2.3",
                    UpdateChannelNames.Internal,
                    artifact,
                    HostingRoot: string.Empty,
                    PublicBaseUri: new Uri("https://updates.example.test/afk4-updates/"),
                    privateKeyPath,
                    "Internal Agent update.",
                    ArtifactStoreKind: UpdateArtifactStoreKindNames.S3,
                    S3Endpoint: new Uri("https://minio.example.test"),
                    S3Bucket: "afk4-updates",
                    S3AccessKeyEnvironmentVariable: "AFK4_TEST_MANIFEST_ACCESS",
                    S3SecretKeyEnvironmentVariable: "AFK4_TEST_MANIFEST_SECRET",
                    ManifestRoots: manifestRoots),
                CancellationToken.None);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AFK4_TEST_MANIFEST_ACCESS", null);
            Environment.SetEnvironmentVariable("AFK4_TEST_MANIFEST_SECRET", null);
        }
    }

    private string CreateTree(string name, params (string Path, string Content)[] files)
    {
        var root = Path.Combine(tempRoot, name);
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return root;
    }

    private static string Sha256Hex(string text) => Sha256Hex(System.Text.Encoding.UTF8.GetBytes(text));

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private string CreatePrivateKeyPem(out string publicKeyPem)
    {
        Directory.CreateDirectory(tempRoot);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        publicKeyPem = ecdsa.ExportSubjectPublicKeyInfoPem();
        var path = Path.Combine(tempRoot, $"key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, ecdsa.ExportPkcs8PrivateKeyPem());
        return path;
    }

    private sealed class FakeStoreHandler(IReadOnlyCollection<string> existingBlobHashes) : HttpMessageHandler
    {
        private readonly object gate = new();

        public List<(string Path, byte[] Body)> Puts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Head)
            {
                var exists = existingBlobHashes.Any(hash => path.Contains(hash, StringComparison.Ordinal));
                return new HttpResponseMessage(exists ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            }

            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            if (request.Method == HttpMethod.Put)
            {
                lock (gate)
                {
                    Puts.Add((path, body));
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
