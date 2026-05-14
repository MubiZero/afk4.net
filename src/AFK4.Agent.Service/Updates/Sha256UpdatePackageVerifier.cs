using System.Security.Cryptography;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed class Sha256UpdatePackageVerifier : IUpdatePackageVerifier
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

        return UpdatePackageVerificationResult.Valid("Update package hash and signature metadata verified.");
    }
}
