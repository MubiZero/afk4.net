using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Operator.App.Mvvm;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Operator.App.Players;

public sealed class PlayerSearchViewModel : INotifyPropertyChanged
{
    private const int DefaultSearchLimit = 20;
    private const string DefaultCurrencyCode = "USD";
    private const string WaitingForBackendConfirmation = "Waiting for backend confirmation";

    private readonly IOperatorPlayerApiClient apiClient;
    private readonly IIdempotencyKeyFactory idempotencyKeyFactory;
    private readonly AsyncRelayCommand searchCommand;
    private readonly AsyncRelayCommand createPlayerCommand;
    private readonly AsyncRelayCommand refreshSelectedPlayerCommand;
    private readonly AsyncRelayCommand topUpWalletCommand;
    private readonly AsyncRelayCommand payDebtCommand;
    private Guid organizationId;
    private Guid branchId;
    private string searchText = "";
    private PlayerSearchResultViewModel? selectedPlayer;
    private string newPlayerDisplayName = "";
    private string? newPlayerPhoneNumber;
    private long topUpAmountMinorUnits;
    private long debtPaymentAmountMinorUnits;
    private string moneyReason = "operator request";
    private WalletSummaryDto? walletSummary;
    private long walletBalanceMinorUnits;
    private long debtBalanceMinorUnits;
    private bool isBusy;
    private string? pendingOperation;
    private string? statusMessage;
    private string? errorMessage;

    public PlayerSearchViewModel(IOperatorPlayerApiClient apiClient)
        : this(apiClient, new GuidIdempotencyKeyFactory())
    {
    }

