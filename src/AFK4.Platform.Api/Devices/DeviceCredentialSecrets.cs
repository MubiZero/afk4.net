using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Devices;

internal static class DeviceCredentialSecrets
{
    public static string CreateEnrollmentCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var value = Convert.ToHexString(bytes);
        return $"AFK4-{value[..4]}-{value[4..]}";
    }

    public static string NormalizeEnrollmentCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    public static string CreateCredentialSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var value = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"afk4_{value}";
    }

    public static byte[] HashSecret(string secret)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public static bool SecretMatches(byte[] expectedHash, string presentedSecret)
    {
        var presentedHash = HashSecret(presentedSecret);
        return CryptographicOperations.FixedTimeEquals(expectedHash, presentedHash);
    }
}
