using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionLeaseSignerTests
{
    [Fact]
    public void Sign_CreatesVerifiableEcdsaSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = key.ExportECPrivateKeyPem();
        var publicPem = key.ExportSubjectPublicKeyInfoPem();
        var signer = new EcdsaSessionLeaseSigner(Options.Create(new SessionLeaseOptions
        {
            SigningPrivateKeyPem = privatePem
        }));

        var lease = signer.Sign(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: TestIds.OrganizationId,
            BranchId: TestIds.BranchId,
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: TestIds.DeviceId,
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"));

        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(publicPem);
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(lease with { Signature = string.Empty });

        Assert.Equal("ECDSA-P256-SHA256", lease.SignatureAlgorithm);
        Assert.True(verifier.VerifyData(
            Encoding.UTF8.GetBytes(payload),
            Convert.FromBase64String(lease.Signature),
            HashAlgorithmName.SHA256));
    }
}
