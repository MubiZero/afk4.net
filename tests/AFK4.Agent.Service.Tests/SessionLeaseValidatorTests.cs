using System.Security.Cryptography;
using System.Text;
using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class SessionLeaseValidatorTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly Guid SessionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public void Validate_AcceptsValidLeaseSignedByBackendKey()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key.ExportSubjectPublicKeyInfoPem());
        var lease = CreateSignedLease(key, DeviceId, Now.AddMinutes(15));

        var result = validator.Validate(lease);

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Validate_RejectsWrongDeviceId()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key.ExportSubjectPublicKeyInfoPem());
        var lease = CreateSignedLease(key, Guid.Parse("eeeeeeee-eeee-4eee-eeee-eeeeeeeeeeee"), Now.AddMinutes(15));

        var result = validator.Validate(lease);

        Assert.False(result.IsValid);
        Assert.Contains("device", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsExpiredLease()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key.ExportSubjectPublicKeyInfoPem());
        var lease = CreateSignedLease(key, DeviceId, Now.AddSeconds(-1));

        var result = validator.Validate(lease);

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsModifiedSignature()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var validator = CreateValidator(key.ExportSubjectPublicKeyInfoPem());
        var lease = CreateSignedLease(key, DeviceId, Now.AddMinutes(15));
        var signature = Convert.FromBase64String(lease.Signature);
        signature[0] ^= 0xFF;

        var result = validator.Validate(lease with { Signature = Convert.ToBase64String(signature) });

        Assert.False(result.IsValid);
        Assert.Contains("signature", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static SessionLeaseValidator CreateValidator(string publicKeyPem)
    {
        return new SessionLeaseValidator(
            Options.Create(new AgentOptions
            {
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                DeviceId = DeviceId,
                LeaseSigningPublicKeyPem = publicKeyPem
            }),
            new FixedTimeProvider(Now));
    }

    private static SessionLeaseDto CreateSignedLease(ECDsa key, Guid deviceId, DateTimeOffset expiresAtUtc)
    {
        var unsigned = new SessionLeaseDto(
            SessionId,
            OrganizationId,
            BranchId,
            SeatId,
            deviceId,
            SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: Now,
            ExpiresAtUtc: expiresAtUtc,
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: string.Empty);
        var payload = SessionLeasePayloadCanonicalizer.CreatePayload(unsigned);
        var signature = key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256);

        return unsigned with { Signature = Convert.ToBase64String(signature) };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
