namespace AFK4.Platform.Api.Identity;

public sealed record StaffContext(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<string> Permissions)
{
    public AuthenticationDomain Domain => AuthenticationDomain.Organization;

    public IReadOnlyList<string> RoleNames { get; init; } = [];

    // Права, сгруппированные по филиалу. Пустой словарь = деградация к union (обратная совместимость
    // для контекстов, собранных не через CreateContextAsync).
    public IReadOnlyDictionary<Guid, IReadOnlySet<string>> PermissionsByBranch { get; init; }
        = new Dictionary<Guid, IReadOnlySet<string>>();

    public bool HasBranchPermission(Guid branchId, string permission)
    {
        if (PermissionsByBranch.TryGetValue(branchId, out var perms))
        {
            return perms.Contains(permission, StringComparer.OrdinalIgnoreCase);
        }
        // Фолбэк: если карта не заполнена (старый путь), — прежнее поведение union.
        return BranchIds.Contains(branchId) && Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
