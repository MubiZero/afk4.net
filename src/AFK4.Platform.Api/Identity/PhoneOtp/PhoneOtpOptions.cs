namespace AFK4.Platform.Api.Identity.PhoneOtp;

public sealed class PhoneOtpOptions
{
    public const string SectionName = "PhoneOtp";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxSendsPerHour { get; set; } = 5;
}
