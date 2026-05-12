namespace AFK4.Platform.Api.Identity;

public sealed class StaffContextAccessor : IStaffContextAccessor
{
    public StaffContext? Current { get; set; }
}
