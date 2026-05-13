using System.Security.Cryptography;
using System.Text;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class SessionLeaseValidator(IOptions<AgentOptions> options, TimeProvider timeProvider)
{
    public SessionLeaseValidationResult Validate(SessionLeaseDto lease)
    {
        var agentOptions = options.Value;

        if (string.IsNullOrWhiteSpace(agentOptions.LeaseSigningPublicKeyPem))
        {
            return SessionLeaseValidationResult.Invalid("Lease signing public key is not configured.");
        }

        if (lease.OrganizationId != agentOptions.OrganizationId)
        {
            return SessionLeaseValidationResult.Invalid("Lease organization does not match this Agent.");
        }

        if (lease.BranchId != agentOptions.BranchId)
        {
            return SessionLeaseValidationResult.Invalid("Lease branch does not match this Agent.");
        }

        if (lease.DeviceId != agentOptions.DeviceId)
        {
            return SessionLeaseValidationResult.Invalid("Lease device does not match this Agent.");
        }

        if (lease.ExpiresAtUtc <= timeProvider.GetUtcNow())
        {
            return SessionLeaseValidationResult.Invalid("Lease has expired.");
        }

        if (!string.Equals(lease.SignatureAlgorithm, "ECDSA-P256-SHA256", StringComparison.Ordinal))
        {
            return SessionLeaseValidationResult.Invalid("Lease signature algorithm is not supported.");
        }

        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(agentOptions.LeaseSigningPublicKeyPem);
            var payload = SessionLeasePayloadCanonicalizer.CreatePayload(lease with { Signature = string.Empty });
            var signature = Convert.FromBase64String(lease.Signature);
            var verified = verifier.VerifyData(
                Encoding.UTF8.GetBytes(payload),
                signature,
                HashAlgorithmName.SHA256);

            return verified
                ? SessionLeaseValidationResult.Valid()
                : SessionLeaseValidationResult.Invalid("Lease signature is invalid.");
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return SessionLeaseValidationResult.Invalid("Lease signature could not be verified.");
        }
    }
}
