using System.ComponentModel;
using System.Globalization;
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
            remainingSeconds: null,
            accruedCostMinorUnits: null,
            currencyCode: null)
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
        int? remainingSeconds,
        long? accruedCostMinorUnits,
        string? currencyCode)
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
        AccruedCostMinorUnits = accruedCostMinorUnits;
        CurrencyCode = currencyCode;
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

    public long? AccruedCostMinorUnits { get; }

    public string? CurrencyCode { get; }

    public bool HasActiveSession => ActiveSessionId is not null;

    // An open tab counts cost up instead of time down: active, no countdown, with
    // a resolvable live accrued charge.
    public bool IsOpenTab => HasActiveSession && RemainingSeconds is null && AccruedCostMinorUnits is not null;

    public string AccruedCostText => AccruedCostMinorUnits is { } minorUnits
        ? $"≈ {FormatMoney(minorUnits)} {CurrencyCode ?? "TJS"}"
        : "";

    public bool HasDevice => DeviceId is not null;

    public bool CanStartSession => !HasActiveSession && StateTone == "Ready";

    public string DisplayState => NormalizeState(State) switch
    {
        "active" => "В сессии",
        "free" => "Свободно",
        "locked" => "Готов",
        "requested" => "Ожидание",
        "ending" => "Команда",
        "paused" => "Пауза",
        "failed" => "Ошибка",
        "offline" => "Офлайн",
        "maintenance" or "service" => "Сервис",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(State.Replace('_', ' '))
    };

    public string StateTone
    {
        get
        {
            var normalized = NormalizeState(State);
            if (HasDevice && !IsOnline && normalized is "active" or "paused" or "ending")
            {
                return "Warning";
            }

            if (HasDevice && !IsOnline)
            {
                return "Offline";
            }

            return normalized switch
            {
                "active" => "Active",
                "free" or "locked" => "Ready",
                "requested" or "ending" => "Pending",
                "paused" => "Warning",
                "failed" => "Blocking",
                "offline" => "Offline",
                "maintenance" or "service" => "Service",
                _ => "Neutral"
            };
        }
    }

    public bool IsPending => StateTone == "Pending";

    public bool IsProblem => StateTone is "Pending" or "Warning" or "Blocking" or "Offline" or "Service";

    public string StateForeground => StateTone switch
    {
        "Active" => "#027A48",
        "Ready" => "#175CD3",
        "Pending" => "#7A2E0E",
        "Warning" => "#B54708",
        "Blocking" => "#B42318",
        "Offline" => "#475467",
        "Service" => "#B54708",
        _ => "#344054"
    };

    public string StateAccentBrush => StateTone switch
    {
        "Active" => "#039855",
        "Ready" => "#2563EB",
        "Pending" => "#7C3AED",
        "Warning" => "#DC6803",
        "Blocking" => "#D92D20",
        "Offline" => "#98A2B3",
        "Service" => "#F59E0B",
        _ => "#667085"
    };

    public string DeviceSummary => DeviceName is null
        ? "Устройство не назначено"
        : $"{DeviceName}: {(IsOnline ? "online" : "offline")} · {(IsLocked ? "locked" : "unlocked")}";

    public string DeviceVersionSummary
    {
        get
        {
            if (DeviceName is null)
            {
                return "Agent/Shell не назначены";
            }

            var agent = string.IsNullOrWhiteSpace(AgentVersion) ? "Agent ?" : $"Agent {AgentVersion}";
            var shell = string.IsNullOrWhiteSpace(ShellVersion) ? "Shell ?" : $"Shell {ShellVersion}";
            return $"{agent} · {shell}";
        }
    }

    public string HeartbeatSummary => LastHeartbeatAtUtc is null
        ? "heartbeat нет"
        : $"heartbeat {LastHeartbeatAtUtc.Value.UtcDateTime:HH:mm}";

    public string OperatorActionText => StateTone switch
    {
        "Active" => RemainingTimeText.Length == 0 ? (IsOpenTab ? AccruedCostText : "играет сейчас") : RemainingTimeText,
        "Ready" => "ожидает",
        "Pending" => NormalizeState(State) == "ending" ? "команда в пути" : "ждет сервер",
        "Warning" when HasActiveSession && !IsOnline => "играет, ПК офлайн",
        "Warning" => "проверить",
        "Blocking" => "нужна проверка",
        "Offline" => "проверить",
        "Service" => "закрыто",
        _ => string.Empty
    };

    public string RemainingTimeText => RemainingSeconds is null
        ? ""
        : FormatDuration(RemainingSeconds.Value);

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
            dto.RemainingSeconds,
            dto.AccruedCostMinorUnits,
            dto.CurrencyCode);
    }

    private static string FormatMoney(long minorUnits)
    {
        return (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    public void ApplyDeviceState(bool isOnline, bool isLocked, DateTimeOffset observedAtUtc)
    {
        IsOnline = isOnline;
        IsLocked = isLocked;
        LastHeartbeatAtUtc = observedAtUtc;

        if (!HasActiveSession)
        {
            State = isOnline
                ? isLocked ? "Locked" : "Free"
                : "Offline";
        }

        OnDisplayStateChanged();
    }

    private static string NormalizeState(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60)
        {
            return $"осталось {seconds} с";
        }

        var totalMinutes = (int)Math.Ceiling(seconds / 60m);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        return hours == 0
            ? $"осталось {minutes} мин"
            : $"осталось {hours} ч {minutes:00} мин";
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

    private void OnDisplayStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayState)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateTone)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateForeground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateAccentBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPending)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsProblem)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceVersionSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeartbeatSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingTimeText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OperatorActionText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStartSession)));
    }
}
