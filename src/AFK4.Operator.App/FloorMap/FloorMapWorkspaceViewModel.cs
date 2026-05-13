using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IOperatorFloorMapApiClient apiClient;
    private readonly DeviceStatusStore deviceStatusStore;
    private Guid? lastBranchId;
    private string branchName = "Floor map";
    private FloorMapSeatViewModel? selectedSeat;
    private bool isLoading;
    private string? errorMessage;

    public FloorMapWorkspaceViewModel()
        : this(new UnconfiguredOperatorFloorMapApiClient())
    {
    }

    public FloorMapWorkspaceViewModel(IOperatorFloorMapApiClient apiClient)
    {
        this.apiClient = apiClient;
        Seats = [];
        deviceStatusStore = new DeviceStatusStore(Seats);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => lastBranchId is not null && !IsLoading);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string BranchName
    {
        get => branchName;
        private set => SetField(ref branchName, value);
    }

    public ObservableCollection<FloorMapSeatViewModel> Seats { get; }

    public FloorMapSeatViewModel? SelectedSeat
    {
        get => selectedSeat;
        set
        {
            if (selectedSeat is not null)
            {
                selectedSeat.IsSelected = false;
            }

            if (SetField(ref selectedSeat, value) && selectedSeat is not null)
            {
                selectedSeat.IsSelected = true;
            }
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetField(ref isLoading, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public async Task LoadAsync(Guid branchId, CancellationToken cancellationToken)
    {
        lastBranchId = branchId;
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            var floorMap = await apiClient.GetFloorMapAsync(branchId, cancellationToken);
            BranchName = floorMap.BranchName;
            Seats.Clear();

            foreach (var seat in floorMap.Seats.OrderBy(seat => seat.SortOrder).ThenBy(seat => seat.SeatName))
            {
                Seats.Add(FloorMapSeatViewModel.FromDto(seat));
            }

            SelectedSeat = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public bool ApplyDeviceStatus(DeviceStatusChangedDto status)
    {
        return deviceStatusStore.Apply(status);
    }

    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        return lastBranchId is null
            ? Task.CompletedTask
            : LoadAsync(lastBranchId.Value, cancellationToken);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
