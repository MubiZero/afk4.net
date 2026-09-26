namespace AFK4.Shared.Contracts.Loyalty;

/// <summary>Подарок на день рождения: сумма на баланс в сам день и кому он положен.</summary>
public sealed record BirthdayGiftSettingsDto(
    bool Enabled,
    long AmountMinorUnits,
    /// Только тем, кто был в клубе за столько дней; 0 — всем.
    int RecentVisitDays);

public sealed record UpdateBirthdayGiftSettingsRequest(
    bool Enabled,
    long AmountMinorUnits,
    int RecentVisitDays);