    public PlayerSearchViewModel(
        IOperatorPlayerApiClient apiClient,
        IIdempotencyKeyFactory idempotencyKeyFactory)
    {
        this.apiClient = apiClient;
        this.idempotencyKeyFactory = idempotencyKeyFactory;

        Results = [];
        PlayerPackages = [];
        searchCommand = new AsyncRelayCommand(SearchAsync, () => !IsBusy);
        createPlayerCommand = new AsyncRelayCommand(CreatePlayerAsync, () => !IsBusy);
        refreshSelectedPlayerCommand = new AsyncRelayCommand(RefreshSelectedPlayerAsync, CanRunSelectedPlayerCommand);
        topUpWalletCommand = new AsyncRelayCommand(TopUpWalletAsync, CanRunSelectedPlayerCommand);
        payDebtCommand = new AsyncRelayCommand(PayDebtAsync, CanRunSelectedPlayerCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PlayerSearchResultViewModel> Results { get; }

    public ObservableCollection<PlayerPackageSummaryViewModel> PlayerPackages { get; }

    public string SearchText
    {
        get => searchText;
        set => SetField(ref searchText, value);
    }

    public PlayerSearchResultViewModel? SelectedPlayer
    {
        get => selectedPlayer;
        set
        {
            if (SetField(ref selectedPlayer, value))
            {
                ApplySelectedPlayerSnapshot(value);
                NotifyCommandStates();
            }
        }
    }

    public string NewPlayerDisplayName
    {
        get => newPlayerDisplayName;
        set => SetField(ref newPlayerDisplayName, value);
    }

    public string? NewPlayerPhoneNumber
    {
        get => newPlayerPhoneNumber;
        set => SetField(ref newPlayerPhoneNumber, value);
    }

    public long TopUpAmountMinorUnits
    {
        get => topUpAmountMinorUnits;
        set => SetField(ref topUpAmountMinorUnits, value);
    }

    public long DebtPaymentAmountMinorUnits
    {
        get => debtPaymentAmountMinorUnits;
        set => SetField(ref debtPaymentAmountMinorUnits, value);
    }

    public string MoneyReason
    {
        get => moneyReason;
        set => SetField(ref moneyReason, value);
    }

    public WalletSummaryDto? WalletSummary
    {
        get => walletSummary;
        private set => SetField(ref walletSummary, value);
    }

    public long WalletBalanceMinorUnits
    {
        get => walletBalanceMinorUnits;
        private set
        {
            if (SetField(ref walletBalanceMinorUnits, value))
            {
                OnPropertyChanged(nameof(WalletSummaryText));
            }
        }
    }

    public long DebtBalanceMinorUnits
    {
        get => debtBalanceMinorUnits;
        private set
        {
            if (SetField(ref debtBalanceMinorUnits, value))
            {
                OnPropertyChanged(nameof(WalletSummaryText));
            }
        }
    }

    public string WalletSummaryText => $"Wallet {FormatMoney(WalletBalanceMinorUnits)} USD / debt {FormatMoney(DebtBalanceMinorUnits)} USD";

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

    public string? ErrorMessage
    {
        get => errorMessage;
        private set => SetField(ref errorMessage, value);
    }

    public ICommand SearchCommand => searchCommand;

    public ICommand CreatePlayerCommand => createPlayerCommand;

    public ICommand RefreshSelectedPlayerCommand => refreshSelectedPlayerCommand;

    public ICommand TopUpWalletCommand => topUpWalletCommand;

    public ICommand PayDebtCommand => payDebtCommand;

    public void ApplyContext(Guid organizationId, Guid branchId)
    {
        this.organizationId = organizationId;
        this.branchId = branchId;
    }

    public void SelectPlayer(PlayerSearchResultViewModel? player)
    {
        SelectedPlayer = player;
        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = null;
    }

    public async Task SearchAsync(CancellationToken cancellationToken)
    {
        var query = SearchText.Trim();
        ErrorMessage = null;
        StatusMessage = null;

        if (query.Length < 2)
        {
            Results.Clear();
            ErrorMessage = "Enter at least two characters to search.";
            return;
        }

        IsBusy = true;
        try
        {
            var players = await apiClient.SearchPlayersAsync(
                branchId,
                query,
                DefaultSearchLimit,
                cancellationToken);

            Results.Clear();
            foreach (var player in players)
            {
                Results.Add(new PlayerSearchResultViewModel(player));
            }

            StatusMessage = $"{Results.Count} player(s) found.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = CreateUserFacingError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CreatePlayerAsync(CancellationToken cancellationToken)
    {
        if (!TryGetOperatorContext(out var organizationIdValue, out var branchIdValue) ||
            !TryValidateDisplayName(out var displayName))
        {
            return;
        }

        var request = new CreatePlayerAccountRequest(
            organizationIdValue,
            displayName,
            string.IsNullOrWhiteSpace(NewPlayerPhoneNumber) ? null : NewPlayerPhoneNumber.Trim(),
            idempotencyKeyFactory.Create("player-create"));

        await ExecutePlayerOperationAsync(
            async token =>
            {
                var player = await apiClient.CreatePlayerAsync(branchIdValue, request, token);
                var searchResult = new PlayerSearchResultViewModel(new PlayerSearchResultDto(
                    player.PlayerAccountId,
                    player.DisplayName,
                    player.PhoneNumber,
                    WalletBalanceMinorUnits: 0,
                    DebtBalanceMinorUnits: 0,
                    ActivePackageCount: 0,
                    player.IsActive));

                Results.Insert(0, searchResult);
                SelectedPlayer = searchResult;
                await RefreshSelectedPlayerStateAsync(player.PlayerAccountId, token);
                StatusMessage = "Player account created.";
            },
            cancellationToken);
    }

    public async Task RefreshSelectedPlayerAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSelectedPlayer(out var player))
        {
            return;
        }

        await ExecutePlayerOperationAsync(
            async token =>
            {
                await RefreshSelectedPlayerStateAsync(player.PlayerAccountId, token);
                StatusMessage = "Player summary refreshed.";
            },
            cancellationToken);
    }

    public async Task TopUpWalletAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSelectedPlayer(out var player) ||
            !TryGetOrganizationContext(out var organizationIdValue) ||
            !TryValidatePositiveMoney(TopUpAmountMinorUnits, "Top-up amount must be greater than zero."))
        {
            return;
        }

        var request = new TopUpWalletRequest(
            organizationIdValue,
            new MoneyDto(DefaultCurrencyCode, TopUpAmountMinorUnits),
            NormalizeReason(),
            idempotencyKeyFactory.Create("wallet-top-up"));

        await ExecuteBackendConfirmationAsync(
            player.PlayerAccountId,
            async token =>
            {
                var summary = await apiClient.TopUpWalletAsync(player.PlayerAccountId, request, token);
                ApplyWalletSummary(summary);
                StatusMessage = "Player wallet top-up confirmed.";
            },
            cancellationToken);
    }

