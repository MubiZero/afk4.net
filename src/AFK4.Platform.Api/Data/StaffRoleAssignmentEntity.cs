namespace AFK4.Platform.Api.Data;

public sealed class StaffRoleAssignmentEntity
{
    public Guid StaffRoleAssignmentId { get; set; }

    public Guid StaffUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string RoleName { get; set; } = string.Empty;
}
