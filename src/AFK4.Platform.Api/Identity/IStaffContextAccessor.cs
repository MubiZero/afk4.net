namespace AFK4.Platform.Api.Identity;

public interface IStaffContextAccessor
{
    StaffContext? Current { get; set; }
}
