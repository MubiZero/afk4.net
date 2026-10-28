#if NETFRAMEWORK
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace System;

/// <summary>
/// Чего нет ни в .NET Framework 4.8, ни в пакете Polyfill (docs/roadmap/netfx48.md). Подключается к каждой
/// сборке под net48 из Directory.Build.props; под net10.0 этих членов нет, там работают настоящие.
/// </summary>
internal static class NetFrameworkCompat
{
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
        /// Открытый ключ P-256 в PEM («BEGIN PUBLIC KEY»): только такие ключи и подписывают обновления и аренды.
        /// В 4.8 нет разбора SubjectPublicKeyInfo — у P-256 он всегда один и тот же заголовок и точка 04‖X‖Y.
        /// </summary>
        public void ImportFromPem(string pem)
        {
            var body = pem
                .Replace("-----BEGIN PUBLIC KEY-----", string.Empty)
                .Replace("-----END PUBLIC KEY-----", string.Empty);
            var der = Convert.FromBase64String(string.Concat(body.Where(c => !char.IsWhiteSpace(c))));
            if (der.Length != P256SpkiHeader.Length + 65 || !der.Take(P256SpkiHeader.Length).SequenceEqual(P256SpkiHeader))
            {
                throw new CryptographicException("Only P-256 public keys in SubjectPublicKeyInfo PEM are supported.");
            }

            ecdsa.ImportParameters(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = der.Skip(P256SpkiHeader.Length + 1).Take(32).ToArray(),
                    Y = der.Skip(P256SpkiHeader.Length + 33).Take(32).ToArray()
                }
            });
        }

        /// <summary>В 4.8 подпись всегда в формате IEEE P1363 — его и принимаем.</summary>
        public bool VerifyData(byte[] data, byte[] signature, HashAlgorithmName hashAlgorithm, DSASignatureFormat signatureFormat) =>
            signatureFormat == DSASignatureFormat.IeeeP1363FixedFieldConcatenation
                ? ecdsa.VerifyData(data, signature, hashAlgorithm)
                : throw new NotSupportedException("Only IEEE P1363 ECDSA signatures are supported on .NET Framework.");
    }

    // SEQUENCE { SEQUENCE { id-ecPublicKey, prime256v1 }, BIT STRING (66 байт: 00 и 65 байт точки) }.
    private static readonly byte[] P256SpkiHeader =
    [
        0x30, 0x59, 0x30, 0x13, 0x06, 0x07, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x02, 0x01,
        0x06, 0x08, 0x2a, 0x86, 0x48, 0xce, 0x3d, 0x03, 0x01, 0x07, 0x03, 0x42, 0x00
    ];

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();
}
#endif
