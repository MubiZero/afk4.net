using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Operator.App.FloorMap;

public sealed class SeatContextPanelViewModel : INotifyPropertyChanged
{
    private const string WaitingForBackendConfirmation = "Ожидаем подтверждение сервера";
    private const string DefaultCurrencyCode = "TJS";
    private const string ServerReady = "Ожидает действия оператора";
    private const string ServerNotSent = "Команда не отправлена";
    private const string DeviceNoCommand = "Команд нет";
    private const string DeviceNotSent = "Команда не отправлена";
    private static readonly IReadOnlyList<BillingModeOptionViewModel> BillingModes =
    [
        new("Гость без учета", "", "Быстрый старт без аккаунта игрока и записи в ledger."),
        new("Постоплата в долг", BillingModeNames.PostpaidDebt, "Нужны аккаунт игрока и версия тарифа."),
        new("Предоплата кошельком", BillingModeNames.PrepaidWallet, "Нужны аккаунт игрока, версия тарифа и баланс."),
        new("Пакет", BillingModeNames.Package, "Нужны аккаунт игрока и пакет игрока.")
    ];

    private readonly IOperatorSessionApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly Func<CancellationToken, Task>? refreshAfterSuccess;
    private readonly AsyncRelayCommand startGuestSessionCommand;
    private readonly AsyncRelayCommand extendSessionCommand;
    private readonly AsyncRelayCommand extendBy15Command;
    private readonly AsyncRelayCommand extendBy30Command;
    private readonly AsyncRelayCommand transferSessionCommand;
    private readonly AsyncRelayCommand endSessionCommand;
    private readonly AsyncRelayCommand beginCheckoutCommand;
    private readonly RelayCommand selectBillingModeCommand;
    private readonly string currencyCode;
    private Guid? organizationId;
    private Guid? branchId;
    private FloorMapSeatViewModel? selectedSeat;
    private string playerAccountIdText = "";
    private int durationMinutes = 60;
    private int additionalMinutes = 15;
    private string billingMode = "";
    private string tariffRuleVersionId = "manual-v1";
    private string tariffVersionIdText = "";
    private string playerPackageIdText = "";
    private string targetSeatIdText = "";
    private bool startAsOpenTab;
    private string reason = "запрос оператора";
    private string? errorMessage;
    private string? pendingOperation;
    private string? statusMessage;
    private string serverConfirmationStatus = "Выберите место";
    private string deviceCommandStatus = "Выберите место";
    private bool isBusy;
    private CheckoutSessionViewModel? checkout;

    public SeatContextPanelViewModel(
        IOperatorSessionApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory,
        Func<CancellationToken, Task>? refreshAfterSuccess = null,
        string currencyCode = DefaultCurrencyCode)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;
        this.refreshAfterSuccess = refreshAfterSuccess;
        this.currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? DefaultCurrencyCode : currencyCode.Trim().ToUpperInvariant();

