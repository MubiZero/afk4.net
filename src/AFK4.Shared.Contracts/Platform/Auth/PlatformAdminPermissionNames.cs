namespace AFK4.Shared.Contracts.Platform.Auth;

public static class PlatformAdminPermissionNames
{
    public const string ViewTenants = "platform.tenants.view";

    public const string CreateTenant = "platform.tenants.create";

    public const string UpdateTenantStatus = "platform.tenants.status.update";

    public const string UpdateTenantPlan = "platform.tenants.plan.update";

    public const string UpdateTenantLimits = "platform.tenants.limits.update";

    public const string ViewTenantSupportNotes = "platform.tenants.support_notes.view";

    public const string ManageTenantSupportNotes = "platform.tenants.support_notes.manage";

    public const string ManageOwnerInvites = "platform.tenants.invites.manage";

    public const string ViewTenantHealth = "platform.tenants.health.view";

    public const string ViewPlatformAudit = "platform.audit.view";

    public const string ViewBilling = "platform.billing.view";

    public const string ManagePlans = "platform.billing.plans.manage";

    public const string ManageSubscriptions = "platform.billing.subscriptions.manage";

    public const string ManageInvoices = "platform.billing.invoices.manage";
}
