namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// The closed registry of notification template keys. Callers reference these constants rather
/// than literal strings so a typo is a compile error, and <see cref="ITemplateProvider"/>
/// validates <see cref="All"/> at startup so a missing template file is a startup error rather
/// than a silent runtime drop. Trigger integrations (§7 of the spec) add their keys here.
/// </summary>
public static class NotificationTemplateKeys
{
    /// <summary>Trivial end-to-end probe template shipped with the core backbone (Stage 1).</summary>
    public const string Test = "notification.test";

    /// <summary>Self-service password reset for a staff/owner account (Stage 3).</summary>
    public const string StaffPasswordReset = "staff.password_reset";

    /// <summary>SMS phone verification code for staff registration (Phase B).</summary>
    public const string StaffPhoneVerification = "staff.phone_verification";

    /// <summary>SMS phone verification code for a player confirming their own number.</summary>
    public const string PlayerPhoneVerification = "player.phone_verification";

    /// <summary>SMS sign-in code for a player entering without a PIN.</summary>
    public const string PlayerSignInCode = "player.sign_in_code";

    /// <summary>SMS password-reset code for a staff/owner account (Phase D).</summary>
    public const string StaffPasswordResetSms = "staff.password_reset_sms";

    /// <summary>Owner invite email carrying the invite code/link (Stage 3).</summary>
    public const string OrganizationOwnerInvite = "owner.invite";

    /// <summary>Staff invite email carrying the invite code; invitee sets their own password on accept (Stage 3, additive).</summary>
    public const string StaffInvite = "staff.invite";

    /// <summary>Приглашение сотрудника коротким кодом. Основной путь: почта есть не у каждого.</summary>
    public const string StaffInviteSms = "staff.invite_sms";

    /// <summary>Invoice issued to the organization owner (Stage 4 billing trigger).</summary>
    public const string InvoiceIssued = "invoice.issued";

    /// <summary>Invoice payment receipt to the organization owner (Stage 4 billing trigger).</summary>
    public const string InvoicePaid = "invoice.paid";

    /// <summary>Invoice overdue dunning notice to the organization owner (Stage 4 billing trigger).</summary>
    public const string InvoiceOverdue = "invoice.overdue";

    /// <summary>Pre-due reminder sent ahead of the invoice due date (Stage 4 billing trigger).</summary>
    public const string InvoiceDueSoon = "invoice.due_soon";

    /// <summary>Shift cash-variance alert to the organization owner (Stage 5 operational trigger).</summary>
    public const string ShiftDiscrepancy = "shift.discrepancy";

    /// <summary>Low-stock alert to the organization owner when stock-on-hand reaches the reorder threshold (Stage 5 operational trigger).</summary>
    public const string LowStock = "inventory.low_stock";

    /// <summary>Daily owner summary digest — revenue, shifts and cash discrepancies for the prior day (Stage 5 digest trigger).</summary>
    public const string OwnerDailySummary = "owner.daily_summary";

    /// <summary>Platform announcement (severity warning or above) delivered to the organization owner (Wave D §3).</summary>
    public const string PlatformAnnouncement = "platform.announcement";

    /// <summary>Scheduled report digest — a recurring CSV report delivered to the owner as an attachment (Stage 5 digest trigger).</summary>
    public const string ScheduledReport = "report.scheduled";

    /// <summary>Пуш игроку: до конца оплаченного времени осталось немного, ещё можно продлить.</summary>
    public const string PlayerSessionEnding = "player.session_ending";

    /// <summary>Пуш игроку: до брони остался час.</summary>
    public const string PlayerReservationSoon = "player.reservation_soon";

    /// <summary>Пуш игроку: заявку на пополнение подтвердили, деньги на балансе.</summary>
    public const string PlayerBalanceToppedUp = "player.balance_topped_up";

    /// <summary>Пуш игроку: заказ из бара собран и его несут за место.</summary>
    public const string PlayerOrderReady = "player.order_ready";

    public static readonly IReadOnlyList<string> All =
        [Test, StaffPasswordReset, StaffPhoneVerification, PlayerPhoneVerification, PlayerSignInCode, StaffPasswordResetSms, OrganizationOwnerInvite, StaffInvite, StaffInviteSms, InvoiceIssued, InvoicePaid, InvoiceOverdue, InvoiceDueSoon, ShiftDiscrepancy, LowStock, OwnerDailySummary, ScheduledReport, PlatformAnnouncement, PlayerSessionEnding, PlayerReservationSoon, PlayerBalanceToppedUp, PlayerOrderReady];
}
