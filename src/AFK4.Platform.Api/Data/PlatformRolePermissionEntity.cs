namespace AFK4.Platform.Api.Data;

public sealed class PlatformRolePermissionEntity
{
    public Guid PlatformRolePermissionId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string PermissionName { get; set; } = string.Empty;
}
