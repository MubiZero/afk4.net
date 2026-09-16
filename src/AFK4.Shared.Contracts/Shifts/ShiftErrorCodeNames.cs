namespace AFK4.Shared.Contracts.Shifts;

/// <summary>
/// Машинные имена отказов по сменам и кассе. См. <see cref="Tariffs.TariffErrorCodeNames"/> — та же
/// причина: у кассы эти отказы самые частые, а без кода до кассира доезжала английская фраза
/// сервера вместе с сырым телом ответа.
/// </summary>
public static class ShiftErrorCodeNames
{
    /// <summary>В филиале уже открыта смена — вторую открыть нельзя.</summary>
    public const string AlreadyOpen = "shift_already_open";

    /// <summary>Смену уже закрыли: скорее всего, это сделал сосед по кассе.</summary>
    public const string AlreadyClosed = "shift_already_closed";

    /// <summary>Валюта операции не совпадает с валютой смены.</summary>
    public const string CurrencyMismatch = "shift_currency_mismatch";

    /// <summary>Расхождение по кассе больше допуска — нужна подпись старшего.</summary>
    public const string SignOffRequired = "shift_sign_off_required";

    /// <summary>Подписать расхождение должен не тот, кто смену открыл или закрывает.</summary>
    public const string SignOffMustDiffer = "shift_sign_off_must_differ";

    /// <summary>У выбранного сотрудника нет права подписывать расхождение.</summary>
    public const string SignOffNotAuthorized = "shift_sign_off_not_authorized";

    /// <summary>Внесение и изъятие наличных возможны только при открытой смене.</summary>
    public const string CashMovementNeedsOpenShift = "cash_movement_needs_open_shift";
}