    public async Task PayDebtAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSelectedPlayer(out var player) ||
            !TryGetOrganizationContext(out var organizationIdValue) ||
            !TryValidatePositiveMoney(DebtPaymentAmountMinorUnits, "Debt payment amount must be greater than zero."))
        {
            return;
        }

        var request = new PayDebtRequest(
            organizationIdValue,
            new MoneyDto(DefaultCurrencyCode, DebtPaymentAmountMinorUnits),
            NormalizeReason(),
            idempotencyKeyFactory.Create("debt-payment"));

        await ExecuteBackendConfirmationAsync(
            player.PlayerAccountId,
            async token =>
            {
                var summary = await apiClient.PayDebtAsync(player.PlayerAccountId, request, token);
                ApplyWalletSummary(summary);
                StatusMessage = "Player debt payment confirmed.";
            },
            cancellationToken);
    }

    private async Task ExecuteBackendConfirmationAsync(
        Guid playerAccountId,
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = WaitingForBackendConfirmation;
        IsBusy = true;

        try
        {
            await execute(cancellationToken);
            await RefreshSelectedPlayerStateAsync(playerAccountId, cancellationToken);
            PendingOperation = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = CreateUserFacingError(exception);
            PendingOperation = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecutePlayerOperationAsync(
        Func<CancellationToken, Task> execute,
        CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        PendingOperation = null;
        IsBusy = true;

        try
        {
            await execute(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
        {
            ErrorMessage = CreateUserFacingError(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshSelectedPlayerStateAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var summary = await apiClient.GetWalletSummaryAsync(playerAccountId, cancellationToken);
        ApplyWalletSummary(summary);

        var packages = await apiClient.GetPlayerPackagesAsync(playerAccountId, cancellationToken);
        PlayerPackages.Clear();
        foreach (var package in packages)
        {
            PlayerPackages.Add(new PlayerPackageSummaryViewModel(package));
        }
    }

    private void ApplySelectedPlayerSnapshot(PlayerSearchResultViewModel? player)
    {
        WalletSummary = null;
        PlayerPackages.Clear();

        if (player is null)
        {
            WalletBalanceMinorUnits = 0;
            DebtBalanceMinorUnits = 0;
            return;
        }

        WalletBalanceMinorUnits = player.WalletBalanceMinorUnits;
        DebtBalanceMinorUnits = player.DebtBalanceMinorUnits;
    }

    private void ApplyWalletSummary(WalletSummaryDto summary)
    {
        WalletSummary = summary;
        WalletBalanceMinorUnits = summary.WalletBalance.MinorUnits;
        DebtBalanceMinorUnits = summary.DebtBalance.MinorUnits;
    }

    private bool TryGetSelectedPlayer(out PlayerSearchResultViewModel player)
    {
        if (SelectedPlayer is null)
        {
            SetValidationError("Select a player first.");
            player = null!;
            return false;
        }

        player = SelectedPlayer;
        return true;
    }

    private bool TryGetOperatorContext(out Guid organizationIdValue, out Guid branchIdValue)
    {
        if (!TryGetOrganizationContext(out organizationIdValue) || branchId == Guid.Empty)
        {
            SetValidationError("Operator context is not loaded.");
            branchIdValue = Guid.Empty;
            return false;
        }

        branchIdValue = branchId;
        return true;
    }

    private bool TryGetOrganizationContext(out Guid organizationIdValue)
    {
        if (organizationId == Guid.Empty)
        {
            SetValidationError("Operator context is not loaded.");
            organizationIdValue = Guid.Empty;
            return false;
        }

        organizationIdValue = organizationId;
        return true;
    }

    private bool TryValidateDisplayName(out string displayName)
    {
        displayName = NewPlayerDisplayName.Trim();
        if (displayName.Length == 0)
        {
            SetValidationError("Player display name is required.");
            return false;
        }

        return true;
    }

    private bool TryValidatePositiveMoney(long amountMinorUnits, string message)
    {
        if (amountMinorUnits <= 0)
        {
            SetValidationError(message);
            return false;
        }

        return true;
    }

    private string NormalizeReason()
    {
        return string.IsNullOrWhiteSpace(MoneyReason)
            ? "operator request"
            : MoneyReason.Trim();
    }

    private void SetValidationError(string message)
    {
        ErrorMessage = message;
        StatusMessage = null;
        PendingOperation = null;
    }

    private static string CreateUserFacingError(Exception exception)
    {
        if (exception is HttpRequestException httpException)
        {
            return httpException.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                ? $"Permission denied: {httpException.Message}"
                : $"Network or API error: {httpException.Message}";
        }

        return exception.Message;
    }

    private bool CanRunSelectedPlayerCommand()
    {
        return !IsBusy && SelectedPlayer is not null;
    }

    private void NotifyCommandStates()
    {
        searchCommand.NotifyCanExecuteChanged();
        createPlayerCommand.NotifyCanExecuteChanged();
        refreshSelectedPlayerCommand.NotifyCanExecuteChanged();
        topUpWalletCommand.NotifyCanExecuteChanged();
        payDebtCommand.NotifyCanExecuteChanged();
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

    private static string FormatMoney(long minorUnits)
    {
        return (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }
}

public sealed class PlayerSearchResultViewModel
{
    public PlayerSearchResultViewModel(PlayerSearchResultDto dto)
    {
        PlayerAccountId = dto.PlayerAccountId;
        DisplayName = dto.DisplayName;
        PhoneNumber = dto.PhoneNumber;
        WalletBalanceMinorUnits = dto.WalletBalanceMinorUnits;
        DebtBalanceMinorUnits = dto.DebtBalanceMinorUnits;
        ActivePackageCount = dto.ActivePackageCount;
        IsActive = dto.IsActive;
    }

    public Guid PlayerAccountId { get; }

    public string DisplayName { get; }

    public string? PhoneNumber { get; }

    public long WalletBalanceMinorUnits { get; }

    public long DebtBalanceMinorUnits { get; }

    public int ActivePackageCount { get; }

    public bool IsActive { get; }

    public string BalanceSummary =>
        $"Wallet {FormatMoney(WalletBalanceMinorUnits)} USD / debt {FormatMoney(DebtBalanceMinorUnits)} USD";

    public string PackageSummary => ActivePackageCount == 1
        ? "1 active package"
        : $"{ActivePackageCount} active packages";

    private static string FormatMoney(long minorUnits)
    {
        return (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }
}

public sealed class PlayerPackageSummaryViewModel
{
    public PlayerPackageSummaryViewModel(PlayerPackageDto dto)
    {
        PlayerPackageId = dto.PlayerPackageId;
        Name = dto.Name;
        RemainingIncludedSeconds = dto.RemainingIncludedSeconds;
        RemainingBonusSeconds = dto.RemainingBonusSeconds;
        ExpiresAtUtc = dto.ExpiresAtUtc;
    }

    public Guid PlayerPackageId { get; }

    public string Name { get; }

    public int RemainingIncludedSeconds { get; }

    public int RemainingBonusSeconds { get; }

    public DateTimeOffset? ExpiresAtUtc { get; }

    public string RemainingSummary => $"{RemainingIncludedSeconds + RemainingBonusSeconds} seconds remaining";
}
