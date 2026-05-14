using System.Security.Cryptography;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class Sha256UpdatePackageVerifierTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-agent-update-verify-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyAsync_WithValidHashAndSignature_ReturnsValid()
    {
        var artifactFile = await CreateArtifactAsync([0x41, 0x46, 0x4b, 0x34]);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var instruction = CreateSignedInstruction(artifactFile.FilePath, artifactFile.SizeBytes, ecdsa);
        var artifact = new DownloadedUpdateArtifact(instruction, artifactFile.FilePath, artifactFile.SizeBytes);
        var verifier = CreateVerifier(ecdsa.ExportSubjectPublicKeyInfoPem());

        var result = await verifier.VerifyAsync(instruction, artifact, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("Update package hash and signature verified.", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_WithModifiedSignedMetadata_ReturnsInvalid()
    {
        var artifactFile = await CreateArtifactAsync([0x41, 0x46, 0x4b, 0x34]);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var instruction = CreateSignedInstruction(artifactFile.FilePath, artifactFile.SizeBytes, ecdsa) with
        {
            ArtifactUri = "https://updates.afk4.test/packages/agent-service/internal/1.2.3/other.zip"
        };
        var artifact = new DownloadedUpdateArtifact(instruction, artifactFile.FilePath, artifactFile.SizeBytes);
        var verifier = CreateVerifier(ecdsa.ExportSubjectPublicKeyInfoPem());

        var result = await verifier.VerifyAsync(instruction, artifact, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Update package signature does not match package metadata.", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_WithoutConfiguredPublicKey_ReturnsInvalid()
    {
        var artifactFile = await CreateArtifactAsync([0x41, 0x46, 0x4b, 0x34]);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var instruction = CreateSignedInstruction(artifactFile.FilePath, artifactFile.SizeBytes, ecdsa);
        var artifact = new DownloadedUpdateArtifact(instruction, artifactFile.FilePath, artifactFile.SizeBytes);
        var verifier = CreateVerifier(publicKeyPem: string.Empty);

        var result = await verifier.VerifyAsync(instruction, artifact, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Update package verification public key is not configured.", result.Message);
    }

    [Fact]
    public async Task VerifyAsync_WithUnsupportedSignatureAlgorithm_ReturnsInvalid()
    {
        var artifactFile = await CreateArtifactAsync([0x41, 0x46, 0x4b, 0x34]);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var instruction = CreateSignedInstruction(artifactFile.FilePath, artifactFile.SizeBytes, ecdsa) with
        {
            SignatureAlgorithm = "ed25519"
        };
        var artifact = new DownloadedUpdateArtifact(instruction, artifactFile.FilePath, artifactFile.SizeBytes);
        var verifier = CreateVerifier(ecdsa.ExportSubjectPublicKeyInfoPem());

        var result = await verifier.VerifyAsync(instruction, artifact, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("Unsupported update package signature algorithm.", result.Message);
    }

    private Sha256UpdatePackageVerifier CreateVerifier(string publicKeyPem)
    {
        return new Sha256UpdatePackageVerifier(Options.Create(new AgentOptions
        {
            UpdatePackageSigningPublicKeyPem = publicKeyPem
        }));
    }

    private async Task<(string FilePath, long SizeBytes)> CreateArtifactAsync(byte[] bytes)
    {
        Directory.CreateDirectory(tempRoot);
        var path = Path.Combine(tempRoot, "agent.zip");
        await File.WriteAllBytesAsync(path, bytes);
        return (path, bytes.Length);
    }

    private static ComponentUpdateInstructionDto CreateSignedInstruction(
        string artifactPath,
        long sizeBytes,
        ECDsa privateKey)
    {
        var sha256 = ComputeSha256(artifactPath);
        var artifactUri = "https://updates.afk4.test/packages/agent-service/internal/1.2.3/agent.zip";
        var releaseNotes = "Internal Agent update.";
        var payload = UpdatePackageSignaturePayload.Create(
            UpdateComponentNames.AgentService,
            "1.2.3",
            UpdateChannelNames.Internal,
            artifactUri,
            sha256,
            sizeBytes,
            releaseNotes);
        var signature = privateKey.SignData(
            payload,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return new ComponentUpdateInstructionDto(
            Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            UpdateComponentNames.AgentService,
            "1.2.3",
            UpdateChannelNames.Internal,
            artifactUri,
            sha256,
            Convert.ToBase64String(signature),
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            sizeBytes,
            releaseNotes);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
