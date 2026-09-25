using System.Globalization;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Tips;

/// <summary>
/// Чаевые администратору смены с экрана итога (спека `2026-09-25-visit-tips-design.md`). Клуб их
/// включает сам; деньги уходят с кошелька игрока записью журнала <c>tip</c> и выручкой не считаются.
/// </summary>
public sealed record TipSettingsDto(bool Enabled);

public sealed record UpdateTipSettingsRequest(bool Enabled);

/// <summary>Можно ли оставить чаевые за этот визит — и сколько.</summary>
public sealed record PlayerTipOfferDto(
    bool Available,
    // Одно из TipUnavailableReasonNames; пусто, если можно.
    string? UnavailableReason,
    IReadOnlyList<MoneyDto> Presets,
    MoneyDto Balance,
    // Имя администратора смены — первое слово: «Чаевые Шерзоду».
    string? RecipientName,
    // Чаевые, уже оставленные за этот визит.
    MoneyDto? Given);

public sealed record PlayerTipRequest(MoneyDto Amount, string IdempotencyKey);

public sealed record PlayerTipResponse(MoneyDto Amount, MoneyDto BalanceAfter, string? RecipientName);

/// <summary>Чаевые смены для Панели. Имени игрока нет: администратору важны сумма и ПК.</summary>
public sealed record ShiftTipsDto(
    Guid ShiftId,
    Guid RecipientStaffUserId,
    string RecipientName,
    MoneyDto Total,
    IReadOnlyList<ShiftTipDto> Tips,
    // Уже выдано из кассы за эту смену: выдать ту же сумму второй раз нельзя.
    MoneyDto? PaidOut = null);

public sealed record PayOutShiftTipsRequest(string IdempotencyKey);

public sealed record ShiftTipDto(
    Guid LedgerEntryId,
    MoneyDto Amount,
    string? SeatLabel,
    DateTimeOffset CreatedAtUtc,
    // Возвращены игроку — в сумму смены не входят.
    bool Reversed);

public static class TipUnavailableReasonNames
{
    public const string Disabled = "disabled";

    /// <summary>В филиале нет открытой смены — деньги некому отдать.</summary>
    public const string NoShift = "no_shift";

    public const string NotEnded = "not_ended";

    public const string TooLate = "too_late";

    public const string AlreadyTipped = "already_tipped";

    public const string NotEnoughBalance = "not_enough_balance";

    public const string InvalidAmount = "invalid_amount";
}

public static class TipErrorCodeNames
{
    /// <summary>Вернуть чаевые можно только из открытой смены.</summary>
    public const string ShiftClosed = "tip_shift_closed";

    public const string AlreadyReversed = "tip_already_reversed";

    /// <summary>Всё, что пришло за смену, уже выдано.</summary>
    public const string NothingToPay = "tip_nothing_to_pay";
}

public static class TipLimits
{
    /// <summary>Суммы чаевых в минорных единицах валюты кошелька: 5, 10 и 20.</summary>
    public static readonly IReadOnlyList<long> PresetMinorUnits = [500, 1000, 2000];

    /// <summary>Сколько после конца сессии можно оставить чаевые.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(2);
}

public static class TipRoutes
{
    public static string Visit(Guid sessionId) => string.Create(CultureInfo.InvariantCulture, $"/api/me/visits/{sessionId:D}/tip");
}
