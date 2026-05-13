using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Billing;

public static class BillingCommandIdempotencyKeyHasher
{
    public static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();
    }
}
