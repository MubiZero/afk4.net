namespace AFK4.Shared.Contracts.Platform.Auth;

/// <summary>Роль платформы вместе с тем, что она даёт и сколько человек её носят.</summary>
public sealed record PlatformRoleDto(
    string RoleName,
    string DisplayName,
    string Description,
    bool IsBuiltIn,
    bool GrantsAllPermissions,
    IReadOnlyList<string> Permissions,
    int AdminCount);

public sealed record CreatePlatformRoleRequest(
    string RoleName,
    string DisplayName,
    string Description,
    IReadOnlyList<string> Permissions);

public sealed record UpdatePlatformRoleRequest(
    string DisplayName,
    string Description,
    IReadOnlyList<string> Permissions);

/// <summary>
/// Машинные коды отказа. Фразу для человека собирает панель: конкретная причина, доехавшая до
/// экрана, — это разница между «почини вот это» и «что-то пошло не так».
/// </summary>
public static class PlatformRoleErrorCodes
{
    public const string RoleNameTaken = "role_name_taken";

    public const string UnknownPermission = "unknown_permission";

    public const string PermissionNotHeldByActor = "permission_not_held_by_actor";

    public const string SelfLockout = "self_lockout";

    public const string BuiltInRole = "built_in_role";

    public const string RoleInUse = "role_in_use";

    public const string Conflict = "conflict";

    public const string InvalidRole = "invalid_role";
}
