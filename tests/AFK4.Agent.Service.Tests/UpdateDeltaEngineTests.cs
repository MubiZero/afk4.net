using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AFK4.Agent.Service.Updates.Delta;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class UpdateDeltaEngineTests : IDisposable
{
    private const string BlobBase = "https://updates.afk4.test/store/";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-delta-{Guid.NewGuid():N}");
    private readonly ECDsa signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public void Dispose()
    {
        signingKey.Dispose();
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── безопасность пути ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("App.dll", true)]
    [InlineData("sub/dir/System.Runtime.dll", true)]
    [InlineData("../evil.dll", false)]
    [InlineData("sub/../../evil.dll", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("C:/Windows/evil.dll", false)]
    [InlineData("sub\\evil.dll", false)]
    [InlineData("a//b.dll", false)]
    [InlineData("trailing./x.dll", false)]
    [InlineData("name .dll/x", true)]
    [InlineData("", false)]
    public void RelativePath_StaysInsideTheRoot(string path, bool allowed)
    {
        var root = Directory.CreateDirectory(Path.Combine(tempRoot, "root")).FullName;

        var ok = UpdateRelativePath.TryResolve(root, path, out var full);

        Assert.Equal(allowed, ok);
        if (ok)
        {
            Assert.StartsWith(root, full, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── проверка манифеста ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Verifier_AcceptsASignedManifestThatMatchesTheInstruction()
    {
        var (instruction, bytes) = SignedManifest([Entry("agent-service", "App.dll", "app")]);

        var result = UpdateManifestVerifier.Verify(instruction, instruction.Manifest!, bytes, PublicKeyPem());

        Assert.True(result.IsValid, result.Message);
        Assert.Equal("App.dll", Assert.Single(result.Document!.Files).Path);
    }

    [Fact]
    public void Verifier_RejectsTamperedBytes_WrongKey_AndBrokenMetadata()
    {
        var (instruction, bytes) = SignedManifest([Entry("agent-service", "App.dll", "app")]);
        var tampered = bytes.ToArray();
        tampered[10] ^= 0x01;
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.False(UpdateManifestVerifier.Verify(instruction, instruction.Manifest!, tampered, PublicKeyPem()).IsValid);
        Assert.False(UpdateManifestVerifier.Verify(instruction, instruction.Manifest!, bytes, otherKey.ExportSubjectPublicKeyInfoPem()).IsValid);
        Assert.False(UpdateManifestVerifier.Verify(instruction, instruction.Manifest! with { Signature = Convert.ToBase64String(new byte[64]) }, bytes, PublicKeyPem()).IsValid);
        Assert.False(UpdateManifestVerifier.Verify(instruction, instruction.Manifest! with { SignatureAlgorithm = "RSA" }, bytes, PublicKeyPem()).IsValid);
        Assert.False(UpdateManifestVerifier.Verify(instruction, instruction.Manifest!, bytes, string.Empty).IsValid);
    }

    [Fact]
    public void Verifier_RejectsAManifestOfAnotherReleaseOrHost()
    {
        var (instruction, bytes) = SignedManifest([Entry("agent-service", "App.dll", "app")]);
        // Подпись манифеста привязана к версии: инструкция с другой версией не примет его даже с верным ключом.
        var otherVersion = instruction with { Version = "9.9.9" };
        Assert.False(UpdateManifestVerifier.Verify(otherVersion, instruction.Manifest!, bytes, PublicKeyPem()).IsValid);

        var (foreign, foreignBytes) = SignedManifest([Entry("agent-service", "App.dll", "app")], blobBase: "https://evil.example/store/");
        var result = UpdateManifestVerifier.Verify(foreign, foreign.Manifest!, foreignBytes, PublicKeyPem());
        Assert.False(result.IsValid);
        Assert.Contains("package host", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../evil.dll", "agent-service")]
    [InlineData("App.dll", "unknown-root")]
    public void Verifier_RejectsUnsafePathsAndUnknownRoots_EvenWhenSigned(string path, string root)
    {
        var (instruction, bytes) = SignedManifest([Entry(root, path, "x")]);

        Assert.False(UpdateManifestVerifier.Verify(instruction, instruction.Manifest!, bytes, PublicKeyPem()).IsValid);
    }

    [Fact]
    public void Verifier_RejectsDuplicateEntriesAndEmptyManifests()
    {
        var (dup, dupBytes) = SignedManifest([Entry("agent-service", "App.dll", "a"), Entry("agent-service", "app.DLL", "b")]);
        var (empty, emptyBytes) = SignedManifest([]);

        Assert.False(UpdateManifestVerifier.Verify(dup, dup.Manifest!, dupBytes, PublicKeyPem()).IsValid);
        Assert.False(UpdateManifestVerifier.Verify(empty, empty.Manifest!, emptyBytes, PublicKeyPem()).IsValid);
    }

    // ── план ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Planner_FetchesOnlyChangedMissingAndWrongSizeFiles()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        await File.WriteAllTextAsync(Path.Combine(service, "same.dll"), "same");
        await File.WriteAllTextAsync(Path.Combine(service, "old.dll"), "old-content");
        await File.WriteAllTextAsync(Path.Combine(service, "short.dll"), "x");
        var manifest = Document(
            Entry("agent-service", "same.dll", "same"),
            Entry("agent-service", "old.dll", "new-content!"),
            Entry("agent-service", "short.dll", "much longer content"),
            Entry("agent-service", "added/new.dll", "added"));

        var plan = await UpdateDeltaPlanner.PlanAsync(manifest, new FixedRoots(("agent-service", service)), CancellationToken.None);

        Assert.NotNull(plan);
        Assert.Equal(1, plan.UnchangedCount);
        Assert.Equal(["added/new.dll", "old.dll", "short.dll"], plan.ToFetch.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.Equal(Path.Combine(service, "added", "new.dll"), plan.ToFetch.Single(file => file.Path == "added/new.dll").TargetPath);
        Assert.True(plan.DownloadBytes > 0);
    }

    [Fact]
    public async Task Planner_CountsASharedBlobOnce_AndReturnsNullWhenARootIsNotInstalled()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var wizard = Directory.CreateDirectory(Path.Combine(tempRoot, "wizard")).FullName;
        var manifest = Document(
            Entry("agent-service", "System.Runtime.dll", "runtime"),
            Entry("setup-wizard", "System.Runtime.dll", "runtime"));

        var plan = await UpdateDeltaPlanner.PlanAsync(
            manifest, new FixedRoots(("agent-service", service), ("setup-wizard", wizard)), CancellationToken.None);

        Assert.Equal(2, plan!.ToFetch.Count);
        Assert.Equal(1, plan.UniqueBlobCount);
        Assert.Equal(manifest.Files[0].CompressedSizeBytes, plan.DownloadBytes);
        Assert.Null(await UpdateDeltaPlanner.PlanAsync(manifest, new FixedRoots(("agent-service", service)), CancellationToken.None));
    }

    [Fact]
    public async Task Plan_IsWorthApplyingOnlyWhenItSavesDownloadAndChangesSomething()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        await File.WriteAllTextAsync(Path.Combine(service, "same.dll"), "same");
        var identical = await UpdateDeltaPlanner.PlanAsync(Document(Entry("agent-service", "same.dll", "same")), new FixedRoots(("agent-service", service)), CancellationToken.None);
        var small = await UpdateDeltaPlanner.PlanAsync(Document(Entry("agent-service", "new.dll", "new")), new FixedRoots(("agent-service", service)), CancellationToken.None);

        Assert.False(identical!.IsWorthApplying(1_000_000, 0.8));
        Assert.True(small!.IsWorthApplying(1_000_000, 0.8));
        Assert.False(small.IsWorthApplying(small.DownloadBytes, 0.5));
    }

    // ── докачка ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fetcher_DownloadsEachBlobOnce_VerifiesIt_AndStagesNothingInTheInstallFolder()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var wizard = Directory.CreateDirectory(Path.Combine(tempRoot, "wizard")).FullName;
        var manifest = Document(
            Entry("agent-service", "System.Runtime.dll", "runtime"),
            Entry("setup-wizard", "System.Runtime.dll", "runtime"),
            Entry("agent-service", "App.dll", "app-v2"));
        var plan = (await UpdateDeltaPlanner.PlanAsync(
            manifest, new FixedRoots(("agent-service", service), ("setup-wizard", wizard)), CancellationToken.None))!;
        var store = new BlobStore(["runtime", "app-v2"]);

        var staged = await CreateFetcher(store).FetchAsync(plan, Path.Combine(tempRoot, "stage"), CancellationToken.None);

        Assert.Equal(2, store.Requests.Count);
        Assert.Equal(3, staged.Files.Count);
        Assert.Equal("runtime", await File.ReadAllTextAsync(staged.Files.First(file => file.Path == "System.Runtime.dll").StagedPath));
        Assert.Empty(Directory.EnumerateFiles(service));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(tempRoot, "stage"), "*.tmp", SearchOption.AllDirectories));

        // Повторный запуск берёт готовое из папки подготовки и не качает снова.
        await CreateFetcher(store).FetchAsync(plan, Path.Combine(tempRoot, "stage"), CancellationToken.None);
        Assert.Equal(2, store.Requests.Count);
    }

    [Fact]
    public async Task Fetcher_RejectsABlobWhoseContentIsNotTheManifestHash_AndKeepsNoStagedFile()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var plan = (await UpdateDeltaPlanner.PlanAsync(
            Document(Entry("agent-service", "App.dll", "expected")), new FixedRoots(("agent-service", service)), CancellationToken.None))!;
        var store = new BlobStore([]);
        store.Serve(Sha("expected"), "XXXXXXXX"); // той же длины, чтобы сработала именно проверка хеша

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateFetcher(store).FetchAsync(plan, Path.Combine(tempRoot, "stage"), CancellationToken.None));

        Assert.Contains("hash", failure.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(tempRoot, "stage"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Fetcher_RejectsABlobThatInflatesBeyondItsDeclaredSize()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var plan = (await UpdateDeltaPlanner.PlanAsync(
            Document(Entry("agent-service", "App.dll", "tiny")), new FixedRoots(("agent-service", service)), CancellationToken.None))!;
        var store = new BlobStore([]);
        store.Serve(Sha("tiny"), new string('A', 200_000));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            CreateFetcher(store).FetchAsync(plan, Path.Combine(tempRoot, "stage"), CancellationToken.None));
    }

    [Fact]
    public async Task Fetcher_SurfacesAMissingBlobAsAnHttpFailure()
    {
        var service = Directory.CreateDirectory(Path.Combine(tempRoot, "service")).FullName;
        var plan = (await UpdateDeltaPlanner.PlanAsync(
            Document(Entry("agent-service", "App.dll", "gone")), new FixedRoots(("agent-service", service)), CancellationToken.None))!;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateFetcher(new BlobStore([])).FetchAsync(plan, Path.Combine(tempRoot, "stage"), CancellationToken.None));
    }

    [Fact]
    public async Task Client_ReturnsOnlyAVerifiedManifest_AndNothingWhenThePackageHasNone()
    {
        var (instruction, bytes) = SignedManifest([Entry("agent-service", "App.dll", "app")]);
        var handler = new RoutingHandler(request =>
            request.RequestUri!.AbsoluteUri == instruction.Manifest!.Uri
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new UpdateManifestClient(new TestHttpClientFactory(new HttpClient(handler)), Options());

        var ok = await client.FetchAsync(instruction, CancellationToken.None);
        var none = await client.FetchAsync(instruction with { Manifest = null }, CancellationToken.None);
        var tamperedHandler = new RoutingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([.. bytes, 0x20]) });
        var tampered = await new UpdateManifestClient(new TestHttpClientFactory(new HttpClient(tamperedHandler)), Options())
            .FetchAsync(instruction, CancellationToken.None);

        Assert.True(ok.IsValid, ok.Message);
        Assert.False(none.IsValid);
        Assert.False(tampered.IsValid);
    }

    // ── вспомогательное ──────────────────────────────────────────────────────────────────────

    private UpdateManifestFileEntry Entry(string root, string path, string content)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(content);
        return new UpdateManifestFileEntry(root, path, Sha(content), raw.Length, Compress(raw).Length);
    }

    private static UpdateManifestDocument Document(params UpdateManifestFileEntry[] files) =>
        new(UpdateManifestLayout.CurrentSchema, UpdateComponentNames.AgentService, "1.2.3", UpdateChannelNames.Stable, BlobBase, files);

    private (ComponentUpdateInstructionDto Instruction, byte[] Bytes) SignedManifest(
        IReadOnlyList<UpdateManifestFileEntry> files,
        string blobBase = BlobBase)
    {
        var document = new UpdateManifestDocument(
            UpdateManifestLayout.CurrentSchema, UpdateComponentNames.AgentService, "1.2.3", UpdateChannelNames.Stable, blobBase, files);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        const string uri = "https://updates.afk4.test/store/agent-service/stable/1.2.3/manifest.json";
        var signature = Convert.ToBase64String(signingKey.SignData(
            UpdateManifestSignaturePayload.Create(UpdateComponentNames.AgentService, "1.2.3", UpdateChannelNames.Stable, uri, sha, bytes.Length),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var instruction = new ComponentUpdateInstructionDto(
            Guid.NewGuid(), Guid.NewGuid(), UpdateComponentNames.AgentService, "1.2.3", UpdateChannelNames.Stable,
            "https://updates.afk4.test/store/agent-service/stable/1.2.3/agent.msi", new string('a', 64), "sig",
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363, 60_000_000, "notes",
            new UpdateManifestDto(uri, sha, bytes.Length, signature, UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363));
        return (instruction, bytes);
    }

    private string PublicKeyPem() => signingKey.ExportSubjectPublicKeyInfoPem();

    private IOptions<AgentOptions> Options() => Microsoft.Extensions.Options.Options.Create(new AgentOptions
    {
        UpdatePackageSigningPublicKeyPem = PublicKeyPem(),
        UpdateDownloadStallTimeout = TimeSpan.FromSeconds(5)
    });

    private UpdateBlobFetcher CreateFetcher(BlobStore store) =>
        new(new TestHttpClientFactory(new HttpClient(store)), Options());

    private static string Sha(string content) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static byte[] Compress(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(raw);
        }

        return output.ToArray();
    }

    private sealed class FixedRoots(params (string Root, string Directory)[] roots) : IUpdateRootResolver
    {
        public string? Resolve(string root) => roots.FirstOrDefault(entry => entry.Root == root).Directory;
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RoutingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    /// <summary>Хранилище тел: отдаёт сжатое тело по ключу blobs/aa/sha.br и считает запросы.</summary>
    private sealed class BlobStore : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal);

        public BlobStore(IEnumerable<string> contents)
        {
            foreach (var content in contents)
            {
                Serve(Sha(content), content);
            }
        }

        public List<string> Requests { get; } = [];

        public void Serve(string sha, string content) =>
            blobs[BlobBase + UpdateManifestLayout.BlobObjectKey(sha)] = Compress(System.Text.Encoding.UTF8.GetBytes(content));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            lock (Requests)
            {
                Requests.Add(uri);
            }

            return Task.FromResult(blobs.TryGetValue(uri, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
