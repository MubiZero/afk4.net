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

    /// <summary>Owner invite email carrying the invite code/link (Stage 3).</summary>
    public const string OwnerInvite = "owner.invite";

    /// <summary>Staff invite email carrying the invite code; invitee sets their own password on accept (Stage 3, additive).</summary>
    public const string StaffInvite = "staff.invite";

    /// <summary>Invoice issued to the organization owner (Stage 4 billing trigger).</summary>
    public const string InvoiceIssued = "invoice.issued";

    /// <summary>Invoice payment receipt to the organization owner (Stage 4 billing trigger).</summary>
    public const string InvoicePaid = "invoice.paid";

    /// <summary>Invoice overdue dunning notice to the organization owner (Stage 4 billing trigger).</summary>
    public const string InvoiceOverdue = "invoice.overdue";

    /// <summary>Shift cash-variance alert to the organization owner (Stage 5 operational trigger).</summary>
    public const string ShiftDiscrepancy = "shift.discrepancy";

    public static readonly IReadOnlyList<string> All =
        [Test, StaffPasswordReset, OwnerInvite, StaffInvite, InvoiceIssued, InvoicePaid, InvoiceOverdue, ShiftDiscrepancy];
}
