using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

/// <summary>Встроенные роли, объявленные кодом. Дальше их состав правит панель.</summary>
public static class PlatformRoleCatalog
{
    public sealed record Declaration(
        string RoleName,
        string DisplayName,
        string Description,
        bool GrantsAllPermissions,
        IReadOnlyList<string> Permissions);

    public static readonly IReadOnlyList<Declaration> Declared =
    [
        new(PlatformAdminRoleNames.PlatformAdmin,
            "Администратор платформы",
            "Полный доступ ко всем разделам платформы.",
            GrantsAllPermissions: true,
            Permissions: []),
        new(PlatformAdminRoleNames.PlatformSupport,
            "Поддержка",
            "Наблюдение за клубами, заметки и приглашения владельцев без доступа к деньгам и раскатам.",
            GrantsAllPermissions: false,
            Permissions:
            [
                PlatformAdminPermissionNames.UseSupportAccess,
                PlatformAdminPermissionNames.ViewOrganizations,
                PlatformAdminPermissionNames.UpdateOrganizationStatus,
                PlatformAdminPermissionNames.ViewOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationOwnerInvites,
                PlatformAdminPermissionNames.ViewOrganizationHealth,
                PlatformAdminPermissionNames.ViewPlatformAudit,
                PlatformAdminPermissionNames.ViewPlatformHealth
            ])
    ];
}
