using System.Security.Cryptography;
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
}
