namespace AFK4.Shared.Contracts.Platform.Auth;

public static class PlatformAdminPermissionNames
{
    public const string ViewOrganizations = "platform.organizations.view";

    public const string CreateOrganization = "platform.organizations.create";

    public const string UpdateOrganizationStatus = "platform.organizations.status.update";

    public const string UpdateOrganizationLimits = "platform.organizations.limits.update";

    public const string ViewOrganizationSupportNotes = "platform.organizations.support_notes.view";

    public const string ManageOrganizationSupportNotes = "platform.organizations.support_notes.manage";

    public const string ManageOrganizationOwnerInvites = "platform.organizations.owner_invites.manage";

    public const string ViewOrganizationHealth = "platform.organizations.health.view";

    public const string ViewPlatformAudit = "platform.audit.view";

    public const string ViewBilling = "platform.billing.view";

    public const string ManagePlans = "platform.billing.plans.manage";

    public const string ManageSubscriptions = "platform.billing.subscriptions.manage";

    public const string ManageInvoices = "platform.billing.invoices.manage";
}
