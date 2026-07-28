using System.Collections.ObjectModel;
using AFK4.OrganizationAdmin.App.Devices;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.OrganizationAdmin.App.FloorMap;

public sealed class MainWindowViewModel
{
    private readonly DeviceStatusStore deviceStatusStore;

    public MainWindowViewModel()
        : this(new UnconfiguredOperatorDeviceApiClient())
    {
    }

    public MainWindowViewModel(IOperatorDeviceApiClient operatorDeviceApiClient)
    {
        Seats =
        [
            new FloorMapSeatViewModel("PC-001", "Main Hall", "Free"),
            new FloorMapSeatViewModel("PC-002", "Main Hall", "Locked")
        ];

        deviceStatusStore = new DeviceStatusStore(Seats);
        TechnicianWorkflow = new TechnicianDeviceWorkflowViewModel(operatorDeviceApiClient);
    }

    public string Title => "Organization Admin";

    public ObservableCollection<FloorMapSeatViewModel> Seats { get; }

    public TechnicianDeviceWorkflowViewModel TechnicianWorkflow { get; }

    public bool ApplyDeviceStatus(DeviceStatusChangedDto status)
    {
        return deviceStatusStore.Apply(status);
    }
}
