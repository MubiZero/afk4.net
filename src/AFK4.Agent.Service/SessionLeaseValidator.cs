using System.Security.Cryptography;
using System.Text;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

/// <summary>
/// Проверяет аренду: подписанную платформой или офлайн-аренду, которую подписал ключ ПК администратора
/// этого филиала (<see cref="OfflineLeaseSigning"/>). Офлайн-аренда живёт не дольше
/// <see cref="OfflineLeaseSigning.MaxDuration"/>.
/// </summary>
public sealed class SessionLeaseValidator(
    IOptions<AgentOptions> options,
    TimeProvider timeProvider,
    TrustedOfflineKeys? offlineKeys = null)
{
    public SessionLeaseValidationResult Validate(SessionLeaseDto lease)
    {
        var agentOptions = options.Value;

        var isOffline = OfflineLeaseSigning.TryGetKeyId(lease.SignatureAlgorithm, out var offlineKeyId);
        var publicKeyPem = isOffline ? offlineKeys?.FindPublicKeyPem(offlineKeyId) : agentOptions.LeaseSigningPublicKeyPem;
        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            return SessionLeaseValidationResult.Invalid(isOffline
                ? "Offline lease key is not trusted by this Agent."
                : "Lease signing public key is not configured.");
        }

        if (isOffline && lease.ExpiresAtUtc - lease.IssuedAtUtc > OfflineLeaseSigning.MaxDuration)
        {
            return SessionLeaseValidationResult.Invalid("Offline lease is longer than allowed.");
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

        if (!isOffline && !string.Equals(lease.SignatureAlgorithm, "ECDSA-P256-SHA256", StringComparison.Ordinal))
        {
            return SessionLeaseValidationResult.Invalid("Lease signature algorithm is not supported.");
        }

        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(publicKeyPem);
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
