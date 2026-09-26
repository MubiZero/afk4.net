namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>
/// Условия оплаты для клубов (спека тарифов клуба, §3–§5): сколько длится пробный период,
/// обещанный платёж и льгота после срока счёта до перехода на бесплатный тариф. Задаёт платформа.
/// </summary>
public sealed record BillingTermsDto(
    int TrialDays,
    int PromisedPaymentDays,
    int FallbackAfterOverdueDays,
    // Пусто — условия ещё не меняли, действуют значения по умолчанию.
    DateTimeOffset? UpdatedAtUtc);

public sealed record UpdateBillingTermsRequest(int TrialDays, int PromisedPaymentDays, int FallbackAfterOverdueDays);

public static class BillingTermsLimits
{
    public const int MaxTrialDays = 90;

    public const int MaxPromisedPaymentDays = 30;

    public const int MaxFallbackAfterOverdueDays = 60;
}

public static class BillingTermsRoutes
{
    public const string Terms = "/api/platform/billing/terms";
}
