using AFK4.Operator.App.PilotSetup;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.Tests;

public sealed class PilotSetupWorkspaceViewModelTests
{
    [Fact]
    public void Constructor_UsesPilotRunbookDefaults()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("Main Hall", viewModel.ZoneName);
        Assert.Equal("PC-", viewModel.SeatPrefix);
        Assert.Equal(10, viewModel.SeatCount);
        Assert.Equal(1, viewModel.SeatSortOrderStart);
        Assert.Equal("PC-001", viewModel.TargetAssignmentSeatName);
        Assert.Equal("Standard", viewModel.TariffName);
        Assert.Equal("TJS", viewModel.CurrencyCode);
        Assert.IsType<long>(viewModel.PricePerMinuteMinorUnits);
        Assert.Equal(100L, viewModel.PricePerMinuteMinorUnits);
        Assert.Equal(1, viewModel.MinimumBillableMinutes);
        Assert.Equal(1, viewModel.RoundingIncrementMinutes);
        Assert.Equal("Drinks", viewModel.ProductCategoryName);
        Assert.Equal("Water 0.5", viewModel.ProductName);
        Assert.Equal("WATER-05", viewModel.ProductSku);
        Assert.IsType<long>(viewModel.ProductPriceMinorUnits);
        Assert.Equal(500L, viewModel.ProductPriceMinorUnits);
        Assert.True(viewModel.ProductTrackStock);
        Assert.False(viewModel.ProductAllowNegativeStock);
        Assert.Equal(string.Empty, viewModel.DeviceIdText);
        Assert.Empty(viewModel.Results);
        Assert.Equal(3, viewModel.StaffUsers.Count);
        AssertStaffUser(
            viewModel.StaffUsers[0],
            "cashier.pilot@afk4.test",
            "Pilot Cashier",
            "ChangeMe!2026",
            "cashier_operator");
        AssertStaffUser(
            viewModel.StaffUsers[1],
            "technician.pilot@afk4.test",
            "Pilot Technician",
            "ChangeMe!2026",
            "technician");
        AssertStaffUser(
            viewModel.StaffUsers[2],
            "supervisor.pilot@afk4.test",
            "Pilot Supervisor",
            "ChangeMe!2026",
            "shift_supervisor");
    }

    [Fact]
    public void ApplyContext_UpdatesOrganizationAndBranchText()
    {
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
        var viewModel = CreateViewModel();

        viewModel.ApplyContext(organizationId, branchId);

        Assert.Equal(organizationId.ToString("D"), viewModel.OrganizationIdText);
        Assert.Equal(branchId.ToString("D"), viewModel.BranchIdText);
    }

    [Fact]
    public void ApplyPermissions_EnablesOnlyMatchingSections()
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyPermissions(new HashSet<string>
        {
            StaffPermissionNames.ManageLayout,
            StaffPermissionNames.AssignDeviceSeat
        });

        Assert.False(viewModel.CanSetupStaff);
        Assert.True(viewModel.CanSetupLayout);
        Assert.False(viewModel.CanSetupTariff);
        Assert.False(viewModel.CanSetupPos);
        Assert.True(viewModel.CanAssignDeviceSeat);
        Assert.True(viewModel.HasAnySetupPermission);
    }

    [Theory]
    [InlineData(StaffPermissionNames.ManageBranchStaff, true, false, false, false, false, true)]
    [InlineData(StaffPermissionNames.ManageTariffs, false, false, true, false, false, true)]
    [InlineData(StaffPermissionNames.ManagePosCatalog, false, false, false, true, false, true)]
    public void ApplyPermissions_MapsSinglePermissionToOnlyMatchingSection(
        string permission,
        bool canSetupStaff,
        bool canSetupLayout,
        bool canSetupTariff,
        bool canSetupPos,
        bool canAssignDeviceSeat,
        bool hasAnySetupPermission)
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyPermissions(new HashSet<string> { permission });

        AssertPermissions(
            viewModel,
            canSetupStaff,
            canSetupLayout,
            canSetupTariff,
            canSetupPos,
            canAssignDeviceSeat,
            hasAnySetupPermission);
    }

    [Fact]
    public void ApplyPermissions_WithNoPermissions_DisablesAllSections()
    {
        var viewModel = CreateViewModel();

        viewModel.ApplyPermissions(new HashSet<string>());

        AssertPermissions(
            viewModel,
            canSetupStaff: false,
            canSetupLayout: false,
            canSetupTariff: false,
            canSetupPos: false,
            canAssignDeviceSeat: false,
            hasAnySetupPermission: false);
    }

    private static PilotSetupWorkspaceViewModel CreateViewModel()
    {
        return new PilotSetupWorkspaceViewModel(new RecordingPilotSetupApiClient());
    }

    private static void AssertStaffUser(
        PilotSetupStaffUserViewModel staffUser,
        string userName,
        string displayName,
        string password,
        string roleName)
    {
        Assert.Equal(userName, staffUser.UserName);
        Assert.Equal(displayName, staffUser.DisplayName);
        Assert.Equal(password, staffUser.Password);
        Assert.Equal(roleName, staffUser.RoleName);
    }

    private static void AssertPermissions(
        PilotSetupWorkspaceViewModel viewModel,
        bool canSetupStaff,
        bool canSetupLayout,
        bool canSetupTariff,
        bool canSetupPos,
        bool canAssignDeviceSeat,
        bool hasAnySetupPermission)
    {
        Assert.Equal(canSetupStaff, viewModel.CanSetupStaff);
        Assert.Equal(canSetupLayout, viewModel.CanSetupLayout);
        Assert.Equal(canSetupTariff, viewModel.CanSetupTariff);
        Assert.Equal(canSetupPos, viewModel.CanSetupPos);
        Assert.Equal(canAssignDeviceSeat, viewModel.CanAssignDeviceSeat);
        Assert.Equal(hasAnySetupPermission, viewModel.HasAnySetupPermission);
    }

    private sealed class RecordingPilotSetupApiClient : IOperatorPilotSetupApiClient
    {
        public Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<StaffUserDto> CreateStaffUserAsync(
            Guid branchId,
            CreateStaffUserRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ZoneDto> CreateZoneAsync(
            Guid branchId,
            CreateZoneRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<SeatDto> CreateSeatAsync(
            Guid branchId,
            CreateSeatRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TariffDto> CreateTariffAsync(
            Guid branchId,
            CreateTariffRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<TariffVersionDto> CreateTariffVersionAsync(
            Guid branchId,
            Guid tariffId,
            CreateTariffVersionRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PosProductCategoryDto> CreateProductCategoryAsync(
            Guid branchId,
            CreateProductCategoryRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PosProductDto> CreateProductAsync(
            Guid branchId,
            CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(
            Guid branchId,
            Guid deviceId,
            AssignDeviceSeatRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
