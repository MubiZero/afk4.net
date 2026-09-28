using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Identity.PhoneOtp;

public sealed class Sha256PhoneOtpHasher
{
    /// <summary>SHA-256 hex (lowercase) of the numeric code. Codes are stored hashed, never plaintext.</summary>
    public string Hash(string code)
    {
        var bytes = Encoding.ASCII.GetBytes(code);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
