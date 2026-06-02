using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapWorkspaceViewModel : INotifyPropertyChanged
{
    private const int StaleAfterSeconds = 30;

    private readonly IOperatorFloorMapApiClient apiClient;
    private readonly IFloorMapCache floorMapCache;
    private readonly TimeProvider timeProvider;
    private readonly DeviceStatusStore deviceStatusStore;
    private readonly RelayCommand selectSeatCommand;
    private readonly RelayCommand selectFilterCommand;
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
        TimeProvider? timeProvider = null)
    {
        this.apiClient = apiClient;
        this.floorMapCache = floorMapCache ?? new FileFloorMapCache();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        Seats = [];
        deviceStatusStore = new DeviceStatusStore(Seats);
        SeatContext = new SeatContextPanelViewModel(sessionApiClient, idempotencyKeyFactory, RefreshAsync, currencyCode);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => lastBranchId is not null && !IsLoading);
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

            // First good refresh exits degraded mode (spec §6.5 "Reconnect").
            IsOffline = false;
            CachedAtUtc = now;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            // The platform is unreachable. Hydrate the last-known-good snapshot and degrade to a
            // read-only mirror (spec §6.5); if there is nothing cached, fall back to the error surface.
            var cached = floorMapCache.Load(branchId);
            if (cached is not null)
            {
                PopulateSeats(cached.FloorMap);
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
