using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Loyalty;

/// <summary>Коды отказа. Машинные: их переводят интерфейсы, а не сервер.</summary>
public static class ReferralErrorCodes
{
    public const string Disabled = "referral_disabled";
    public const string UnknownCode = "referral_unknown_code";
    public const string OwnCode = "referral_own_code";
    public const string AlreadyClaimed = "referral_already_claimed";
    public const string WindowClosed = "referral_window_closed";
}

public sealed record ReferralClaimOutcome(bool Succeeded, string? ErrorCode, string? ReferrerDisplayName)
{
    public static ReferralClaimOutcome Ok(string referrerDisplayName) => new(true, null, referrerDisplayName);

    public static ReferralClaimOutcome Fail(string errorCode) => new(false, errorCode, null);
}
