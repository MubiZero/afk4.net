using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Agent.Service.Updates;
using AFK4.Agent.Service.Updates.Delta;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Tests;

/// <summary>Настоящие подписи и настоящее Brotli для тестов дельты: подписанный манифест, хранилище тел и корни.</summary>
internal sealed class DeltaTestKit : IDisposable
{
    public const string BlobBase = "https://updates.afk4.test/store/";
    public const string ManifestUri = "https://updates.afk4.test/store/agent-service/stable/1.2.3/manifest.json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string PublicKeyPem => Key.ExportSubjectPublicKeyInfoPem();

    public void Dispose() => Key.Dispose();

    public static UpdateManifestFileEntry Entry(string root, string path, string content)
    {
        var raw = Encoding.UTF8.GetBytes(content);
        return new UpdateManifestFileEntry(root, path, Sha(content), raw.Length, Compress(raw).Length);
    }

    public (ComponentUpdateInstructionDto Instruction, byte[] ManifestBytes) SignedInstruction(
        IReadOnlyList<UpdateManifestFileEntry> files,
        string component = UpdateComponentNames.AgentService,
        string version = "1.2.3",
        long packageSizeBytes = 60_000_000)
    {
        var document = new UpdateManifestDocument(UpdateManifestLayout.CurrentSchema, component, version, UpdateChannelNames.Stable, BlobBase, files);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var signature = Convert.ToBase64String(Key.SignData(
            UpdateManifestSignaturePayload.Create(component, version, UpdateChannelNames.Stable, ManifestUri, sha, bytes.Length),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        var instruction = new ComponentUpdateInstructionDto(
            Guid.NewGuid(), Guid.NewGuid(), component, version, UpdateChannelNames.Stable,
            "https://updates.afk4.test/store/agent-service/stable/1.2.3/agent.msi", new string('a', 64), "sig",
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363, packageSizeBytes, "notes",
            new UpdateManifestDto(ManifestUri, sha, bytes.Length, signature, UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363));
        return (instruction, bytes);
    }

    public static string Sha(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    public static byte[] Compress(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            brotli.Write(raw);
        }

        return output.ToArray();
    }

    /// <summary>Сеть хранилища: манифест по его адресу и сжатые тела по ключу blobs/aa/sha.br.</summary>
    public sealed class Store : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> responses = new(StringComparer.Ordinal);

        public List<string> Requests { get; } = [];

        public Store WithManifest(byte[] bytes)
        {
            responses[ManifestUri] = bytes;
            return this;
        }

        public Store WithBlobs(params string[] contents)
        {
            foreach (var content in contents)
            {
                responses[BlobBase + UpdateManifestLayout.BlobObjectKey(Sha(content))] = Compress(Encoding.UTF8.GetBytes(content));
            }

            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            lock (Requests)
            {
                Requests.Add(uri);
            }

            return Task.FromResult(responses.TryGetValue(uri, out var body)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    public sealed class HttpFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    public sealed class Roots(params (string Root, string Directory)[] roots) : IUpdateRootResolver
    {
        public string? Resolve(string root) => roots.FirstOrDefault(entry => entry.Root == root).Directory;
    }

    public sealed class RecordingEnvironment : IMachineEnvironment
    {
        public Dictionary<string, string> Values { get; } = [];

        public void Set(string name, string value) => Values[name] = value;
    }

    public sealed class RecordingRestart : IAgentRestartScheduler
    {
        public List<UpdateInstallState> Scheduled { get; } = [];

        public Task<UpdateRestartResult> ScheduleRestartAsync(
            ComponentUpdateInstructionDto instruction, UpdateInstallState state, CancellationToken cancellationToken)
        {
            Scheduled.Add(state);
            return Task.FromResult(UpdateRestartResult.Scheduled("Agent restart scheduled."));
        }
    }
}
