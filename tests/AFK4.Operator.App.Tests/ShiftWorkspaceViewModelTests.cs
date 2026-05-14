using AFK4.Operator.App.Mvvm;
using AFK4.Operator.App.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;
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

    [Fact]
    public async Task LoadReportsAsync_LoadsAllOperationalReports()
    {
        var apiClient = new RecordingShiftApiClient
        {
            ShiftReport = new ShiftReportResultDto(
                [
                    new ShiftReportRowDto(
                        ShiftId,
                        OrganizationId,
                        BranchId,
                        StaffUserId,
                        StaffUserId,
                        ShiftStateNames.Closed,
                        new MoneyDto("USD", 50000),
                        new MoneyDto("USD", 1500),
                        new MoneyDto("USD", 2400),
                        new MoneyDto("USD", 0),
                        new MoneyDto("USD", 0),
                        new MoneyDto("USD", 53900),
                        new MoneyDto("USD", 53900),
                        new MoneyDto("USD", 0),
                        DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                        DateTimeOffset.Parse("2026-05-14T18:00:00Z"))
                ],
                Limit: 25),
            SalesReport = new SalesReportResultDto(
                [
                    new SalesReportRowDto(
                        Guid.Parse("77777777-7777-4777-8777-777777777777"),
                        OrganizationId,
                        BranchId,
                        ShiftId,
                        StaffUserId,
                        "paid",
                        new MoneyDto("USD", 2400),
                        new MoneyDto("USD", 2400),
                        new MoneyDto("USD", 0),
                        LineCount: 1,
                        ItemQuantity: 2,
                        CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
                        PaidAtUtc: DateTimeOffset.Parse("2026-05-14T12:01:00Z"),
                        RefundedAtUtc: null,
                        VoidedAtUtc: null)
                ],
                Limit: 25,
                GrossSalesTotal: new MoneyDto("USD", 2400),
                RefundsTotal: new MoneyDto("USD", 0),
                NetSalesTotal: new MoneyDto("USD", 2400)),
            GameplayTimeReport = new GameplayTimeReportResultDto(
                [
                    new GameplayTimeReportRowDto(
                        Guid.Parse("88888888-8888-4888-8888-888888888888"),
                        OrganizationId,
                        BranchId,
                        Guid.Parse("99999999-9999-4999-8999-999999999999"),
                        Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
                        StaffUserId,
                        "guest",
                        null,
                        "ended",
                        DurationSeconds: 7200,
                        PackageSeconds: 0,
                        BonusSeconds: 0,
                        GameplayRevenue: new MoneyDto("USD", 12000),
                        StartedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                        EndedAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
                        EndsAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"))
                ],
                Limit: 25,
                TotalDurationSeconds: 7200,
                TotalPackageSeconds: 0,
                TotalBonusSeconds: 0,
                GameplayRevenueTotal: new MoneyDto("USD", 12000)),
            CashOperationReport = new CashOperationReportResultDto(
                [
                    new CashOperationReportRowDto(
                        Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
                        OrganizationId,
                        BranchId,
                        ShiftId,
                        StaffUserId,
                        "cash_movement",
                        CashMovementTypeNames.CashIn,
                        new MoneyDto("USD", 5000),
                        "drawer correction",
                        DateTimeOffset.Parse("2026-05-14T11:00:00Z"))
                ],
                Limit: 25,
                CashInTotal: new MoneyDto("USD", 5000),
                CashOutTotal: new MoneyDto("USD", 0),
                NetCashTotal: new MoneyDto("USD", 5000)),
            OperatorActionReport = new OperatorActionReportResultDto(
                [
                    new OperatorActionReportRowDto(
                        StaffUserId,
                        "Manager One",
                        "sessions.start",
                        "succeeded",
                        Count: 3,
                        FirstAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                        LastAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"))
                ],
                Limit: 25,
                TotalActionCount: 3)
        };
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("unused"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.ReportFromUtcText = "2026-05-14T00:00:00Z";
        viewModel.ReportToUtcText = "2026-05-15T00:00:00Z";
        viewModel.ReportLimitText = "25";

        await viewModel.LoadReportsAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastShiftReportBranchId);
        Assert.Equal(BranchId, apiClient.LastSalesReportBranchId);
        Assert.Equal(BranchId, apiClient.LastGameplayTimeReportBranchId);
        Assert.Equal(BranchId, apiClient.LastCashOperationReportBranchId);
        Assert.Equal(BranchId, apiClient.LastOperatorActionReportBranchId);
        Assert.Equal(DateTimeOffset.Parse("2026-05-14T00:00:00Z"), apiClient.LastReportFromUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-05-15T00:00:00Z"), apiClient.LastReportToUtc);
        Assert.Equal(25, apiClient.LastReportLimit);
        Assert.Single(viewModel.ShiftReportRows);
        Assert.Single(viewModel.SalesReportRows);
        Assert.Single(viewModel.GameplayTimeReportRows);
        Assert.Single(viewModel.CashOperationReportRows);
        Assert.Single(viewModel.OperatorActionReportRows);
        Assert.Equal("1 shifts loaded.", viewModel.ShiftReportSummary);
        Assert.Equal("Gross USD 2400, refunds USD 0, net USD 2400.", viewModel.SalesReportSummary);
        Assert.Equal("Gameplay 7200 seconds, package 0, bonus 0, revenue USD 12000.", viewModel.GameplayTimeReportSummary);
        Assert.Equal("Cash in USD 5000, cash out USD 0, net USD 5000.", viewModel.CashOperationReportSummary);
        Assert.Equal("3 operator actions across 1 groups.", viewModel.OperatorActionReportSummary);
        Assert.Equal("Reports loaded.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadReportsAsync_RejectsInvalidLimit()
    {
        var apiClient = new RecordingShiftApiClient();
        var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("unused"));
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.ReportLimitText = "0";

        await viewModel.LoadReportsAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.ShiftReportCallCount);
        Assert.Equal("Report limit must be a positive whole number.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ExportShiftReportCsvAsync_UsesFiltersAndSavesCsv()
    {
        var apiClient = new RecordingShiftApiClient
        {
            ShiftReportCsv = "shift_id,state\r\n1,open\r\n"
        };
        var fileWriter = new RecordingReportCsvFileWriter("C:\\exports\\afk4-shifts-report.csv");
        var viewModel = new ShiftWorkspaceViewModel(
            apiClient,
            new FixedIdempotencyKeyFactory("unused"),
            fileWriter);
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.ReportFromUtcText = "2026-05-14T00:00:00Z";
        viewModel.ReportToUtcText = "2026-05-15T00:00:00Z";
        viewModel.ReportLimitText = "25";

        await viewModel.ExportShiftReportCsvAsync(CancellationToken.None);

        Assert.Equal(1, apiClient.ShiftReportCsvCallCount);
        Assert.Equal(BranchId, apiClient.LastShiftReportBranchId);
        Assert.Equal(DateTimeOffset.Parse("2026-05-14T00:00:00Z"), apiClient.LastReportFromUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-05-15T00:00:00Z"), apiClient.LastReportToUtc);
        Assert.Equal(25, apiClient.LastReportLimit);
        Assert.Equal("afk4-shifts-report.csv", fileWriter.LastSuggestedFileName);
        Assert.Equal("shift_id,state\r\n1,open\r\n", fileWriter.LastCsv);
        Assert.Equal("CSV exported to C:\\exports\\afk4-shifts-report.csv.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
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

        public ShiftReportResultDto ShiftReport { get; init; } = new([], 50);

        public SalesReportResultDto SalesReport { get; init; } = new(
            [],
            50,
            new MoneyDto("USD", 0),
            new MoneyDto("USD", 0),
            new MoneyDto("USD", 0));

        public GameplayTimeReportResultDto GameplayTimeReport { get; init; } = new(
            [],
            50,
            0,
            0,
            0,
            new MoneyDto("USD", 0));

        public CashOperationReportResultDto CashOperationReport { get; init; } = new(
            [],
            50,
            new MoneyDto("USD", 0),
            new MoneyDto("USD", 0),
            new MoneyDto("USD", 0));

        public OperatorActionReportResultDto OperatorActionReport { get; init; } = new([], 50, 0);

        public string ShiftReportCsv { get; init; } = "shift_id,state\r\n1,open\r\n";

        public string SalesReportCsv { get; init; } = "pos_sale_id,state\r\n1,paid\r\n";

        public string GameplayTimeReportCsv { get; init; } = "session_id,state\r\n1,ended\r\n";

        public string CashOperationReportCsv { get; init; } = "operation_id,type\r\n1,cash_in\r\n";

        public string OperatorActionReportCsv { get; init; } = "action,count\r\nsessions.start,1\r\n";

        public int GetCurrentShiftCallCount { get; private set; }

        public int CashMovementCallCount { get; private set; }

        public int CloseShiftCallCount { get; private set; }

        public int ShiftReportCallCount { get; private set; }

        public int ShiftReportCsvCallCount { get; private set; }

        public Guid LastOpenBranchId { get; private set; }

        public OpenShiftRequest? LastOpenRequest { get; private set; }

        public Guid LastCurrentBranchId { get; private set; }

        public Guid LastCashMovementShiftId { get; private set; }

        public RecordCashMovementRequest? LastCashMovementRequest { get; private set; }

        public Guid LastCloseShiftId { get; private set; }

        public CloseShiftRequest? LastCloseRequest { get; private set; }

        public Guid LastShiftReportBranchId { get; private set; }

        public Guid LastSalesReportBranchId { get; private set; }

        public Guid LastGameplayTimeReportBranchId { get; private set; }

        public Guid LastCashOperationReportBranchId { get; private set; }

        public Guid LastOperatorActionReportBranchId { get; private set; }

        public DateTimeOffset? LastReportFromUtc { get; private set; }

        public DateTimeOffset? LastReportToUtc { get; private set; }

        public int? LastReportLimit { get; private set; }

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

        public Task<ShiftReportResultDto> GetShiftReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            ShiftReportCallCount++;
            LastShiftReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(ShiftReport);
        }

        public Task<SalesReportResultDto> GetSalesReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastSalesReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(SalesReport);
        }

        public Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastGameplayTimeReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(GameplayTimeReport);
        }

        public Task<CashOperationReportResultDto> GetCashOperationReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastCashOperationReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(CashOperationReport);
        }

        public Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastOperatorActionReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(OperatorActionReport);
        }

        public Task<string> ExportShiftReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            ShiftReportCsvCallCount++;
            LastShiftReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(ShiftReportCsv);
        }

        public Task<string> ExportSalesReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastSalesReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(SalesReportCsv);
        }

        public Task<string> ExportGameplayTimeReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastGameplayTimeReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(GameplayTimeReportCsv);
        }

        public Task<string> ExportCashOperationReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastCashOperationReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(CashOperationReportCsv);
        }

        public Task<string> ExportOperatorActionReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            LastOperatorActionReportBranchId = branchId;
            LastReportFromUtc = fromUtc;
            LastReportToUtc = toUtc;
            LastReportLimit = limit;
            return Task.FromResult(OperatorActionReportCsv);
        }
    }

    private sealed class RecordingReportCsvFileWriter(string savedPath) : IReportCsvFileWriter
    {
        public string? LastSuggestedFileName { get; private set; }

        public string? LastCsv { get; private set; }

        public Task<string?> SaveAsync(
            string suggestedFileName,
            string csv,
            CancellationToken cancellationToken)
        {
            LastSuggestedFileName = suggestedFileName;
            LastCsv = csv;
            return Task.FromResult<string?>(savedPath);
        }
    }
}
