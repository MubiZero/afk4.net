namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Токены, которые рантайм гарантированно передаёт каждому шаблону. Рендерер бросает исключение на
/// плейсхолдер без значения, поэтому лишний <c>{{токен}}</c> в теме или теле — это не «пустое место
/// в письме», а несостоявшаяся доставка. Где токен приходит не из всех мест формирования
/// (<c>player.sign_in_code</c> шлётся и при регистрации, без имени), здесь лежит пересечение.
/// </summary>
public static class NotificationTemplateTokens
{
    private static readonly string[] InvoiceTokens =
        ["displayName", "organizationName", "invoiceNumber", "amount", "currency", "dueDate", "paidDate", "daysOverdue"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByTemplateKey =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [NotificationTemplateKeys.Test] = [],
            [NotificationTemplateKeys.InvoiceIssued] = InvoiceTokens,
            [NotificationTemplateKeys.InvoicePaid] = InvoiceTokens,
            [NotificationTemplateKeys.InvoiceOverdue] = InvoiceTokens,
            [NotificationTemplateKeys.InvoiceDueSoon] = InvoiceTokens,
            [NotificationTemplateKeys.LowStock] =
                ["displayName", "organizationName", "branchName", "productName", "sku", "stockOnHand", "threshold"],
            [NotificationTemplateKeys.OwnerDailySummary] =
                ["displayName", "organizationName", "date", "revenue", "salesCount", "currency", "shiftsClosed",
                 "discrepancyCount", "discrepancyTotal", "refundTotal", "compCount", "compValueTotal",
                 "manualCorrectionTotal", "writeOffTotal"],
            [NotificationTemplateKeys.OrganizationOwnerInvite] = ["displayName", "code"],
            [NotificationTemplateKeys.PlatformAnnouncement] = ["displayName", "organizationName", "title", "body"],
            [NotificationTemplateKeys.PlayerBalanceToppedUp] = ["amount", "balance"],
            [NotificationTemplateKeys.PlayerOrderReady] = ["items", "seat"],
            [NotificationTemplateKeys.PlayerPhoneVerification] = ["code", "expiresInMinutes", "displayName"],
            [NotificationTemplateKeys.PlayerReservationSoon] = ["club", "time"],
            [NotificationTemplateKeys.PlayerSessionEnding] = ["minutes", "seat"],
            [NotificationTemplateKeys.PlayerSignInCode] = ["code", "expiresInMinutes"],
            [NotificationTemplateKeys.ScheduledReport] =
                ["displayName", "organizationName", "reportType", "periodStart", "periodEnd"],
            [NotificationTemplateKeys.ShiftDiscrepancy] =
                ["displayName", "organizationName", "branchName", "difference", "countedCash", "expectedCash",
                 "currency", "closedDate"],
            [NotificationTemplateKeys.StaffInvite] = ["displayName", "code"],
            [NotificationTemplateKeys.StaffInviteSms] = ["displayName", "code"],
            [NotificationTemplateKeys.StaffPasswordReset] = ["displayName", "code", "expiresInMinutes"],
            [NotificationTemplateKeys.StaffPasswordResetSms] = ["code", "expiresInMinutes"],
            [NotificationTemplateKeys.StaffPhoneVerification] = ["code", "expiresInMinutes", "displayName"],
        };
}