        startGuestSessionCommand = new AsyncRelayCommand(StartGuestSessionAsync, () => !IsBusy && CanStartGuestSession);
        extendSessionCommand = new AsyncRelayCommand(ExtendSessionAsync, () => !IsBusy && HasActiveSession);
        extendBy15Command = new AsyncRelayCommand(token => ExtendByMinutesAsync(15, token), () => !IsBusy && HasActiveSession);
        extendBy30Command = new AsyncRelayCommand(token => ExtendByMinutesAsync(30, token), () => !IsBusy && HasActiveSession);
        transferSessionCommand = new AsyncRelayCommand(TransferSessionAsync, () => !IsBusy && HasActiveSession);
        endSessionCommand = new AsyncRelayCommand(EndSessionAsync, () => !IsBusy && HasActiveSession);
        beginCheckoutCommand = new AsyncRelayCommand(BeginCheckoutAsync, () => !IsBusy && HasActiveSession && !IsCheckoutOpen);
        selectBillingModeCommand = new RelayCommand(parameter =>
        {
            if (parameter is BillingModeOptionViewModel option)
            {
                BillingMode = option.Value;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FloorMapSeatViewModel? SelectedSeat
    {
        get => selectedSeat;
        private set
        {
            if (ReferenceEquals(selectedSeat, value))
            {
                return;
            }

            if (selectedSeat is not null)
            {
                selectedSeat.PropertyChanged -= OnSelectedSeatPropertyChanged;
            }

            selectedSeat = value;

            if (selectedSeat is not null)
            {
                selectedSeat.PropertyChanged += OnSelectedSeatPropertyChanged;
            }

            OnPropertyChanged();
            NotifySelectedSeatState();
        }
    }

    public bool HasSelectedSeat => SelectedSeat is not null;

    public bool HasActiveSession => SelectedSeat?.ActiveSessionId is not null;

    public bool CanStartGuestSession => SelectedSeat?.CanStartSession == true;

    public string SelectedSeatSummary => SelectedSeat is null
        ? "ПК не выбран"
        : $"{SelectedSeat.Name} - {SelectedSeat.DisplayState}";

    public string SelectedSeatDetails => SelectedSeat is null
        ? "Выберите ПК на карте зала, чтобы запустить или управлять сессией."
        : $"{SelectedSeat.Zone} · {SelectedSeat.DeviceSummary}";

    public string SeatActionTitle => SelectedSeat is null
        ? "Выберите ПК"
        : HasActiveSession ? "Управление активной сессией" : "Запуск сессии";

    public string SeatActionHint => SelectedSeat is null
        ? "Карта зала - основной экран оператора. Выберите место, чтобы увидеть действия."
        : HasActiveSession
            ? "Сессия активна. Завершите ее при уходе игрока или продлите время."
            : "Быстрый гостевой старт не пишет ledger. Платные режимы используйте только с аккаунтом игрока.";

    public string ActiveSessionSummary => HasActiveSession
        ? (SelectedSeat?.RemainingTimeText is { Length: > 0 } remaining ? remaining : "Сессия активна")
        : "Активной сессии нет";

    public string SelectedSeatName => SelectedSeat?.Name ?? "Место не выбрано";

    public string SelectedSeatStatusText => SelectedSeat?.DisplayState ?? "нет выбора";

    public string SelectedSeatStatusForeground => SelectedSeat?.StateForeground ?? "#475467";

    public string SelectedSeatMetaLine => SelectedSeat is null
        ? "Выберите ПК на карте зала"
        : $"{SelectedSeat.Zone} · {SelectedSeat.DeviceVersionSummary}";

    public string ActiveSessionTimerText => HasActiveSession
        ? FormatTimer(SelectedSeat?.RemainingSeconds)
        : "--:--";

    public string ActiveSessionLeaseText => HasActiveSession
        ? "lease ok"
        : "lease none";

    public double ActiveSessionProgressValue
    {
        get
        {
            if (!HasActiveSession || SelectedSeat?.RemainingSeconds is not { } remainingSeconds)
            {
                return 0;
            }

            return Math.Clamp(remainingSeconds / 3600d * 100d, 0d, 100d);
        }
    }

    public string MoneyImpactText => SelectedSeat?.AccruedCostMinorUnits is { } minorUnits
        ? $"{FormatMoney(minorUnits)} {SelectedSeat.CurrencyCode ?? currencyCode}"
        : $"0.00 {currencyCode}";

    // An open tab is only valid for an unbilled guest or a postpaid-debt player;
    // other modes must reserve a fixed duration up front.
    public bool OpenTabAllowed => string.IsNullOrWhiteSpace(BillingMode) || BillingMode == BillingModeNames.PostpaidDebt;

    public bool StartAsOpenTab
    {
        get => startAsOpenTab;
        set
        {
            if (SetField(ref startAsOpenTab, value))
            {
                OnPropertyChanged(nameof(StartDurationModeText));
            }
        }
    }

    private bool EffectiveOpenTab => StartAsOpenTab && OpenTabAllowed;

    public string StartDurationModeText => EffectiveOpenTab ? "Открытый счёт (оплата при завершении)" : "Фиксированная длительность";

    public string LedgerImpactText => string.IsNullOrWhiteSpace(BillingMode)
        ? "ledger не пишется"
        : "ledger будет записан";

    public string DeviceConnectivityLine => SelectedSeat is null
        ? "нет выбранного устройства"
        : $"{(SelectedSeat.IsOnline ? "online" : "offline")} · locked={SelectedSeat.IsLocked.ToString().ToLowerInvariant()} · commands {(DeviceCommandStatus == DeviceNoCommand ? "ok" : DeviceCommandStatus)}";

    public IReadOnlyList<BillingModeOptionViewModel> BillingModeOptions => BillingModes;

    public bool IsPlayerBillingRequired => !string.IsNullOrWhiteSpace(BillingMode);

    public string BillingModeDescription =>
        BillingModes.FirstOrDefault(mode => mode.Value == BillingMode)?.Description ?? BillingModes[0].Description;

    public string PlayerAccountIdText
    {
        get => playerAccountIdText;
        set => SetField(ref playerAccountIdText, value);
    }

    public int DurationMinutes
    {
        get => durationMinutes;
        set => SetField(ref durationMinutes, value);
    }

    public int AdditionalMinutes
    {
        get => additionalMinutes;
        set => SetField(ref additionalMinutes, value);
    }

    public string BillingMode
    {
        get => billingMode;
        set
        {
            if (SetField(ref billingMode, value))
            {
                OnPropertyChanged(nameof(IsPlayerBillingRequired));
                OnPropertyChanged(nameof(BillingModeDescription));
                OnPropertyChanged(nameof(LedgerImpactText));
                OnPropertyChanged(nameof(OpenTabAllowed));
                OnPropertyChanged(nameof(StartDurationModeText));
            }
        }
    }

    public string TariffRuleVersionId
    {
        get => tariffRuleVersionId;
        set => SetField(ref tariffRuleVersionId, value);
    }

    public string TariffVersionIdText
    {
        get => tariffVersionIdText;
        set => SetField(ref tariffVersionIdText, value);
    }

    public string PlayerPackageIdText
    {
        get => playerPackageIdText;
        set => SetField(ref playerPackageIdText, value);
    }

    public string TargetSeatIdText
    {
        get => targetSeatIdText;
        set => SetField(ref targetSeatIdText, value);
    }

    public string Reason
    {
        get => reason;
        set => SetField(ref reason, value);
    }

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public string? PendingOperation
    {
        get => pendingOperation;
        private set => SetField(ref pendingOperation, value);
    }

    public string? StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetField(ref isBusy, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public ICommand StartGuestSessionCommand => startGuestSessionCommand;

    public ICommand ExtendSessionCommand => extendSessionCommand;

    public ICommand ExtendBy15Command => extendBy15Command;

    public ICommand ExtendBy30Command => extendBy30Command;

    public ICommand TransferSessionCommand => transferSessionCommand;

    public ICommand EndSessionCommand => endSessionCommand;

    public ICommand BeginCheckoutCommand => beginCheckoutCommand;

    public ICommand SelectBillingModeCommand => selectBillingModeCommand;

    public CheckoutSessionViewModel? Checkout
    {
        get => checkout;
        private set
        {
            if (SetField(ref checkout, value))
            {
                OnPropertyChanged(nameof(IsCheckoutOpen));
                NotifyCommandStates();
            }
        }
    }

    public bool IsCheckoutOpen => Checkout is not null;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        this.organizationId = organizationId;
        this.branchId = branchId;
    }

    public void SelectSeat(FloorMapSeatViewModel? seat)
    {
        SelectedSeat = seat;
        ErrorMessage = null;
        PendingOperation = null;
        StatusMessage = null;
        ServerConfirmationStatus = seat is null ? "Выберите место" : ServerReady;
        DeviceCommandStatus = seat is null
            ? "Выберите место"
            : seat.HasDevice ? DeviceNoCommand : "Устройство не назначено";
    }

    private void OnSelectedSeatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(FloorMapSeatViewModel.CanStartSession)
            or nameof(FloorMapSeatViewModel.DisplayState)
            or nameof(FloorMapSeatViewModel.StateForeground)
            or nameof(FloorMapSeatViewModel.DeviceSummary)
            or nameof(FloorMapSeatViewModel.DeviceVersionSummary)
            or nameof(FloorMapSeatViewModel.RemainingTimeText)
            or nameof(FloorMapSeatViewModel.IsOnline)
            or nameof(FloorMapSeatViewModel.IsLocked)
            or nameof(FloorMapSeatViewModel.RemainingSeconds))
        {
            NotifySelectedSeatState();
        }
    }

    private void NotifySelectedSeatState()
    {
        OnPropertyChanged(nameof(HasActiveSession));
        OnPropertyChanged(nameof(HasSelectedSeat));
        OnPropertyChanged(nameof(CanStartGuestSession));
        OnPropertyChanged(nameof(SelectedSeatSummary));
        OnPropertyChanged(nameof(SelectedSeatDetails));
        OnPropertyChanged(nameof(SeatActionTitle));
        OnPropertyChanged(nameof(SeatActionHint));
        OnPropertyChanged(nameof(ActiveSessionSummary));
        OnPropertyChanged(nameof(SelectedSeatName));
        OnPropertyChanged(nameof(SelectedSeatStatusText));
        OnPropertyChanged(nameof(SelectedSeatStatusForeground));
        OnPropertyChanged(nameof(SelectedSeatMetaLine));
        OnPropertyChanged(nameof(ActiveSessionTimerText));
        OnPropertyChanged(nameof(ActiveSessionLeaseText));
        OnPropertyChanged(nameof(ActiveSessionProgressValue));
        OnPropertyChanged(nameof(MoneyImpactText));
        OnPropertyChanged(nameof(DeviceConnectivityLine));
        NotifyCommandStates();
    }

    public async Task StartGuestSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSelectedSeat(out var seat) ||
            !TryGetOperatorContext(out var organizationIdValue, out var branchIdValue) ||
            !TryValidatePositive(DurationMinutes, "Длительность должна быть больше нуля.") ||
            !TryValidateTariffRuleVersion() ||
            !TryParseOptionalGuid(PlayerAccountIdText, "ID аккаунта игрока", out var playerAccountId) ||
            !TryParseOptionalGuid(TariffVersionIdText, "ID версии тарифа", out var tariffVersionId) ||
            !TryParseOptionalGuid(PlayerPackageIdText, "ID пакета игрока", out var playerPackageId))
        {
            return;
        }

