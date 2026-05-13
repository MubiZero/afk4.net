namespace AFK4.Shared.Contracts.Identity;

public static class StaffPermissionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "devices.commands.dispatch";

    public const string ViewDeviceCommandStatus = "devices.commands.status.view";

    public const string RotateDeviceCredential = "devices.credentials.rotate";

    public const string RevokeDeviceCredential = "devices.credentials.revoke";

    public const string ViewDeviceDetail = "devices.detail.view";

    public const string ViewFloorMap = "floor_map.view";

    public const string StartSession = "sessions.start";

    public const string ExtendSession = "sessions.extend";

    public const string TransferSession = "sessions.transfer";

    public const string EndSession = "sessions.end";

    public const string ViewSession = "sessions.view";

    public const string ManageRoles = "identity.roles.manage";

    public const string ViewAudit = "audit.view";
}
