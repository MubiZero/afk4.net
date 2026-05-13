using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapSeatViewModel : INotifyPropertyChanged
{
    private string state;
    private bool isLocked;
    private bool isOnline = true;
    private DateTimeOffset? lastHeartbeatAtUtc;
    private int? remainingSeconds;
    private bool isSelected;

    public FloorMapSeatViewModel(string name, string zone, string state)
        : this(
            seatId: Guid.Empty,
            name,
            zoneId: Guid.Empty,
            zone,
            sortOrder: 0,
            state,
            deviceId: null,
            deviceName: name,
            isOnline: true,
            isLocked: state == "Locked",
            lastHeartbeatAtUtc: null,
            agentVersion: null,
            shellVersion: null,
            activeSessionId: null,
            remainingSeconds: null)
    {
    }

    private FloorMapSeatViewModel(
        Guid seatId,
        string name,
        Guid zoneId,
        string zone,
        int sortOrder,
        string state,
        Guid? deviceId,
        string? deviceName,
        bool isOnline,
        bool isLocked,
        DateTimeOffset? lastHeartbeatAtUtc,
        string? agentVersion,
        string? shellVersion,
        Guid? activeSessionId,
        int? remainingSeconds)
    {
        SeatId = seatId;
        Name = name;
        ZoneId = zoneId;
        Zone = zone;
        SortOrder = sortOrder;
        this.state = state;
        DeviceId = deviceId;
        DeviceName = deviceName;
        this.isOnline = isOnline;
        this.isLocked = isLocked;
        this.lastHeartbeatAtUtc = lastHeartbeatAtUtc;
        AgentVersion = agentVersion;
        ShellVersion = shellVersion;
        ActiveSessionId = activeSessionId;
        this.remainingSeconds = remainingSeconds;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid SeatId { get; }

    public string Name { get; }

    public Guid ZoneId { get; }

    public string Zone { get; }

    public int SortOrder { get; }

    public Guid? DeviceId { get; }

    public string? DeviceName { get; }

    public string? AgentVersion { get; }

    public string? ShellVersion { get; }

    public Guid? ActiveSessionId { get; }

    public string State
    {
        get => state;
        private set => SetField(ref state, value);
    }

    public bool IsOnline
    {
        get => isOnline;
        private set => SetField(ref isOnline, value);
    }

    public bool IsLocked
    {
        get => isLocked;
        private set => SetField(ref isLocked, value);
    }

    public DateTimeOffset? LastHeartbeatAtUtc
    {
        get => lastHeartbeatAtUtc;
        private set => SetField(ref lastHeartbeatAtUtc, value);
    }

    public int? RemainingSeconds
    {
        get => remainingSeconds;
        private set => SetField(ref remainingSeconds, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetField(ref isSelected, value);
    }

    public static FloorMapSeatViewModel FromDto(SeatStatusDto dto)
    {
        return new FloorMapSeatViewModel(
            dto.SeatId,
            dto.SeatName,
            dto.ZoneId,
            dto.ZoneName,
            dto.SortOrder,
            dto.State,
            dto.DeviceId,
            dto.DeviceName,
            dto.IsDeviceOnline ?? false,
            dto.IsDeviceLocked ?? true,
            dto.LastHeartbeatAtUtc,
            dto.AgentVersion,
            dto.ShellVersion,
            dto.ActiveSessionId,
            dto.RemainingSeconds);
    }

    public void ApplyDeviceState(bool isOnline, bool isLocked, DateTimeOffset observedAtUtc)
    {
        IsOnline = isOnline;
        IsLocked = isLocked;
        LastHeartbeatAtUtc = observedAtUtc;
        State = isOnline
            ? isLocked ? "Locked" : "Free"
            : "Offline";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
