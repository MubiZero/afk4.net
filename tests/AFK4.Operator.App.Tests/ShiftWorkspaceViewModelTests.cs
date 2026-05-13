using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Operator.App.Tests;

public sealed class ShiftWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ShiftId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public async Task OpenShiftAsync_SendsStartingCashAndStoresCurrentShift()
    {
        var apiClient = new RecordingShiftApiClient();
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("shift-open-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.StartingCashMinorUnits = 50000;
        viewModel.OpeningNote = "Morning shift";

        await viewModel.OpenShiftAsync(CancellationToken.None);

        Assert.Equal("shift-open-001", apiClient.LastOpenRequest?.IdempotencyKey);
        Assert.Equal(50000, apiClient.LastOpenRequest?.StartingCash.MinorUnits);
        Assert.Equal("Morning shift", apiClient.LastOpenRequest?.OpeningNote);
        Assert.Equal(BranchId, apiClient.LastOpenBranchId);
        Assert.Equal(ShiftStateNames.Open, viewModel.CurrentShift?.State);
        Assert.True(viewModel.CanRunMoneyWorkflows);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadCurrentShiftAsync_StoresOpenShiftAndExpectedCashState()
    {
        var apiClient = new RecordingShiftApiClient
        {
            CurrentShift = CreateShift(ShiftStateNames.Open, expectedCashMinorUnits: 51000)
        };
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("shift-current-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.LoadCurrentShiftAsync(CancellationToken.None);

        Assert.Equal(ShiftId, viewModel.CurrentShiftId);
        Assert.Equal(51000, viewModel.ExpectedCashMinorUnits);
        Assert.True(viewModel.CanRunMoneyWorkflows);
        Assert.Equal("Open shift loaded.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadCurrentShiftAsync_WithMissingCurrentShift_DisablesMoneyWorkflows()
    {
        var apiClient = new RecordingShiftApiClient
        {
            CurrentShift = null
        };
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("shift-current-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.LoadCurrentShiftAsync(CancellationToken.None);

        Assert.Null(viewModel.CurrentShift);
        Assert.False(viewModel.CanRunMoneyWorkflows);
        Assert.Equal("No open shift.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RecordCashMovementAsync_SendsMovementAndRefreshesCurrentShift()
    {
        var apiClient = new RecordingShiftApiClient();
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("cash-in-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SetCurrentShift(CreateShift(ShiftStateNames.Open));
        viewModel.CashMovementAmountMinorUnits = 2500;
        viewModel.CashMovementType = CashMovementTypeNames.CashIn;
        viewModel.CashMovementReason = "drawer correction";

        await viewModel.RecordCashMovementAsync(CancellationToken.None);

        Assert.Equal(ShiftId, apiClient.LastCashMovementShiftId);
        Assert.Equal("cash-in-001", apiClient.LastCashMovementRequest?.IdempotencyKey);
        Assert.Equal(CashMovementTypeNames.CashIn, apiClient.LastCashMovementRequest?.MovementType);
        Assert.Equal(2500, apiClient.LastCashMovementRequest?.Amount.MinorUnits);
        Assert.Equal("drawer correction", apiClient.LastCashMovementRequest?.Reason);
        Assert.Equal(1, apiClient.GetCurrentShiftCallCount);
        Assert.Equal("Cash movement recorded.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task CloseShiftAsync_SendsCountedCashAndDisablesMoneyWorkflows()
    {
        var apiClient = new RecordingShiftApiClient();
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("shift-close-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.SetCurrentShift(CreateShift(ShiftStateNames.Open));
        viewModel.CountedCashMinorUnits = 52500;
        viewModel.ClosingNote = "balanced";

        await viewModel.CloseShiftAsync(CancellationToken.None);

        Assert.Equal(ShiftId, apiClient.LastCloseShiftId);
        Assert.Equal("shift-close-001", apiClient.LastCloseRequest?.IdempotencyKey);
        Assert.Equal(52500, apiClient.LastCloseRequest?.CountedCash.MinorUnits);
        Assert.Equal("balanced", apiClient.LastCloseRequest?.ClosingNote);
        Assert.Equal(ShiftStateNames.Closed, viewModel.CurrentShift?.State);
        Assert.False(viewModel.CanRunMoneyWorkflows);
        Assert.Equal(52500, viewModel.CountedCashResultMinorUnits);
        Assert.Equal(1000, viewModel.DifferenceMinorUnits);
    }

    [Fact]
    public async Task MoneyWorkflowCommands_RequireCurrentOpenShift()
    {
        var apiClient = new RecordingShiftApiClient();
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("cash-in-001"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.CashMovementAmountMinorUnits = 1000;
        viewModel.CashMovementReason = "drawer correction";

        await viewModel.RecordCashMovementAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.CashMovementCallCount);
        Assert.Equal("Open shift is required before money operations.", viewModel.ErrorMessage);

        viewModel.SetCurrentShift(CreateShift(ShiftStateNames.Closed));

        await viewModel.CloseShiftAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.CloseShiftCallCount);
        Assert.False(viewModel.CanRunMoneyWorkflows);
        Assert.Equal("Open shift is required before money operations.", viewModel.ErrorMessage);
    }

    private static ShiftDto CreateShift(
        string state,
        long expectedCashMinorUnits = 51500,
        long countedCashMinorUnits = 52500)
    {
        var isClosed = state == ShiftStateNames.Closed;

        return new ShiftDto(
            ShiftId,
            OrganizationId,
            BranchId,
            StaffUserId,
            isClosed ? StaffUserId : null,
            state,
            new MoneyDto("USD", 50000),
            isClosed ? new MoneyDto("USD", countedCashMinorUnits) : null,
            isClosed ? new MoneyDto("USD", expectedCashMinorUnits) : new MoneyDto("USD", expectedCashMinorUnits),
            isClosed ? new MoneyDto("USD", countedCashMinorUnits - expectedCashMinorUnits) : null,
            "Morning shift",
            isClosed ? "balanced" : string.Empty,
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            isClosed ? DateTimeOffset.Parse("2026-05-14T18:00:00Z") : null);
    }

    private sealed class FixedIdempotencyKeyFactory(string key) : IIdempotencyKeyFactory
    {
        public string Create(string operationName)
        {
            return key;
        }
    }

    private sealed class RecordingShiftApiClient : IOperatorShiftApiClient
    {
        public ShiftDto? CurrentShift { get; init; } = CreateShift(ShiftStateNames.Open);

        public int GetCurrentShiftCallCount { get; private set; }

        public int CashMovementCallCount { get; private set; }

        public int CloseShiftCallCount { get; private set; }

        public Guid LastOpenBranchId { get; private set; }

        public OpenShiftRequest? LastOpenRequest { get; private set; }

        public Guid LastCurrentBranchId { get; private set; }

        public Guid LastCashMovementShiftId { get; private set; }

        public RecordCashMovementRequest? LastCashMovementRequest { get; private set; }

        public Guid LastCloseShiftId { get; private set; }

        public CloseShiftRequest? LastCloseRequest { get; private set; }

        public Task<ShiftDto> OpenShiftAsync(
            Guid branchId,
            OpenShiftRequest request,
            CancellationToken cancellationToken)
        {
            LastOpenBranchId = branchId;
            LastOpenRequest = request;
            return Task.FromResult(CreateShift(ShiftStateNames.Open, request.StartingCash.MinorUnits));
        }

        public Task<ShiftDto?> GetCurrentShiftAsync(Guid branchId, CancellationToken cancellationToken)
        {
            GetCurrentShiftCallCount++;
            LastCurrentBranchId = branchId;
            return Task.FromResult(CurrentShift);
        }

        public Task<CashMovementDto> RecordCashMovementAsync(
            Guid shiftId,
            RecordCashMovementRequest request,
            CancellationToken cancellationToken)
        {
            CashMovementCallCount++;
            LastCashMovementShiftId = shiftId;
            LastCashMovementRequest = request;
            return Task.FromResult(new CashMovementDto(
                Guid.Parse("66666666-6666-4666-8666-666666666666"),
                OrganizationId,
                BranchId,
                shiftId,
                StaffUserId,
                request.MovementType,
                request.Amount,
                request.Reason,
                DateTimeOffset.Parse("2026-05-14T10:00:00Z")));
        }

        public Task<ShiftDto> CloseShiftAsync(
            Guid shiftId,
            CloseShiftRequest request,
            CancellationToken cancellationToken)
        {
            CloseShiftCallCount++;
            LastCloseShiftId = shiftId;
            LastCloseRequest = request;
            return Task.FromResult(CreateShift(
                ShiftStateNames.Closed,
                expectedCashMinorUnits: 51500,
                countedCashMinorUnits: request.CountedCash.MinorUnits));
        }
    }
}
