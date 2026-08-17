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

public interface IReferralService
{
    /// <summary>Настройки, если программа действительно работает; иначе null.</summary>
    Task<OrganizationReferralSettingsEntity?> GetActiveSettingsAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    /// <summary>Код игрока; заводит его, если ещё не заводили.</summary>
    Task<string> EnsureCodeAsync(Guid playerAccountId, CancellationToken cancellationToken);

    /// <summary>Приглашённый называет код друга.</summary>
    Task<ReferralClaimOutcome> ClaimAsync(
        Guid inviteePlayerAccountId,
        string code,
        CancellationToken cancellationToken);

    /// <summary>
    /// Записи бонуса, если это пополнение закрывает приглашение. Возвращает их, а не сохраняет:
    /// деньги за друга обязаны лечь в ту же транзакцию, что и само пополнение, — иначе бонус
    /// однажды выживет без пополнения, которое его вызвало. Тем же приёмом устроен кешбэк.
    /// </summary>
    Task<IReadOnlyList<LedgerEntryEntity>> BuildTopUpRewardEntriesAsync(
        Guid organizationId,
        Guid branchId,
        Guid inviteePlayerAccountId,
        long topUpMinorUnits,
        string currencyCode,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);
}
