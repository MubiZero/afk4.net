using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Sessions;

public static class SessionCommandIdempotencyKeyHasher
{
    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
    }
}
