using AFK4.OrganizationAdmin.App.Auth;
using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.OrganizationAdmin.App.Players;
using AFK4.OrganizationAdmin.App.Pos;
using AFK4.OrganizationAdmin.App.Settings;
using AFK4.OrganizationAdmin.App.Shell;
using AFK4.OrganizationAdmin.App.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;
using AFK4.Shared.Contracts.Reports;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminShellViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public void SignIn_WithCashierPermissions_ShowsOperationalWorkspacesOnly()
    {
        var context = CreateContext(
            OrganizationPermissionNames.ViewFloorMap,
            OrganizationPermissionNames.StartSession,
            OrganizationPermissionNames.CreatePosSale,
            OrganizationPermissionNames.PayPosSale,
            OrganizationPermissionNames.OpenShift,
            OrganizationPermissionNames.ViewShift);
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(context);

        Assert.True(shell.IsSignedIn);
        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.FloorMap);
        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Pos);
        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Shifts);
        Assert.DoesNotContain(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
        Assert.Equal(OrganizationAdminWorkspaceKind.FloorMap, shell.SelectedWorkspace);
        Assert.True(shell.NavigationItems.Single(item => item.Kind == OrganizationAdminWorkspaceKind.FloorMap).IsSelected);
    }

    [Fact]
    public void ApplySignedInContext_WithDevicePermissions_ShowsSettings()
    {
        var context = CreateContext(
            OrganizationPermissionNames.ViewFloorMap,
            OrganizationPermissionNames.ViewDeviceDetail,
            OrganizationPermissionNames.RotateDeviceCredential);
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(context);

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
    }

    [Fact]
    public void ApplySignedInContext_WithUpdateStatusPermission_ShowsSettings()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ViewUpdateStatus));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
        Assert.Contains(shell.Settings.Panels, panel => panel.Key == "updates");
    }

    [Fact]
    public void ApplySignedInContext_WithUpdateManagementPermission_ShowsSettings()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ManageUpdateRollouts));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
        Assert.Contains(shell.Settings.Panels, panel => panel.Key == "updates");
    }

    [Fact]
    public void ApplySignedInContext_WithAuditPermission_ShowsSettings()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ViewAudit));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
        Assert.Contains(shell.Settings.Panels, panel => panel.Key == "audit");
    }

    [Fact]
    public void ApplySignedInContext_WithDiagnosticsPermission_ShowsSettings()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ViewDiagnostics));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
        Assert.Contains(shell.Settings.Panels, panel => panel.Key == "diagnostics");
    }

    [Fact]
    public void ApplySignedInContext_WithPilotSetupPermission_ShowsSettings()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ManageBranchStaff));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Settings);
        Assert.Contains(shell.Settings.Panels, panel => panel.Key == "pilot-setup");
        Assert.True(shell.Settings.PilotSetup?.CanSetupStaff);
    }

    [Fact]
    public void ApplySignedInContext_WithPlayerViewPermission_ShowsPlayers()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ViewPlayers));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Players);
    }

    [Fact]
    public void ApplySignedInContext_WithReportPermission_ShowsShifts()
    {
        var shell = new OrganizationAdminShellViewModel();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ViewReports));

        Assert.Contains(shell.NavigationItems, item => item.Kind == OrganizationAdminWorkspaceKind.Shifts);
    }

    [Fact]
    public void NavigateCommand_ChangesSelectedWorkspaceOnlyWhenItemIsAllowed()
    {
        var context = CreateContext(
            OrganizationPermissionNames.ViewFloorMap,
            OrganizationPermissionNames.CreatePosSale);
        var shell = new OrganizationAdminShellViewModel();
        shell.ApplySignedInContext(context);

        shell.NavigateCommand.Execute(OrganizationAdminWorkspaceKind.Pos);
        shell.NavigateCommand.Execute(OrganizationAdminWorkspaceKind.Settings);

        Assert.Equal(OrganizationAdminWorkspaceKind.Pos, shell.SelectedWorkspace);
        Assert.True(shell.NavigationItems.Single(item => item.Kind == OrganizationAdminWorkspaceKind.Pos).IsSelected);
        Assert.False(shell.NavigationItems.Single(item => item.Kind == OrganizationAdminWorkspaceKind.FloorMap).IsSelected);
    }

    [Fact]
    public void SignOutCommand_ClearsUserAndNavigation()
    {
        var shell = new OrganizationAdminShellViewModel();
        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.ViewFloorMap));

        shell.SignOutCommand.Execute(null);

        Assert.False(shell.IsSignedIn);
        Assert.Null(shell.CurrentUser);
        Assert.Empty(shell.NavigationItems);
        Assert.Null(shell.SelectedWorkspace);
    }

    [Fact]
    public void ApplySignedInContext_WiresShiftStateIntoPosWorkspace()
    {
        var pos = new PosWorkspaceViewModel(new RecordingPosApiClient());
        var shifts = new ShiftWorkspaceViewModel(new RecordingShiftApiClient());
        var shell = new OrganizationAdminShellViewModel(
            new SignInViewModel(new UnconfiguredOperatorAuthApiClient()),
            new FloorMapWorkspaceViewModel(),
            new PlayerSearchViewModel(new UnconfiguredOperatorPlayerApiClient()),
            pos,
            shifts,
            new SettingsWorkspaceViewModel(new HashSet<string>()));
        var shift = CreateOpenShift();

        shell.ApplySignedInContext(CreateContext(OrganizationPermissionNames.CreatePosSale, OrganizationPermissionNames.ViewShift));
        shifts.SetCurrentShift(shift);

        Assert.Same(pos, shell.Pos);
        Assert.Same(shifts, shell.Shifts);
        Assert.Equal(shift.ShiftId, shell.Pos.CurrentShiftId);
        Assert.Equal(ShiftStateNames.Open, shell.CurrentShiftState);
    }

    private static OperatorUserContext CreateContext(params string[] permissions)
    {
        return new OperatorUserContext(
            StaffUserId,
            OrganizationId,
            BranchId,
            "Cashier One",
            permissions.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private static ShiftDto CreateOpenShift()
    {
        return new ShiftDto(
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            OrganizationId,
            BranchId,
            StaffUserId,
            null,
            ShiftStateNames.Open,
            new MoneyDto("USD", 50000),
            null,
            null,
            null,
            "Morning shift",
            string.Empty,
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            null);
    }

    private sealed class RecordingPosApiClient : IOperatorPosApiClient
    {
        public Task<IReadOnlyList<PosProductDto>> GetCatalogAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PosProductDto>>([]);
        }

        public Task<PosSaleDto> CreateSaleAsync(
            Guid branchId,
            CreatePosSaleRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PosSaleDto> PaySaleManualAsync(
            Guid saleId,
            ManualPaymentRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PosSaleDto> RefundSaleAsync(
            Guid saleId,
            RefundPosSaleRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PosSaleDto> VoidSaleAsync(
            Guid saleId,
            VoidPosSaleRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PosSaleDto> GetSaleAsync(
            Guid saleId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ReceiptDto> GetReceiptAsync(
            Guid receiptId,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingShiftApiClient : IOperatorShiftApiClient
    {
        public Task<ShiftDto> OpenShiftAsync(
            Guid branchId,
            OpenShiftRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ShiftDto?> GetCurrentShiftAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ShiftDto?>(null);
        }

        public Task<CashMovementDto> RecordCashMovementAsync(
            Guid shiftId,
            RecordCashMovementRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ShiftDto> CloseShiftAsync(
            Guid shiftId,
            CloseShiftRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ShiftReportResultDto> GetShiftReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SalesReportResultDto> GetSalesReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<CashOperationReportResultDto> GetCashOperationReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExportShiftReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExportSalesReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExportGameplayTimeReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExportCashOperationReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<string> ExportOperatorActionReportCsvAsync(
            Guid branchId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
