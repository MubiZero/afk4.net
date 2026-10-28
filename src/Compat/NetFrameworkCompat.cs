#if NETFRAMEWORK
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace System;

/// <summary>
/// Чего нет ни в .NET Framework 4.8, ни в пакете Polyfill (docs/roadmap/windows7.md). Подключается к каждой
/// сборке под net48 из Directory.Build.props; под net10.0 этих членов нет, там работают настоящие.
/// </summary>
internal static class NetFrameworkCompat
{
    /// <summary>
    /// Windows 7 сама предлагает по HTTPS только TLS 1.0, а Cloudflare перед сервером требует 1.2 — просим его явно.
    /// На Windows 8 и новее версию выбирает система (там и TLS 1.3). Зовётся в начале каждой программы на ПК.
    /// </summary>
    public static void UseTls12OnWindows7()
    {
        if (Environment.OSVersion.Version < new Version(6, 2))
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
        }
    }

    extension(string)
    {
        /// <summary><c>string.Create(CultureInfo.InvariantCulture, $"…")</c> — строка в заданной культуре.</summary>
        public static string Create(IFormatProvider? provider, FormattableString text) => text.ToString(provider);
    }

    extension(string text)
    {
        public string Replace(string oldValue, string? newValue, StringComparison comparison)
        {
            if (oldValue.Length == 0)
            {
                throw new ArgumentException("The string to replace must not be empty.", nameof(oldValue));
            }

            var result = new StringBuilder(text.Length);
            var start = 0;
            int found;
            while ((found = text.IndexOf(oldValue, start, comparison)) >= 0)
            {
                result.Append(text, start, found - start).Append(newValue);
                start = found + oldValue.Length;
            }

            return result.Append(text, start, text.Length - start).ToString();
        }
    }

    extension(Environment)
    {
        /// <summary>Миллисекунды с загрузки Windows, без переполнения через 49 дней.</summary>
        public static long TickCount64 => (long)GetTickCount64();
    }

    extension(Process process)
    {
        public bool WaitForExit(TimeSpan timeout) => process.WaitForExit((int)Math.Min(int.MaxValue, timeout.TotalMilliseconds));
    }

    extension(Task)
    {
        public static Task Delay(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken) =>
            timeProvider.Delay(delay, cancellationToken);
    }

    extension(Marshal)
    {
        public static int GetLastPInvokeError() => Marshal.GetLastWin32Error();
    }

    extension(PhysicalAddress)
    {
        public static bool TryParse(string? text, out PhysicalAddress? address)
        {
            try
            {
                address = text is null ? null : PhysicalAddress.Parse(text.ToUpperInvariant().Replace(':', '-'));
                return address is not null;
            }
            catch (FormatException)
            {
                address = null;
                return false;
            }
        }
    }

    extension<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        public bool TryRemove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(item);
    }

    extension(ECDsa ecdsa)
    {
        /// <summary>
        /// Ключ P-256 в PEM: открытый («PUBLIC KEY», SubjectPublicKeyInfo) или закрытый («EC PRIVATE KEY», SEC 1) — только
        /// такие и подписывают обновления, аренды и заявки ПК. У P-256 оба формата — неизменный заголовок и числа ключа,
        /// поэтому разбираются по точной раскладке, которую пишет .NET 10.
        /// </summary>
        public void ImportFromPem(string pem)
        {
            if (pem.Contains("EC PRIVATE KEY"))
            {
                var der = PemBody(pem, "EC PRIVATE KEY");
                RequireLayout(der, Sec1Prefix, Sec1Middle, Sec1PrivateLength);
                ecdsa.ImportParameters(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    D = Slice(der, Sec1Prefix.Length, 32),
                    Q = Point(der, Sec1Prefix.Length + 32 + Sec1Middle.Length)
                });
                return;
            }

            var spki = PemBody(pem, "PUBLIC KEY");
            RequireLayout(spki, P256SpkiHeader, [], 0);
            ecdsa.ImportParameters(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = Point(spki, P256SpkiHeader.Length) });
        }

        public string ExportSubjectPublicKeyInfoPem()
        {
            var parameters = ecdsa.ExportParameters(includePrivateParameters: false);
            return Pem("PUBLIC KEY", [.. P256SpkiHeader, 0x04, .. parameters.Q.X!, .. parameters.Q.Y!]);
        }

        public string ExportECPrivateKeyPem()
        {
            var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
            return Pem("EC PRIVATE KEY", [.. Sec1Prefix, .. parameters.D!, .. Sec1Middle, 0x04, .. parameters.Q.X!, .. parameters.Q.Y!]);
        }

        /// <summary>Закрытый ключ P-256 в PKCS#8 — ключ стойки Панели; раскладка та же, что пишет .NET 10.</summary>
        public void ImportPkcs8PrivateKey(byte[] source, out int bytesRead)
        {
            RequireLayout(source, Pkcs8Prefix, Pkcs8Middle, Sec1PrivateLength);
            ecdsa.ImportParameters(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = Slice(source, Pkcs8Prefix.Length, 32),
                Q = Point(source, Pkcs8Prefix.Length + 32 + Pkcs8Middle.Length)
            });
            bytesRead = source.Length;
        }

        public byte[] ExportPkcs8PrivateKey()
        {
            var parameters = ecdsa.ExportParameters(includePrivateParameters: true);
            return [.. Pkcs8Prefix, .. parameters.D!, .. Pkcs8Middle, 0x04, .. parameters.Q.X!, .. parameters.Q.Y!];
        }

        /// <summary>В 4.8 подпись всегда в формате IEEE P1363 — его и принимаем.</summary>
        public bool VerifyData(byte[] data, byte[] signature, HashAlgorithmName hashAlgorithm, DSASignatureFormat signatureFormat) =>
            signatureFormat == DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                ? ecdsa.VerifyData(data, signature, hashAlgorithm)
                : throw new NotSupportedException("Only IEEE P1363 ECDSA signatures are supported on .NET Framework.");
    }

    /// <summary>Ключ в Exception.Data, под которым лежит код ответа HTTP (у HttpRequestException в 4.8 его нет).</summary>
    public const string StatusCodeKey = "AFK4.HttpStatusCode";

    extension(HttpRequestException exception)
    {
        public System.Net.HttpStatusCode? StatusCode => exception.Data[StatusCodeKey] as System.Net.HttpStatusCode?;
    }

    extension(HMACSHA256)
    {
        public static byte[] HashData(byte[] key, byte[] source)
        {
            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(source);
        }
    }

    // SEQUENCE { SEQUENCE { id-ecPublicKey, prime256v1 }, BIT STRING (66 байт: 00 и 65 байт точки) }.
    private static readonly byte[] P256SpkiHeader =
    [
        0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01,
        0x06, 0x08, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07, 0x03, 0x42, 0x00
    ];

    // SEQUENCE { INTEGER 1, OCTET STRING d (32), [0] prime256v1, [1] BIT STRING (00 и точка) } — как пишет .NET.
    private static readonly byte[] Sec1Prefix = [0x30, 0x77, 0x02, 0x01, 0x01, 0x04, 0x20];
    private static readonly byte[] Sec1Middle =
        [0xa0, 0x0a, 0x06, 0x08, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07, 0xa1, 0x44, 0x03, 0x42, 0x00];
    private const int Sec1PrivateLength = 32;

    // PKCS#8: SEQUENCE { 0, AlgorithmIdentifier (id-ecPublicKey, prime256v1), OCTET STRING { ECPrivateKey без [0] } }.
    private static readonly byte[] Pkcs8Prefix =
    [
        0x30, 0x81, 0x87, 0x02, 0x01, 0x00, 0x30, 0x13, 0x06, 0x07, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01,
        0x06, 0x08, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07, 0x04, 0x6d, 0x30, 0x6b, 0x02, 0x01, 0x01, 0x04, 0x20
    ];
    private static readonly byte[] Pkcs8Middle = [0xa1, 0x44, 0x03, 0x42, 0x00];

    private static byte[] PemBody(string pem, string label)
    {
        var body = pem.Replace($"-----BEGIN {label}-----", string.Empty).Replace($"-----END {label}-----", string.Empty);
        return Convert.FromBase64String(string.Concat(body.Where(c => !char.IsWhiteSpace(c))));
    }

    private static void RequireLayout(byte[] der, byte[] prefix, byte[] middle, int privateLength)
    {
        var expected = prefix.Length + privateLength + middle.Length + 65;
        if (der.Length != expected
            || !der.Take(prefix.Length).SequenceEqual(prefix)
            || !der.Skip(prefix.Length + privateLength).Take(middle.Length).SequenceEqual(middle)
            || der[prefix.Length + privateLength + middle.Length] != 0x04)
        {
            throw new CryptographicException("Only P-256 keys in the layout .NET writes are supported on .NET Framework.");
        }
    }

    private static ECPoint Point(byte[] der, int at) => new() { X = Slice(der, at + 1, 32), Y = Slice(der, at + 33, 32) };

    private static byte[] Slice(byte[] source, int start, int length) => source.Skip(start).Take(length).ToArray();

    private static string Pem(string label, byte[] der)
    {
        var base64 = Convert.ToBase64String(der);
        var lines = Enumerable.Range(0, (base64.Length + 63) / 64).Select(i => base64.Substring(i * 64, Math.Min(64, base64.Length - i * 64)));
        return $"-----BEGIN {label}-----\n{string.Join("\n", lines)}\n-----END {label}-----";
    }

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
#endif
