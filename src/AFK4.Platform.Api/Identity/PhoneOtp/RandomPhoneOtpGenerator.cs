using System.Globalization;
using System.Security.Cryptography;

namespace AFK4.Platform.Api.Identity.PhoneOtp;

public sealed class RandomPhoneOtpGenerator : IPhoneOtpGenerator
{
    public const int Digits = 6;
    private const int UpperExclusive = 1_000_000;

    public string Generate()
    {
        var value = RandomNumberGenerator.GetInt32(0, UpperExclusive);
        return value.ToString("D" + Digits, CultureInfo.InvariantCulture);
    }
}
