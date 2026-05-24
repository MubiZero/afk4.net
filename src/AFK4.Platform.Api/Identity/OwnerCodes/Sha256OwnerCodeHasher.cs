using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Identity.OwnerCodes;

public sealed class Sha256OwnerCodeHasher : IOwnerCodeHasher
{
    public string Normalize(string rawCode)
    {
        if (string.IsNullOrEmpty(rawCode))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(rawCode.Length);
        foreach (var character in rawCode)
        {
            if (character >= '0' && character <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    public string Hash(string normalizedCode)
    {
        var bytes = Encoding.ASCII.GetBytes(normalizedCode);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string Suffix(string normalizedCode)
    {
        if (normalizedCode.Length <= 4)
        {
            return normalizedCode.PadLeft(4, '0');
        }

        return normalizedCode[^4..];
    }
}
