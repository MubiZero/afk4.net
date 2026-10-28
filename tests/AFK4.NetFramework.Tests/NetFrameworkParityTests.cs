using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Shell;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.NetFramework.Tests;

/// <summary>
/// Сборка для Windows 7 должна понимать то же, что пишет .NET 10: ключи ПК и стойки, подписи, даты в JSON, сжатые
/// части обновлений. Эталоны сняты с настоящего .NET 10 (ключ P-256, подпись, Brotli, JSON) и вписаны сюда как есть.
/// </summary>
public sealed class NetFrameworkParityTests
{
    private const string Sec1Pem = "-----BEGIN EC PRIVATE KEY-----\nMHcCAQEEIInS1gIJ3L1cVYCs64NxgaGKfKoG8lwkQiklt4BEfz8ioAoGCCqGSM49\nAwEHoUQDQgAEsfOcxeGZxfEKthZuCjGUUsaLKjX7+/UmTJK1SQhubdL6qJo80q66\nsrsVcAaJRgMJj2gUSFLdZej49fG/z48n3w==\n-----END EC PRIVATE KEY-----";
    private const string SpkiPem = "-----BEGIN PUBLIC KEY-----\nMFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEsfOcxeGZxfEKthZuCjGUUsaLKjX7\n+/UmTJK1SQhubdL6qJo80q66srsVcAaJRgMJj2gUSFLdZej49fG/z48n3w==\n-----END PUBLIC KEY-----";
    private const string Pkcs8 = "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgidLWAgncvVxVgKzrg3GBoYp8qgbyXCRCKSW3gER/PyKhRANCAASx85zF4ZnF8Qq2Fm4KMZRSxosqNfv79SZMkrVJCG5t0vqomjzSrrqyuxVwBolGAwmPaBRIUt1l6Pj18b/Pjyff";
    private const string Signature = "cRE6btvvcr/9PCMez5tOgWI40EYYnk3UBV09ml5mS2a68zSbw4WTK5hdEtTh8oXXWStjY2bjFZAZ6xC2KUeHnw==";
    private const string Brotli = "G68EAARkcqTlgUArgoCzViKvbDGwKhTqndn5L0WYsNwnrWI=";
    private static readonly byte[] SignedPayload = Encoding.UTF8.GetBytes("afk4 net48 parity");

    [Fact]
    public void KeysWrittenByNet10ReadAndWriteBackByteForByte()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        key.ImportFromPem(Sec1Pem);

        Assert.Equal(Sec1Pem, key.ExportECPrivateKeyPem());
        Assert.Equal(SpkiPem, key.ExportSubjectPublicKeyInfoPem());
        Assert.Equal(Pkcs8, Convert.ToBase64String(key.ExportPkcs8PrivateKey()));

        using var fromPkcs8 = ECDsa.Create();
        fromPkcs8.ImportPkcs8PrivateKey(Convert.FromBase64String(Pkcs8), out var read);
        Assert.Equal(Convert.FromBase64String(Pkcs8).Length, read);
        Assert.Equal(SpkiPem, fromPkcs8.ExportSubjectPublicKeyInfoPem());
    }

    [Fact]
    public void SignatureMadeByNet10VerifiesWithThePublicPem()
    {
        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(SpkiPem);

        Assert.True(verifier.VerifyData(SignedPayload, Convert.FromBase64String(Signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.False(verifier.VerifyData(Encoding.UTF8.GetBytes("tampered"), Convert.FromBase64String(Signature), HashAlgorithmName.SHA256));
    }

    [Fact]
    public void ForeignKeyLayoutsAreRefusedRatherThanMisread()
    {
        using var key = ECDsa.Create();
        Assert.Throws<CryptographicException>(() => key.ImportFromPem("-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----"));
    }

    [Fact]
    public void DatesAndTimesInContractsUseTheNet10Json()
    {
        var json = JsonSerializer.Serialize(new { D = new DateOnly(2026, 10, 29), T = new TimeOnly(8, 30), T2 = TimeOnly.FromTimeSpan(new TimeSpan(0, 23, 59, 59, 500)) });
        Assert.Equal("{\"D\":\"2026-10-29\",\"T\":\"08:30:00\",\"T2\":\"23:59:59.5000000\"}", json);

        var preference = JsonSerializer.Deserialize<OrganizationAdminUpdatePreferenceDto>(
            "{\"organizationId\":\"00000000-0000-0000-0000-000000000001\",\"branchId\":\"00000000-0000-0000-0000-000000000002\",\"maintenanceWindowStart\":\"02:00:00\",\"maintenanceWindowEnd\":\"06:00:00\",\"timeZone\":\"Asia/Dushanbe\"}",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(new TimeOnly(2, 0), preference.MaintenanceWindowStart);
    }

    [Fact]
    public void ClubTimeZoneByIanaNameWorksWithoutIcu()
    {
        var preference = new OrganizationAdminUpdatePreferenceDto(Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(2, 0), new TimeOnly(6, 0), "Asia/Dushanbe");
        // 22:30 UTC — 03:30 в Душанбе (UTC+5): внутри окна обслуживания.
        Assert.True(OrganizationAdminUpdateReadiness.IsInsideWindow(preference, new DateTimeOffset(2026, 10, 28, 22, 30, 0, TimeSpan.Zero)));
        Assert.False(OrganizationAdminUpdateReadiness.IsInsideWindow(preference, new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void UpdatePartsCompressedByNet10BrotliUnpack()
    {
        using var compressed = new MemoryStream(Convert.FromBase64String(Brotli));
        using var brotli = new BrotliSharpLib.BrotliStream(compressed, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(brotli, Encoding.UTF8);

        Assert.Equal(string.Concat(Enumerable.Repeat("Привет, клуб! ", 50)), reader.ReadToEnd());
    }

    [Fact]
    public async Task ShellPipeFramesRoundTrip()
    {
        using var stream = new MemoryStream();
        var message = new ShellPipeMessage(ShellPipeMessageTypeNames.Hello, Hello: new ShellPipeHelloDto(ShellPipeProtocol.Version, "ПК 07", 1));
        await ShellPipeCodec.WriteAsync(stream, message, CancellationToken.None);
        stream.Position = 0;

        var read = await ShellPipeCodec.ReadAsync(stream, CancellationToken.None);
        Assert.Equal(ShellPipeMessageTypeNames.Hello, read!.Type);
        Assert.Null(await ShellPipeCodec.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public void ReplaceIgnoringCaseMatchesNet10()
    {
        Assert.Equal("C:\\AFK4\\bin", "%ROOT%\\bin".Replace("%root%", "C:\\AFK4", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("aXbXc", "a--b--c".Replace("--", "X", StringComparison.Ordinal));
    }
}
