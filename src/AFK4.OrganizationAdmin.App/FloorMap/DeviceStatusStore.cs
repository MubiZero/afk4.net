using AFK4.Shared.Contracts.Devices;

namespace AFK4.OrganizationAdmin.App.FloorMap;

public sealed class DeviceStatusStore(IList<FloorMapSeatViewModel> seats)
{
    public bool Apply(DeviceStatusChangedDto status)
    {
        var seat = seats.FirstOrDefault(candidate => candidate.DeviceId == status.DeviceId)
            ?? seats.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, status.MachineName, StringComparison.OrdinalIgnoreCase));

        if (seat is null)
        {
            return false;
        }

        seat.ApplyDeviceState(status.IsOnline, status.IsLocked, status.ObservedAtUtc);
        return true;
    }
}
