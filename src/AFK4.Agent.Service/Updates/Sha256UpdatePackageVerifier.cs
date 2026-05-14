using System.Security.Cryptography;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class Sha256UpdatePackageVerifier(IOptions<AgentOptions> options) : IUpdatePackageVerifier
{
    public async Task<UpdatePackageVerificationResult> VerifyAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(instruction.Sha256))
        {
            return UpdatePackageVerificationResult.Invalid("Update package SHA-256 hash is missing.");
        }

        if (string.IsNullOrWhiteSpace(instruction.Signature) ||
            string.IsNullOrWhiteSpace(instruction.SignatureAlgorithm))
        {
            return UpdatePackageVerificationResult.Invalid("Update package signature metadata is missing.");
        }

        if (!string.Equals(
            instruction.SignatureAlgorithm,
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            StringComparison.Ordinal))
        {
            return UpdatePackageVerificationResult.Invalid("Unsupported update package signature algorithm.");
        }

        if (string.IsNullOrWhiteSpace(options.Value.UpdatePackageSigningPublicKeyPem))
        {
            return UpdatePackageVerificationResult.Invalid("Update package verification public key is not configured.");
        }

        if (!File.Exists(artifact.FilePath))
        {
            return UpdatePackageVerificationResult.Invalid("Downloaded update artifact was not found.");
        }

        if (instruction.SizeBytes > 0 && artifact.SizeBytes != instruction.SizeBytes)
        {
            return UpdatePackageVerificationResult.Invalid("Downloaded update artifact size does not match package metadata.");
        }

        await using var stream = File.OpenRead(artifact.FilePath);
        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        if (!string.Equals(hash, instruction.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return UpdatePackageVerificationResult.Invalid("Downloaded update artifact SHA-256 hash does not match package metadata.");
        }

        var payload = UpdatePackageSignaturePayload.Create(
            instruction.Component,
            instruction.Version,
            instruction.Channel,
            instruction.ArtifactUri,
            hash,
            artifact.SizeBytes,
            instruction.ReleaseNotes);

        if (!VerifySignature(options.Value.UpdatePackageSigningPublicKeyPem, instruction.Signature, payload))
        {
            return UpdatePackageVerificationResult.Invalid("Update package signature does not match package metadata.");
        }

        return UpdatePackageVerificationResult.Valid("Update package hash and signature verified.");
    }

    private static bool VerifySignature(string publicKeyPem, string signature, byte[] payload)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);
            return ecdsa.VerifyData(
                payload,
                Convert.FromBase64String(signature),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
