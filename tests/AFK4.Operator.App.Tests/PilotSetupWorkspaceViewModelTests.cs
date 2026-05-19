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
        Assert.Equal("Standard", viewModel.TariffName);
        Assert.Equal("TJS", viewModel.CurrencyCode);
        Assert.Equal(100, viewModel.PricePerMinuteMinorUnits);
        Assert.Equal("Drinks", viewModel.ProductCategoryName);
        Assert.Equal("Water 0.5", viewModel.ProductName);
        Assert.Equal("WATER-05", viewModel.ProductSku);
        Assert.Equal(3, viewModel.StaffUsers.Count);
        Assert.Contains(viewModel.StaffUsers, staffUser => staffUser.RoleName == "cashier_operator");
        Assert.Contains(viewModel.StaffUsers, staffUser => staffUser.RoleName == "technician");
        Assert.Contains(viewModel.StaffUsers, staffUser => staffUser.RoleName == "shift_supervisor");
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

    private static PilotSetupWorkspaceViewModel CreateViewModel()
    {
        return new PilotSetupWorkspaceViewModel(new RecordingPilotSetupApiClient());
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
