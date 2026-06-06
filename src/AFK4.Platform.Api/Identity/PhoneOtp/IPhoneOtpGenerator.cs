namespace AFK4.Platform.Api.Identity.PhoneOtp;

public interface IPhoneOtpGenerator
{
    /// <summary>A cryptographically-random 6-digit numeric code, zero-padded.</summary>
    string Generate();
}
