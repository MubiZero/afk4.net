namespace AFK4.Platform.Api.Data;

/// <summary>
/// Условия оплаты для клубов — одна строка на платформу. Нет строки — действуют значения по
/// умолчанию из <see cref="AFK4.Shared.Contracts.Platform.Billing.ClubPlanLimits"/>.
/// </summary>
public sealed class PlatformBillingTermsEntity
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public int TrialDays { get; set; }

    public int PromisedPaymentDays { get; set; }

    public int FallbackAfterOverdueDays { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid? UpdatedByPlatformAdminUserId { get; set; }
}
