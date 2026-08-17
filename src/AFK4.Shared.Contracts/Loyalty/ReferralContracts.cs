namespace AFK4.Shared.Contracts.Loyalty;

/// <summary>Настройки «приведи друга» глазами клуба.</summary>
public sealed record ReferralSettingsDto(
    bool Enabled,
    long ReferrerBonusMinorUnits,
    long InviteeBonusMinorUnits,
    long MinimumTopUpMinorUnits,
    int ClaimWindowDays,
    int MaxRewardedPerReferrer);

public sealed record UpdateReferralSettingsRequest(
    bool Enabled,
    long ReferrerBonusMinorUnits,
    long InviteeBonusMinorUnits,
    long MinimumTopUpMinorUnits,
    int ClaimWindowDays,
    int MaxRewardedPerReferrer);

/// <summary>
/// Экран «Приведи друга» глазами игрока: свой код, условия и что уже вышло.
///
/// Суммы и условия приходят с сервера, а не зашиты в приложение: их назначает клуб, и каждый
/// назначает свои.
/// </summary>
public sealed record PlayerReferralDto(
    bool Enabled,
    string? Code,
    long ReferrerBonusMinorUnits,
    long InviteeBonusMinorUnits,
    long MinimumTopUpMinorUnits,
    string CurrencyCode,
    int InvitedCount,
    int RewardedCount,
    long EarnedMinorUnits,
    /// <summary>Игрок сам пришёл по чужому коду — второй раз назвать код нельзя.</summary>
    bool HasClaimedCode,
    /// <summary>Назвать код ещё можно: приглашение не использовано и окно не закрылось.</summary>
    bool CanClaimCode);

public sealed record ClaimReferralCodeRequest(string Code);
