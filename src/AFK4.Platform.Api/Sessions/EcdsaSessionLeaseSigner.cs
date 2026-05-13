using System.Security.Cryptography;
using System.Text;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Sessions;

public sealed class EcdsaSessionLeaseSigner(IOptions<SessionLeaseOptions> options) : ISessionLeaseSigner
{
    public const string SignatureAlgorithm = "ECDSA-P256-SHA256";

    public SessionLeaseDto Sign(
        Guid SessionId,
        Guid OrganizationId,
        Guid BranchId,
        Guid SeatId,
        Guid DeviceId,
        string State,
        int Sequence,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SigningPrivateKeyPem))
        {
            throw new InvalidOperationException("Session lease signing private key is not configured.");
        }

        var leaseWithoutSignature = new SessionLeaseDto(
            SessionId,
            OrganizationId,
            BranchId,
            SeatId,
            DeviceId,
            State,
            Sequence,
            IssuedAtUtc,
            ExpiresAtUtc,
            SignatureAlgorithm,
            Signature: string.Empty);
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(leaseWithoutSignature);

        using var signer = ECDsa.Create();
        signer.ImportFromPem(options.Value.SigningPrivateKeyPem);
        var signature = signer.SignData(
            Encoding.UTF8.GetBytes(payload),
            HashAlgorithmName.SHA256);

        return leaseWithoutSignature with
        {
            Signature = Convert.ToBase64String(signature)
        };
    }
}
