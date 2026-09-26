using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>
/// Тариф клуба словами (спека `2026-09-25-club-plans-per-pc-design.md`): сколько ПК, сколько из них
/// платных, во что выйдет месяц и что клуб может сделать сам. Цену прежней сетки клуб не видит.
/// </summary>
public sealed record ClubPlanDto(
    string PlanCode,
    // Одно из ClubPlanKindNames
    string Kind,
    int Devices,
    int IncludedDevices,
    int BillableDevices,
    MoneyDto PricePerDevice,
    // Счёт за месяц при сегодняшнем числе ПК. У бесплатного и пробного — ноль.
    MoneyDto EstimatedMonthly,
    DateTimeOffset? TrialEndsAtUtc,
    bool TrialAvailable,
    bool CanSwitchToPerPc,
    bool PromisedPaymentAvailable,
    DateTimeOffset? PromisedPaymentUntilUtc,
    // Просроченное; пусто — долга нет.
    MoneyDto? Overdue,
    // «Приведи клуб»: код клуба и сколько бесплатных месяцев накоплено за приведённых.
    string? ReferralCode = null,
    int FreeMonths = 0,
    int ReferredClubs = 0,
    // ПК, на которых новые сессии не запускаются: сверх предела бесплатного тарифа (§5a).
    int DevicesOutsidePlan = 0,
    // Когда клуб перейдёт на бесплатный тариф, если не оплатит просроченное. Пусто — не грозит.
    DateTimeOffset? FallbackAtUtc = null);

/// <summary>Игровые ПК клуба глазами тарифа: какие работают на бесплатном и какие отметил владелец.</summary>
public sealed record ClubPlanDevicesDto(
    // Предел ПК на клуб; пусто — у тарифа предела нет, работают все.
    int? Limit,
    IReadOnlyList<ClubPlanDeviceDto> Devices);

public sealed record ClubPlanDeviceDto(
    Guid DeviceId,
    string Name,
    string BranchName,
    // Новые сессии на нём запускаются.
    bool Works,
    // Владелец отметил его работающим на бесплатном тарифе.
    bool Kept);

/// <summary>Какие ПК работают на бесплатном тарифе — не больше предела; пустой список снимает выбор.</summary>
public sealed record SetClubPlanDevicesRequest(IReadOnlyList<Guid> DeviceIds);

public static class ClubPlanKindNames
{
    public const string Free = "free";

    public const string PerPc = "per_pc";

    public const string Trial = "trial";

    /// <summary>Прежняя сетка тарифов: условия у поддержки, цену экран не показывает.</summary>
    public const string Legacy = "legacy";
}

public static class ClubPlanErrorCodeNames
{
    public const string TrialUsed = "plan_trial_used";

    /// <summary>Сначала оплатить просроченное — потом снова на тариф за ПК.</summary>
    public const string OverdueInvoices = "plan_overdue_invoices";

    public const string NothingToPromise = "plan_nothing_to_promise";

    public const string PromiseUsed = "plan_promise_used";

    public const string AlreadyOnPlan = "plan_already_on_plan";

    /// <summary>Отмечено больше ПК, чем разрешает тариф.</summary>
    public const string TooManyDevices = "plan_devices_too_many";

    /// <summary>В списке не игровой ПК клуба или неподтверждённый.</summary>
    public const string UnknownDevice = "plan_device_unknown";
}

public static class ClubPlanLimits
{
    public const int TrialDays = 30;

    public const int PromisedPaymentDays = 7;

    /// <summary>Сколько дней после срока оплаты клуб живёт на своём тарифе, прежде чем уйти на бесплатный.</summary>
    public const int FallbackAfterOverdueDays = 14;

    public const int FreeDevices = 10;

    public const long PricePerDeviceMinorUnits = 1000;
}
