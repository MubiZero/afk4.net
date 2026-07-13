using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Players;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Operator.App.Tests;

public sealed class PlayerSearchViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid PlayerAccountId = Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2");

    [Fact]
    public async Task SearchAsync_RequiresTwoCharactersAndShowsBackendResults()
    {
        var apiClient = new RecordingPlayerApiClient
        {
            SearchResults =
            [
                new PlayerSearchResultDto(
                    PlayerAccountId,
                    "Alex Player",
                    "+992000000001",
                    WalletBalanceMinorUnits: 12000,
                    DebtBalanceMinorUnits: 0,
                    ActivePackageCount: 1,
                    IsActive: true,
                    CreatedAtUtc: default,
                    LastActivityAtUtc: null,
                    ActivePackageName: null,
                    ActivePackageRemainingMinutes: 0)
            ]
        };
        var viewModel = new PlayerSearchViewModel(apiClient);
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SearchText = "Al";

        await viewModel.SearchAsync(CancellationToken.None);

        Assert.Single(viewModel.Results);
        Assert.Equal("Alex Player", viewModel.Results[0].DisplayName);
        Assert.Equal("Кошелек 120.00 TJS / долг 0.00 TJS", viewModel.Results[0].BalanceSummary);
        Assert.Equal(BranchId, apiClient.LastSearchBranchId);
        Assert.Equal("Al", apiClient.LastSearchQuery);
    }

    [Fact]
    public async Task SearchAsync_WithShortText_DoesNotCallBackend()
    {
        var apiClient = new RecordingPlayerApiClient();
        var viewModel = new PlayerSearchViewModel(apiClient);
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SearchText = "A";

        await viewModel.SearchAsync(CancellationToken.None);

        Assert.Empty(viewModel.Results);
        Assert.False(viewModel.HasSearchResults);
        Assert.Equal(0, apiClient.SearchCallCount);
        Assert.Equal("Введите минимум два символа для поиска.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CreatePlayerAsync_SendsIdempotentRequestAndSelectsCreatedPlayer()
    {
        var apiClient = new RecordingPlayerApiClient
        {
            CreatedPlayer = new PlayerAccountDto(
                PlayerAccountId,
                OrganizationId,
                BranchId,
                "Alex Player",
                "+992000000001",
                IsActive: true,
                DateTimeOffset.Parse("2026-05-14T09:00:00Z")),
            WalletSummary = CreateWalletSummary(walletMinorUnits: 0, debtMinorUnits: 0)
        };
        var viewModel = new PlayerSearchViewModel(apiClient, new FixedIdempotencyKeyFactory("player-create-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.NewPlayerDisplayName = "Alex Player";
        viewModel.NewPlayerPhoneNumber = "+992000000001";

        await viewModel.CreatePlayerAsync(CancellationToken.None);

        Assert.Equal("player-create-001", apiClient.LastCreateRequest?.IdempotencyKey);
        Assert.Equal(OrganizationId, apiClient.LastCreateRequest?.OrganizationId);
        Assert.Equal(BranchId, apiClient.LastCreateBranchId);
        Assert.Equal("Alex Player", viewModel.SelectedPlayer?.DisplayName);
        Assert.True(viewModel.HasSearchResults);
        Assert.Equal(0, viewModel.WalletBalanceMinorUnits);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task TopUpWalletAsync_WaitsForBackendConfirmationAndRefreshesSummary()
    {
        var apiClient = new RecordingPlayerApiClient
        {
            WalletSummary = CreateWalletSummary(walletMinorUnits: 15000, debtMinorUnits: 0),
            Packages =
            [
                new PlayerPackageDto(
                    Guid.Parse("77777777-7777-4777-8777-777777777777"),
                    Guid.Parse("88888888-8888-4888-8888-888888888888"),
                    PlayerAccountId,
                    "Night pack",
                    new MoneyDto("USD", 10000),
                    IncludedSeconds: 3600,
                    BonusSeconds: 0,
                    RemainingIncludedSeconds: 1800,
                    RemainingBonusSeconds: 0,
                    PurchasedAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                    ExpiresAtUtc: null)
            ],
            HoldTopUp = new TaskCompletionSource<WalletSummaryDto>()
        };
        var viewModel = new PlayerSearchViewModel(apiClient, new FixedIdempotencyKeyFactory("wallet-top-up-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SelectPlayer(new PlayerSearchResultViewModel(new PlayerSearchResultDto(
            PlayerAccountId,
            "Alex Player",
            "+992000000001",
            WalletBalanceMinorUnits: 12000,
            DebtBalanceMinorUnits: 0,
            ActivePackageCount: 1,
            IsActive: true,
            CreatedAtUtc: default,
            LastActivityAtUtc: null,
            ActivePackageName: null,
            ActivePackageRemainingMinutes: 0)));
        viewModel.TopUpAmountMinorUnits = 3000;
        viewModel.MoneyReason = "cash top-up";

        var operation = viewModel.TopUpWalletAsync(CancellationToken.None);

        Assert.True(viewModel.IsBusy);
        Assert.Equal("Ожидаем подтверждение сервера", viewModel.PendingOperation);

        apiClient.HoldTopUp.SetResult(CreateWalletSummary(walletMinorUnits: 15000, debtMinorUnits: 0));
        await operation;

        Assert.Equal("wallet-top-up-001", apiClient.LastTopUpRequest?.IdempotencyKey);
        Assert.Equal("TJS", apiClient.LastTopUpRequest?.Amount.CurrencyCode);
        Assert.Equal(3000, apiClient.LastTopUpRequest?.Amount.MinorUnits);
        Assert.Equal(15000, viewModel.WalletBalanceMinorUnits);
        Assert.Equal(0, viewModel.DebtBalanceMinorUnits);
        Assert.Single(viewModel.PlayerPackages);
        Assert.Null(viewModel.ErrorMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task PayDebtAsync_SendsPaymentAndRefreshesSummary()
    {
        var apiClient = new RecordingPlayerApiClient
        {
            WalletSummary = CreateWalletSummary(walletMinorUnits: 12000, debtMinorUnits: 0)
        };
        var viewModel = new PlayerSearchViewModel(apiClient, new FixedIdempotencyKeyFactory("debt-pay-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SelectPlayer(new PlayerSearchResultViewModel(new PlayerSearchResultDto(
            PlayerAccountId,
            "Alex Player",
            "+992000000001",
            WalletBalanceMinorUnits: 12000,
            DebtBalanceMinorUnits: 5000,
            ActivePackageCount: 0,
            IsActive: true,
            CreatedAtUtc: default,
            LastActivityAtUtc: null,
            ActivePackageName: null,
            ActivePackageRemainingMinutes: 0)));
        viewModel.DebtPaymentAmountMinorUnits = 5000;
        viewModel.MoneyReason = "debt cash";

        await viewModel.PayDebtAsync(CancellationToken.None);

        Assert.Equal("debt-pay-001", apiClient.LastPayDebtRequest?.IdempotencyKey);
        Assert.Equal("TJS", apiClient.LastPayDebtRequest?.Amount.CurrencyCode);
        Assert.Equal(5000, apiClient.LastPayDebtRequest?.Amount.MinorUnits);
        Assert.Equal(0, viewModel.DebtBalanceMinorUnits);
        Assert.Equal("Погашение долга подтверждено.", viewModel.StatusMessage);
    }

    private static WalletSummaryDto CreateWalletSummary(long walletMinorUnits, long debtMinorUnits)
    {
        return new WalletSummaryDto(
            PlayerAccountId,
            new MoneyDto("USD", walletMinorUnits),
            new MoneyDto("USD", debtMinorUnits),
            []);
    }

    private sealed class FixedIdempotencyKeyFactory(string key) : IIdempotencyKeyFactory
    {
        public string Create(string operationName)
        {
            return key;
        }
    }

    private sealed class RecordingPlayerApiClient : IOperatorPlayerApiClient
    {
        public IReadOnlyList<PlayerSearchResultDto> SearchResults { get; init; } = [];

        public PlayerAccountDto? CreatedPlayer { get; init; }

        public WalletSummaryDto WalletSummary { get; init; } = CreateWalletSummary(walletMinorUnits: 0, debtMinorUnits: 0);

        public IReadOnlyList<PlayerPackageDto> Packages { get; init; } = [];

        public TaskCompletionSource<WalletSummaryDto>? HoldTopUp { get; init; }

        public int SearchCallCount { get; private set; }

        public Guid LastSearchBranchId { get; private set; }

        public string? LastSearchQuery { get; private set; }

        public Guid LastCreateBranchId { get; private set; }

        public CreatePlayerAccountRequest? LastCreateRequest { get; private set; }

        public TopUpWalletRequest? LastTopUpRequest { get; private set; }

        public PayDebtRequest? LastPayDebtRequest { get; private set; }

        public Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
            Guid branchId,
            string query,
            int limit,
            CancellationToken cancellationToken)
        {
            SearchCallCount++;
            LastSearchBranchId = branchId;
            LastSearchQuery = query;
            return Task.FromResult(SearchResults);
        }

        public Task<PlayerAccountDto> CreatePlayerAsync(
            Guid branchId,
            CreatePlayerAccountRequest request,
            CancellationToken cancellationToken)
        {
            LastCreateBranchId = branchId;
            LastCreateRequest = request;
            return Task.FromResult(CreatedPlayer ?? new PlayerAccountDto(
                PlayerAccountId,
                request.OrganizationId,
                branchId,
                request.DisplayName,
                request.PhoneNumber,
                IsActive: true,
                DateTimeOffset.Parse("2026-05-14T09:00:00Z")));
        }

        public Task<WalletSummaryDto> GetWalletSummaryAsync(Guid playerAccountId, CancellationToken cancellationToken)
        {
            return Task.FromResult(WalletSummary);
        }

        public Task<IReadOnlyList<PlayerPackageDto>> GetPlayerPackagesAsync(Guid playerAccountId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Packages);
        }

        public Task<WalletSummaryDto> TopUpWalletAsync(
            Guid playerAccountId,
            TopUpWalletRequest request,
            CancellationToken cancellationToken)
        {
            LastTopUpRequest = request;
            return HoldTopUp?.Task ?? Task.FromResult(WalletSummary);
        }

        public Task<WalletSummaryDto> PayDebtAsync(
            Guid playerAccountId,
            PayDebtRequest request,
            CancellationToken cancellationToken)
        {
            LastPayDebtRequest = request;
            return Task.FromResult(WalletSummary);
        }
    }
}
