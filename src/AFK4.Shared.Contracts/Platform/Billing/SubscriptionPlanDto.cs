namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record SubscriptionPlanDto(
    string PlanCode,
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    bool IsActive,
    int SortOrder,
    // Цена каждого ПК сверх включённых — у тарифа за ПК; у прочих ноль.
    long PricePerDeviceMinorUnits = 0,
    int IncludedDevices = 0,
    // Игровых ПК на весь клуб; пусто — без предела.
    int? MaxDevices = null,
    // Каждая функция платформы и включена ли она этим тарифом.
    IReadOnlyList<PlanFeatureDto>? Features = null,
    // Сколько клубов сейчас на этом тарифе — им «применить лимиты» при правке.
    int Clubs = 0);

public sealed record PlanFeatureDto(string FeatureKey, string Name, bool IsIncluded);
