namespace AFK4.Platform.Api.Identity.PhoneOtp;

public interface IPhoneOtpHasher
{
    /// <summary>SHA-256 hex (lowercase) of the numeric code. Codes are stored hashed, never plaintext.</summary>
    string Hash(string code);
}
