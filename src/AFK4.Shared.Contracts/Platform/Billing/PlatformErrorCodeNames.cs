namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>
/// Машинные имена отказов платформенного контура.
///
/// Причина та же, по которой они появились у офбординга (<see
/// cref="Organizations.OffboardingErrorCodes"/>): текст отказа сервер пишет по-английски и
/// именами своих полей («CurrentPeriodEndUtc must be later than…»), а панель работает на трёх
/// языках. Показать такой текст нельзя, и без кода панель показывала одно «не удалось сохранить
/// изменения» на десяток разных причин — в форме подписки из семи полей человек не мог понять,
/// какое из них поправить.
///
/// Здесь только те причины, которые человек за панелью исправляет сам. Остальное остаётся без
/// кода и честно называется общими словами.
/// </summary>
public static class PlatformErrorCodeNames
{
    // --- Счета -----------------------------------------------------------------------------

    /// <summary>Счёт за текущий период уже выставлен.</summary>
    public const string InvoicePeriodAlreadyBilled = "invoice_period_already_billed";

    /// <summary>Счёт уже оплачен.</summary>
    public const string InvoiceAlreadyPaid = "invoice_already_paid";

    /// <summary>Счёт уже аннулирован.</summary>
    public const string InvoiceAlreadyVoid = "invoice_already_void";

    /// <summary>Оплаченный счёт не аннулируют — на него выписывают кредит-ноту.</summary>
    public const string PaidInvoiceCannotBeVoided = "paid_invoice_cannot_be_voided";

    /// <summary>Кредит-ноту не оплачивают: она учитывается в балансе.</summary>
    public const string CreditNoteNotPayable = "credit_note_not_payable";

    /// <summary>Номер счёта занял параллельный запрос — можно повторить.</summary>
    public const string InvoiceNumberingConflict = "invoice_numbering_conflict";

    // --- Подписка ---------------------------------------------------------------------------

    /// <summary>Конец оплаченного периода не позже его начала.</summary>
    public const string SubscriptionPeriodEndNotAfterStart = "subscription_period_end_not_after_start";

    /// <summary>Отсрочку ставят на будущее: прошедшая дата ничего не отсрочит.</summary>
    public const string SubscriptionGraceNotInFuture = "subscription_grace_not_in_future";

    /// <summary>Перевод на пробный период без даты его окончания.</summary>
    public const string SubscriptionTrialNeedsPeriodEnd = "subscription_trial_needs_period_end";

    /// <summary>Выбранного тарифа нет в каталоге.</summary>
    public const string SubscriptionPlanNotFound = "subscription_plan_not_found";

    // --- Организации и филиалы ----------------------------------------------------------------

    /// <summary>Такой адрес организации уже занят другим клубом.</summary>
    public const string OrganizationSlugTaken = "organization_slug_taken";

    /// <summary>Такой адрес филиала уже занят в этой организации.</summary>
    public const string BranchSlugTaken = "branch_slug_taken";

    /// <summary>Логин владельца уже занят в этой организации.</summary>
    public const string OwnerUserNameTaken = "owner_username_taken";
}