        if (seat.ActiveSessionId is not null)
        {
            SetValidationError("На выбранном месте уже есть активная сессия.");
            return;
        }

        if (!seat.CanStartSession)
        {
            SetValidationError("Место не готово к запуску сессии.");
            return;
        }

        if (IsPlayerBillingRequired && playerAccountId is null)
        {
            SetValidationError("Для быстрого старта выберите гостя без учета или укажите аккаунт игрока для платного режима.");
            return;
        }

        var openTab = EffectiveOpenTab;
        var request = new StartGuestSessionRequest(
            organizationIdValue,
            seat.SeatId,
            TariffRuleVersionId.Trim(),
            idempotencyKeyFactory.Create("session-start"),
            DurationMode: openTab ? SessionDurationModes.Open : SessionDurationModes.Fixed,
            DurationMinutes: openTab ? null : DurationMinutes,
            PlayerAccountId: playerAccountId,
            BillingMode: BillingMode.Trim(),
            TariffVersionId: tariffVersionId,
            PlayerPackageId: playerPackageId);

        await ExecuteBackendCommandAsync(
            token => apiClient.StartGuestSessionAsync(branchIdValue, request, token),
            cancellationToken);
    }

    public async Task ExtendSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId) ||
            !TryValidatePositive(AdditionalMinutes, "Дополнительные минуты должны быть больше нуля.") ||
            !TryValidateTariffRuleVersion() ||
            !TryParseOptionalGuid(PlayerAccountIdText, "ID аккаунта игрока", out var playerAccountId) ||
            !TryParseOptionalGuid(TariffVersionIdText, "ID версии тарифа", out var tariffVersionId) ||
            !TryParseOptionalGuid(PlayerPackageIdText, "ID пакета игрока", out var playerPackageId))
        {
            return;
        }

        var request = new ExtendSessionRequest(
            AdditionalMinutes,
            TariffRuleVersionId.Trim(),
            idempotencyKeyFactory.Create("session-extend"),
            playerAccountId,
            BillingMode,
            tariffVersionId,
            playerPackageId);

        await ExecuteBackendCommandAsync(
            token => apiClient.ExtendSessionAsync(sessionId, request, token),
            cancellationToken);
    }

    public Task ExtendByMinutesAsync(int minutes, CancellationToken cancellationToken)
    {
        AdditionalMinutes = minutes;
        return ExtendSessionAsync(cancellationToken);
    }

    public async Task TransferSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId))
        {
            return;
        }

        if (!Guid.TryParse(TargetSeatIdText, out var targetSeatId))
        {
            SetValidationError("Укажите ID места для переноса.");
            return;
        }

        var request = new TransferSessionRequest(
            targetSeatId,
            idempotencyKeyFactory.Create("session-transfer"));

        await ExecuteBackendCommandAsync(
            token => apiClient.TransferSessionAsync(sessionId, request, token),
            cancellationToken);
    }

    public async Task EndSessionAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId))
        {
            return;
        }

        var request = new EndSessionRequest(
            string.IsNullOrWhiteSpace(Reason) ? "запрос оператора" : Reason.Trim(),
            idempotencyKeyFactory.Create("session-end"));

        await ExecuteBackendCommandAsync(
            token => apiClient.EndSessionAsync(sessionId, request, token),
            cancellationToken);
    }

    public async Task BeginCheckoutAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(out var sessionId) ||
            !TryGetOperatorContext(out var organizationIdValue, out _))
        {
            return;
        }

        var checkoutViewModel = new CheckoutSessionViewModel(
            apiClient,
            idempotencyKeyFactory,
            organizationIdValue,
            sessionId,
            currencyCode,
            onCompleted: async token =>
            {
                if (refreshAfterSuccess is not null)
                {
                    await refreshAfterSuccess(token);
                }

                StatusMessage = "Оплата принята, сессия закрыта.";
            },
            onClosed: () => Checkout = null);

        Checkout = checkoutViewModel;
        await checkoutViewModel.LoadQuoteAsync(cancellationToken);
    }

    public void CancelCheckout()
    {
        Checkout = null;
    }

    private async Task ExecuteBackendCommandAsync(
        Func<CancellationToken, Task<SessionCommandResponse>> execute,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = WaitingForBackendConfirmation;
        ServerConfirmationStatus = "Ожидаем подтверждение сервера";
        DeviceCommandStatus = "Команда еще не отправлена";
        IsBusy = true;

        try
        {
            var response = await execute(cancellationToken);

            if (refreshAfterSuccess is not null)
            {
                await refreshAfterSuccess(cancellationToken);
            }

            PendingOperation = null;
            ServerConfirmationStatus = "Подтверждено сервером";
            DeviceCommandStatus = FormatDeviceCommandStatus(response.DeviceCommands);
            StatusMessage = "Команда сессии принята.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = exception.Message;
            PendingOperation = null;
            ServerConfirmationStatus = "Ошибка сервера";
            DeviceCommandStatus = DeviceNotSent;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryGetSelectedSeat(out FloorMapSeatViewModel seat)
    {
        if (SelectedSeat is null)
        {
            SetValidationError("Сначала выберите место.");
            seat = null!;
            return false;
        }

        seat = SelectedSeat;
        return true;
    }

    private bool TryGetActiveSession(out Guid sessionId)
    {
        if (SelectedSeat?.ActiveSessionId is null)
        {
            SetValidationError("На выбранном месте нет активной сессии.");
            sessionId = Guid.Empty;
            return false;
        }

        sessionId = SelectedSeat.ActiveSessionId.Value;
        return true;
    }

    private bool TryGetOperatorContext(out Guid organizationIdValue, out Guid branchIdValue)
    {
        if (organizationId is null || branchId is null)
        {
            SetValidationError("Контекст оператора не загружен.");
            organizationIdValue = Guid.Empty;
            branchIdValue = Guid.Empty;
            return false;
        }

        organizationIdValue = organizationId.Value;
        branchIdValue = branchId.Value;
        return true;
    }

    private bool TryValidatePositive(int value, string message)
    {
        if (value <= 0)
        {
            SetValidationError(message);
            return false;
        }

        return true;
    }

    private bool TryValidateTariffRuleVersion()
    {
        if (string.IsNullOrWhiteSpace(TariffRuleVersionId))
        {
            SetValidationError("Версия тарифного правила обязательна.");
            return false;
        }

        return true;
    }

    private bool TryParseOptionalGuid(string text, string fieldName, out Guid? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (Guid.TryParse(text, out var parsed))
        {
            value = parsed;
            return true;
        }

        SetValidationError($"{fieldName} должен быть корректным GUID.");
        value = null;
        return false;
    }

    private void SetValidationError(string message)
    {
        ErrorMessage = message;
        StatusMessage = null;
        PendingOperation = null;
        ServerConfirmationStatus = ServerNotSent;
        DeviceCommandStatus = DeviceNotSent;
    }

    public string ServerConfirmationStatus
    {
        get => serverConfirmationStatus;
        private set => SetField(ref serverConfirmationStatus, value);
    }

    public string DeviceCommandStatus
    {
        get => deviceCommandStatus;
        private set
        {
            if (SetField(ref deviceCommandStatus, value))
            {
                OnPropertyChanged(nameof(DeviceConnectivityLine));
            }
        }
    }

    private static string FormatMoney(long minorUnits)
    {
        return (minorUnits / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatTimer(int? seconds)
    {
        if (seconds is null)
        {
            return "∞";
        }

        var safeSeconds = Math.Max(0, seconds.Value);
        var totalMinutes = safeSeconds / 60;
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }

    private static string FormatDeviceCommandStatus(IReadOnlyList<DeviceCommandDto> commands)
    {
        if (commands.Count == 0)
        {
            return "Команда не требуется";
        }

        if (commands.Count == 1)
        {
            return $"{ToDisplayCommandType(commands[0].Type)} отправлена Agent";
        }

        return $"Отправлено команд Agent: {commands.Count}";
    }

    private static string ToDisplayCommandType(string commandType)
    {
        return commandType.Trim().ToLowerInvariant() switch
        {
            "lock" => "lock",
            "unlock" => "unlock",
            "lease_refresh" => "lease refresh",
            "restart_shell" => "restart Shell",
            _ => commandType.Trim()
        };
    }

    private void NotifyCommandStates()
    {
        startGuestSessionCommand.NotifyCanExecuteChanged();
        extendSessionCommand.NotifyCanExecuteChanged();
        extendBy15Command.NotifyCanExecuteChanged();
        extendBy30Command.NotifyCanExecuteChanged();
        transferSessionCommand.NotifyCanExecuteChanged();
        endSessionCommand.NotifyCanExecuteChanged();
        beginCheckoutCommand.NotifyCanExecuteChanged();
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

public sealed record BillingModeOptionViewModel(
    string Label,
    string Value,
    string Description);
