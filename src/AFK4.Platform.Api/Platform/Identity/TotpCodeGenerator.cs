using System.Security.Cryptography;

namespace AFK4.Platform.Api.Platform.Identity;

/// <summary>
/// RFC 6238 TOTP (time-based one-time password) implementation without external dependencies.
/// Pure: no clock, no config, no I/O — time is always passed in explicitly by the caller.
/// </summary>
public static class TotpCodeGenerator
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int DefaultStep = 30;
    private const int DefaultDigits = 6;

    public static string Generate(byte[] secret, long unixTimeSeconds, int step = DefaultStep, int digits = DefaultDigits)
    {
        var counter = unixTimeSeconds / step;

        var counterBytes = new byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        var hash = HMACSHA1.HashData(secret, counterBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        var otp = binaryCode % (int)Math.Pow(10, digits);

        return otp.ToString().PadLeft(digits, '0');
    }

    public static bool Verify(byte[] secret, string code, long unixTimeSeconds, int allowedDriftSteps = 1)
    {
        var codeBytes = System.Text.Encoding.ASCII.GetBytes(code);

        for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
        {
            var candidate = Generate(secret, unixTimeSeconds + drift * DefaultStep, DefaultStep, DefaultDigits);
            var candidateBytes = System.Text.Encoding.ASCII.GetBytes(candidate);

            if (candidateBytes.Length == codeBytes.Length &&
                CryptographicOperations.FixedTimeEquals(candidateBytes, codeBytes))
            {
                return true;
            }
        }

        return false;
    }

    public static string ToBase32(byte[] secret)
    {
        if (secret.Length == 0)
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder((secret.Length * 8 + 4) / 5);

        int buffer = 0;
        int bitsLeft = 0;

        foreach (var b in secret)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                var index = (buffer >> bitsLeft) & 0x1F;
                result.Append(Base32Alphabet[index]);
            }
        }

        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 0x1F;
            result.Append(Base32Alphabet[index]);
        }

        return result.ToString();
    }
}
