using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Devices;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapWorkspaceViewModel : INotifyPropertyChanged
{
    private const int StaleAfterSeconds = 30;

    private static readonly IReadOnlyDictionary<string, string> EmptyCommandPayload =
        new Dictionary<string, string>();

    private readonly IOperatorFloorMapApiClient apiClient;
    private readonly IFloorMapCache floorMapCache;
    private readonly TimeProvider timeProvider;
    private readonly IOperatorDeviceApiClient deviceApiClient;
    private readonly IActionOutbox actionOutbox;
    private readonly DeviceStatusStore deviceStatusStore;
    private readonly RelayCommand selectSeatCommand;
    private readonly RelayCommand selectFilterCommand;
    private readonly AsyncRelayCommand lockSeatCommand;
    private readonly AsyncRelayCommand unlockSeatCommand;
    private Guid? lastBranchId;
    private string branchName = "Филиал";
    private FloorMapSeatViewModel? selectedSeat;
    private string selectedFilterKey = FloorMapFilterKeys.All;
    private bool isLoading;
    private string? errorMessage;
    private bool isOffline;
    private DateTimeOffset? cachedAtUtc;

    public FloorMapWorkspaceViewModel()
        : this(
            new UnconfiguredOperatorFloorMapApiClient(),
            new UnconfiguredOperatorSessionApiClient(),
            new GuidIdempotencyKeyFactory())
    {
    }

    public FloorMapWorkspaceViewModel(IOperatorFloorMapApiClient apiClient)
        : this(
            apiClient,
            new UnconfiguredOperatorSessionApiClient(),
            new GuidIdempotencyKeyFactory())
    {
    }

    public FloorMapWorkspaceViewModel(
        IOperatorFloorMapApiClient apiClient,
        IOperatorSessionApiClient sessionApiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory,
        string currencyCode = "TJS",
        IFloorMapCache? floorMapCache = null,
        TimeProvider? timeProvider = null,
        IOperatorDeviceApiClient? deviceApiClient = null,
        IActionOutbox? actionOutbox = null)
    {
        this.apiClient = apiClient;
        this.floorMapCache = floorMapCache ?? new FileFloorMapCache();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.deviceApiClient = deviceApiClient ?? new UnconfiguredOperatorDeviceApiClient();
        this.actionOutbox = actionOutbox ?? new FileActionOutbox();
        Seats = [];
        deviceStatusStore = new DeviceStatusStore(Seats);
        SeatContext = new SeatContextPanelViewModel(sessionApiClient, idempotencyKeyFactory, RefreshAsync, currencyCode);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => lastBranchId is not null && !IsLoading);
        // Lock/unlock stay available offline (spec §6.5): they queue locally instead of dispatching.
        lockSeatCommand = new AsyncRelayCommand(token => QueueOrDispatchSeatActionAsync("lock", token), () => SelectedSeat?.HasDevice == true && !IsLoading);
        unlockSeatCommand = new AsyncRelayCommand(token => QueueOrDispatchSeatActionAsync("unlock", token), () => SelectedSeat?.HasDevice == true && !IsLoading);
        selectSeatCommand = new RelayCommand(parameter => SelectedSeat = parameter as FloorMapSeatViewModel);
        selectFilterCommand = new RelayCommand(parameter => SelectFilter(parameter as FloorMapFilterOptionViewModel));
        FilterOptions =
        [
            new(FloorMapFilterKeys.All, "Все", isSelected: true),
            new(FloorMapFilterKeys.Ready, "Свободные"),
            new(FloorMapFilterKeys.Active, "Активные"),
            new(FloorMapFilterKeys.Problems, "Проблемы")
        ];
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string BranchName
    {
        get => branchName;
        private set => SetField(ref branchName, value);
    }

    public ObservableCollection<FloorMapSeatViewModel> Seats { get; }

    public ObservableCollection<FloorMapFilterOptionViewModel> FilterOptions { get; }

    public IEnumerable<FloorMapSeatViewModel> FilteredSeats => selectedFilterKey switch
    {
        FloorMapFilterKeys.Ready => Seats.Where(seat => !seat.HasActiveSession && seat.StateTone == "Ready"),
        FloorMapFilterKeys.Active => Seats.Where(seat => seat.HasActiveSession || seat.StateTone == "Active"),
        FloorMapFilterKeys.Problems => Seats.Where(seat => seat.IsProblem),
        _ => Seats
    };

    public int AvailableSeatCount => Seats.Count(seat => !seat.HasActiveSession && seat.StateTone == "Ready");

    public int ActiveSeatCount => Seats.Count(seat => seat.HasActiveSession || seat.State == "Active");

    public int PendingSeatCount => Seats.Count(seat => seat.IsPending);

    public int ProblemSeatCount => Seats.Count(seat => seat.IsProblem);

    public int OfflineSeatCount => Seats.Count(seat => seat.State == "Offline" || !seat.IsOnline);

    public string FloorSummary =>
        $"готово {AvailableSeatCount} / активно {ActiveSeatCount} / ожидают {PendingSeatCount} / офлайн {OfflineSeatCount}";

    public string HeaderSummary => $"{BranchName} · {FloorSummary}";

    public string ZoneSummary => Seats.Count == 0
        ? "online 0 · lock ok 0 · update n/a"
        : $"online {Seats.Count(seat => seat.IsOnline)} · lock ok {Seats.Count(seat => seat.IsLocked)} · update {LatestAgentVersion}";

    public IReadOnlyList<FloorMapFilterOptionViewModel> ZoneOptions => Seats
        .Select(seat => seat.Zone)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(3)
        .Select((zone, index) => new FloorMapFilterOptionViewModel($"zone-{index}", zone, index == 0))
        .ToList();

    public string LatestAgentVersion => Seats
        .Select(seat => seat.AgentVersion)
        .FirstOrDefault(version => !string.IsNullOrWhiteSpace(version)) ?? "n/a";

    public IReadOnlyList<OperationalSignalViewModel> OperationalSignals
    {
        get
        {
            var signals = Seats
                .Where(seat => seat.IsProblem)
                .Take(4)
                .Select(seat => new OperationalSignalViewModel(
                    seat.Name,
                    seat.OperatorActionText.Length == 0 ? seat.DisplayState : seat.OperatorActionText,
                    seat.StateTone))
                .ToList();

            if (signals.Count == 0)
            {
                signals.Add(new OperationalSignalViewModel("Зал", "критичных сигналов нет", "Active"));
            }

            return signals;
        }
    }

    public SeatContextPanelViewModel SeatContext { get; }

    public FloorMapSeatViewModel? SelectedSeat
    {
        get => selectedSeat;
        set
        {
            if (ReferenceEquals(selectedSeat, value))
            {
                return;
            }

            if (selectedSeat is not null)
            {
                selectedSeat.IsSelected = false;
            }

            if (SetField(ref selectedSeat, value) && value is not null)
            {
                value.IsSelected = true;
            }

            SeatContext.SelectSeat(value);
            lockSeatCommand.NotifyCanExecuteChanged();
            unlockSeatCommand.NotifyCanExecuteChanged();
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
                lockSeatCommand.NotifyCanExecuteChanged();
                unlockSeatCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    /// <summary>
    /// True when the last refresh failed and the workspace is rendering a cached, read-only mirror
    /// (spec §6.5). Seats render muted and billing actions are disabled while this is set.
    /// </summary>
    public bool IsOffline
    {
        get => isOffline;
        private set
        {
            if (SetField(ref isOffline, value))
            {
                SeatContext.IsOffline = value;
                OnPropertyChanged(nameof(OfflineBanner));
            }
        }
    }

    /// <summary>UTC timestamp of the floor-map snapshot currently shown (live or cached).</summary>
    public DateTimeOffset? CachedAtUtc
    {
        get => cachedAtUtc;
        private set
        {
            if (SetField(ref cachedAtUtc, value))
            {
                OnPropertyChanged(nameof(OfflineBanner));
            }
        }
    }

    /// <summary>
    /// Operator-facing banner shown once the mirror is offline or the data has aged past 30s (D8):
    /// "Офлайн — данные от HH:MM, только просмотр". Null while live and fresh.
    /// </summary>
    public string? OfflineBanner
    {
        get
        {
            if (cachedAtUtc is null)
            {
                return null;
            }

            var isStale = timeProvider.GetUtcNow() - cachedAtUtc.Value > TimeSpan.FromSeconds(StaleAfterSeconds);
            if (!isOffline && !isStale)
            {
                return null;
            }

            return $"Офлайн — данные от {cachedAtUtc.Value.ToLocalTime():HH:mm}, только просмотр";
        }
    }

    public AsyncRelayCommand RefreshCommand { get; }

    public ICommand SelectSeatCommand => selectSeatCommand;

    public ICommand SelectFilterCommand => selectFilterCommand;

    public ICommand LockSeatCommand => lockSeatCommand;

    public ICommand UnlockSeatCommand => unlockSeatCommand;

    /// <summary>
    /// Operator-visible notes for offline lock/unlock actions that were dropped on reconnect because the
    /// cloud state had already moved past them (spec §6.5 D9), so the operator knows they were not applied.
    /// </summary>
    public ObservableCollection<string> OfflineActionAudit { get; } = [];

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        SeatContext.ApplyContext(organizationId, branchId);
    }

    public async Task LoadAsync(Guid branchId, CancellationToken cancellationToken)
    {
        lastBranchId = branchId;
        ErrorMessage = null;
        IsLoading = true;

        try
        {
            var floorMap = await apiClient.GetFloorMapAsync(branchId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            floorMapCache.Save(floorMap, now);
            PopulateSeats(floorMap);

            // First good refresh exits degraded mode and drains the offline action queue against the
            // now-authoritative map (spec §6.5 "Reconnect").
            IsOffline = false;
            CachedAtUtc = now;
            await DrainActionOutboxAsync(cancellationToken);
            ApplyQueuedActionPills();
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            // The platform is unreachable. Hydrate the last-known-good snapshot and degrade to a
            // read-only mirror (spec §6.5); if there is nothing cached, fall back to the error surface.
            var cached = floorMapCache.Load(branchId);
            if (cached is not null)
            {
                PopulateSeats(cached.FloorMap);
                ApplyQueuedActionPills();
                CachedAtUtc = cached.CachedAtUtc;
                IsOffline = true;
            }
            else
            {
                ErrorMessage = exception.Message;
            }
        }
        finally
        {
            IsLoading = false;
            RefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public bool ApplyDeviceStatus(DeviceStatusChangedDto status)
    {
        var applied = deviceStatusStore.Apply(status);
        if (applied)
        {
            OnFloorSummaryChanged();
            OnPropertyChanged(nameof(FilteredSeats));
        }

        return applied;
    }

    private void OnFloorSummaryChanged()
    {
        OnPropertyChanged(nameof(AvailableSeatCount));
        OnPropertyChanged(nameof(ActiveSeatCount));
        OnPropertyChanged(nameof(PendingSeatCount));
        OnPropertyChanged(nameof(ProblemSeatCount));
        OnPropertyChanged(nameof(OfflineSeatCount));
        OnPropertyChanged(nameof(FloorSummary));
        OnPropertyChanged(nameof(HeaderSummary));
        OnPropertyChanged(nameof(ZoneSummary));
        OnPropertyChanged(nameof(ZoneOptions));
        OnPropertyChanged(nameof(LatestAgentVersion));
        OnPropertyChanged(nameof(OperationalSignals));
    }

    private void SelectFilter(FloorMapFilterOptionViewModel? option)
    {
        if (option is null || selectedFilterKey == option.Key)
        {
            return;
        }

        selectedFilterKey = option.Key;
        foreach (var filter in FilterOptions)
        {
            filter.IsSelected = filter.Key == selectedFilterKey;
        }

        OnPropertyChanged(nameof(FilteredSeats));
    }

    private void PopulateSeats(FloorMapDto floorMap)
    {
        BranchName = floorMap.BranchName;
        Seats.Clear();

        foreach (var seat in floorMap.Seats.OrderBy(seat => seat.SortOrder).ThenBy(seat => seat.SeatName))
        {
            Seats.Add(FloorMapSeatViewModel.FromDto(seat));
        }

        SelectedSeat = null;
        OnFloorSummaryChanged();
        OnPropertyChanged(nameof(FilteredSeats));
    }

    private async Task QueueOrDispatchSeatActionAsync(string commandType, CancellationToken cancellationToken)
    {
        var seat = SelectedSeat;
        if (seat?.DeviceId is not { } deviceId)
        {
            return;
        }

        if (IsOffline)
        {
            // Record the intent locally; it replays (or is dropped if superseded) on reconnect (spec §6.5).
            actionOutbox.Enqueue(new OperatorActionOutboxEntry(
                IdempotencyKey: Guid.NewGuid(),
                DeviceId: deviceId,
                SeatId: seat.SeatId,
                SeatName: seat.Name,
                CommandType: commandType,
                ExpectedSessionId: seat.ActiveSessionId,
                QueuedAtUtc: timeProvider.GetUtcNow()));
            ApplyQueuedActionPills();
            return;
        }

        await deviceApiClient.DispatchDeviceCommandAsync(deviceId, commandType, EmptyCommandPayload, cancellationToken);
    }

    private async Task DrainActionOutboxAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in actionOutbox.Pending)
        {
            var seat = Seats.FirstOrDefault(seat => seat.DeviceId == entry.DeviceId);
            if (seat is null || seat.ActiveSessionId != entry.ExpectedSessionId)
            {
                // Superseded by current cloud state (D9): the seat or its session moved on while we were
                // offline, so dropping the action with an audit note is safer than re-applying it.
                OfflineActionAudit.Add(
                    $"{entry.SeatName}: команда {entry.CommandType} отменена — состояние изменилось на сервере");
                actionOutbox.Acknowledge(entry.IdempotencyKey);
                continue;
            }

            try
            {
                await deviceApiClient.DispatchDeviceCommandAsync(
                    entry.DeviceId,
                    entry.CommandType,
                    EmptyCommandPayload,
                    cancellationToken);
                actionOutbox.Acknowledge(entry.IdempotencyKey);
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
            {
                // Connectivity dropped again mid-drain; keep the remaining actions queued for the next refresh.
                break;
            }
        }
    }

    private void ApplyQueuedActionPills()
    {
        var queuedByDevice = actionOutbox.Pending
            .Where(entry => entry.DeviceId != Guid.Empty)
            .GroupBy(entry => entry.DeviceId)
            .ToDictionary(group => group.Key, group => group.Last().CommandType);

        foreach (var seat in Seats)
        {
            if (seat.DeviceId is { } deviceId && queuedByDevice.TryGetValue(deviceId, out var commandType))
            {
                seat.SetQueuedAction($"Очередь: {commandType}");
            }
            else
            {
                seat.SetQueuedAction(null);
            }
        }
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
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class FloorMapFilterOptionViewModel(string key, string label, bool isSelected = false) : INotifyPropertyChanged
{
    private bool isSelected = isSelected;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; } = key;

    public string Label { get; } = label;

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}

public sealed record OperationalSignalViewModel(string Title, string Detail, string Tone);

public static class FloorMapFilterKeys
{
    public const string All = "all";
    public const string Ready = "ready";
    public const string Active = "active";
    public const string Problems = "problems";
}
