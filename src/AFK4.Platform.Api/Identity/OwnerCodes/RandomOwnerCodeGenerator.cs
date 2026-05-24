using System.Security.Cryptography;

namespace AFK4.Platform.Api.Identity.OwnerCodes;

public sealed class RandomOwnerCodeGenerator : IOwnerCodeGenerator
{
    public const int Digits = 8;

    private const int UpperExclusive = 100_000_000;

    public string Generate()
    {
        var value = RandomNumberGenerator.GetInt32(0, UpperExclusive);
        return value.ToString("D" + Digits, System.Globalization.CultureInfo.InvariantCulture);
    }
}
