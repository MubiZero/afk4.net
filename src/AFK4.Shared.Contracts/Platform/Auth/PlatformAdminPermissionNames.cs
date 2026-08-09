namespace AFK4.Shared.Contracts.Platform.Auth;

public static class PlatformAdminPermissionNames
{
    public const string UseSupportAccess = "platform.support.access";

    public const string ViewOrganizations = "platform.organizations.view";

    public const string CreateOrganization = "platform.organizations.create";

    public const string UpdateOrganizationStatus = "platform.organizations.status.update";

    public const string UpdateOrganizationLimits = "platform.organizations.limits.update";

    public const string UpdateOrganizationProfile = "platform.organizations.profile.update";

    public const string UpdateOrganizationUpdateChannel = "platform.organizations.update_channel.update";

    public const string ViewOrganizationSupportNotes = "platform.organizations.support_notes.view";

    public const string ManageOrganizationSupportNotes = "platform.organizations.support_notes.manage";

    public const string ManageOrganizationOwnerInvites = "platform.organizations.owner_invites.manage";

    public const string TransferOrganizationOwner = "platform.organizations.owner.transfer";

    public const string ViewOrganizationHealth = "platform.organizations.health.view";

    public const string ViewPlatformAudit = "platform.audit.view";

    public const string ViewBilling = "platform.billing.view";

    public const string ManagePlans = "platform.billing.plans.manage";

    public const string ManageSubscriptions = "platform.billing.subscriptions.manage";

    public const string ManageInvoices = "platform.billing.invoices.manage";

    public const string ViewUpdates = "platform.updates.view";

    public const string ManageUpdatePackages = "platform.updates.packages.manage";

    public const string ManageUpdateRollouts = "platform.updates.rollouts.manage";

    public const string ManagePlatformAdmins = "platform.admins.manage";

    public const string ViewPlatformHealth = "platform.health.view";

    public const string ManageOrganizationFeatures = "platform.organizations.features.manage";

    /// <summary>
    /// Все права платформы. Роль с полным доступом получает этот список целиком, панель
    /// показывает его как набор переключателей. Полнота стережётся тестом.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        UseSupportAccess,
        ViewOrganizations,
        CreateOrganization,
        UpdateOrganizationStatus,
        UpdateOrganizationLimits,
        UpdateOrganizationProfile,
        UpdateOrganizationUpdateChannel,
        ViewOrganizationSupportNotes,
        ManageOrganizationSupportNotes,
        ManageOrganizationOwnerInvites,
        TransferOrganizationOwner,
        ViewOrganizationHealth,
        ManageOrganizationFeatures,
        ViewPlatformAudit,
        ViewBilling,
        ManagePlans,
        ManageSubscriptions,
        ManageInvoices,
        ViewUpdates,
        ManageUpdatePackages,
        ManageUpdateRollouts,
        ManagePlatformAdmins,
        ViewPlatformHealth
    ];
}
