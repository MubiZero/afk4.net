using System.Security.Cryptography;
using System.Text;
using AFK4.Operator.App.PilotSetup;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.Tests;

public sealed class PilotSetupWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public void Constructor_UsesPilotRunbookDefaults()
    {
        var testStartDateUtc = DateTimeOffset.UtcNow.Date;
        var viewModel = CreateViewModel();
        var testEndDateUtc = DateTimeOffset.UtcNow.Date;

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
        Assert.Equal(TimeSpan.Zero, viewModel.EffectiveFromUtc.Offset);
        Assert.Equal(TimeSpan.Zero, viewModel.EffectiveFromUtc.TimeOfDay);
        Assert.InRange(viewModel.EffectiveFromUtc.Date, testStartDateUtc, testEndDateUtc);
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
    [InlineData(StaffPermissionNames.ManageLayout, false, true, false, false, false, true)]
    [InlineData(StaffPermissionNames.ManageTariffs, false, false, true, false, false, true)]
    [InlineData(StaffPermissionNames.ManagePosCatalog, false, false, false, true, false, true)]
    [InlineData(StaffPermissionNames.AssignDeviceSeat, false, false, false, false, true, true)]
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

    [Fact]
    public async Task ApplyAsync_WithAllPermissions_CallsSetupEndpointsInOrder()
    {
        var apiClient = new RecordingPilotSetupApiClient();
        var viewModel = CreateReadyViewModel(
            apiClient,
            StaffPermissionNames.ManageBranchStaff,
            StaffPermissionNames.ManageLayout,
            StaffPermissionNames.ManageTariffs,
            StaffPermissionNames.ManagePosCatalog,
            StaffPermissionNames.AssignDeviceSeat);
        viewModel.DeviceIdText = RecordingPilotSetupApiClient.DeviceId.ToString("D");

        await viewModel.ApplyAsync(CancellationToken.None);

        Assert.Equal(
        [
            "get-staff",
            "create-staff:cashier.pilot@afk4.test",
            "create-staff:technician.pilot@afk4.test",
            "create-staff:supervisor.pilot@afk4.test",
            "get-zones",
            "create-zone:Main Hall",
            "create-seat:PC-001",
            "create-seat:PC-002",
            "create-seat:PC-003",
            "create-seat:PC-004",
            "create-seat:PC-005",
            "create-seat:PC-006",
            "create-seat:PC-007",
            "create-seat:PC-008",
            "create-seat:PC-009",
            "create-seat:PC-010",
            "create-tariff:Standard",
            "create-tariff-version:TJS",
            "create-pos-category:Drinks",
            "create-pos-product:WATER-05",
            "assign-device-seat"
        ], apiClient.Calls);
        Assert.Equal("Pilot setup applied.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Contains(viewModel.Results, result => result.Key == "device-assignment" && result.State == "created");
    }

    [Fact]
    public async Task ApplyAsync_ReusesExistingStaffZoneAndSeats()
    {
        var apiClient = new RecordingPilotSetupApiClient
        {
            ExistingStaffUserNames = new HashSet<string> { "cashier.pilot@afk4.test" },
            ExistingZoneName = "Main Hall",
            ExistingSeatNames = new HashSet<string> { "PC-001", "PC-002" }
        };
        var viewModel = CreateReadyViewModel(
            apiClient,
            StaffPermissionNames.ManageBranchStaff,
            StaffPermissionNames.ManageLayout);
        viewModel.SeatCount = 3;

        await viewModel.ApplyAsync(CancellationToken.None);

        Assert.DoesNotContain("create-staff:cashier.pilot@afk4.test", apiClient.Calls);
        Assert.DoesNotContain("create-zone:Main Hall", apiClient.Calls);
        Assert.DoesNotContain("create-seat:PC-001", apiClient.Calls);
        Assert.DoesNotContain("create-seat:PC-002", apiClient.Calls);
        Assert.Contains("create-seat:PC-003", apiClient.Calls);
        Assert.Contains(viewModel.Results, result => result.Key == "staff:cashier.pilot@afk4.test" && result.State == "reused");
    }

    [Fact]
    public async Task ApplyAsync_WithInvalidDeviceId_DoesNotCallApiAndShowsError()
    {
        var apiClient = new RecordingPilotSetupApiClient();
        var viewModel = CreateReadyViewModel(apiClient, StaffPermissionNames.AssignDeviceSeat);
        viewModel.DeviceIdText = "not-a-guid";

        await viewModel.ApplyAsync(CancellationToken.None);

        Assert.Empty(apiClient.Calls);
        Assert.Equal("DeviceId must be a valid GUID.", viewModel.ErrorMessage);
        Assert.Contains(viewModel.Results, result => result.Key == "device-assignment" && result.State == "failed");
    }

    [Fact]
    public async Task ApplyAsync_WhenApiFails_StopsAtFailedStepAndKeepsPasswordOutOfMessages()
    {
        var apiClient = new RecordingPilotSetupApiClient
        {
            FailOnCall = "create-staff:technician.pilot@afk4.test"
        };
        var viewModel = CreateReadyViewModel(apiClient, StaffPermissionNames.ManageBranchStaff);

        await viewModel.ApplyAsync(CancellationToken.None);

        Assert.Equal("Platform API returned 500 Internal Server Error: staff create failed", viewModel.ErrorMessage);
        Assert.DoesNotContain("ChangeMe!2026", viewModel.ErrorMessage);
        Assert.All(viewModel.Results, result => Assert.DoesNotContain("ChangeMe!2026", result.Detail));
        Assert.DoesNotContain("create-staff:supervisor.pilot@afk4.test", apiClient.Calls);
    }

    private static PilotSetupWorkspaceViewModel CreateViewModel()
    {
        return new PilotSetupWorkspaceViewModel(new RecordingPilotSetupApiClient());
    }

    private static PilotSetupWorkspaceViewModel CreateReadyViewModel(
        RecordingPilotSetupApiClient apiClient,
        params string[] permissions)
    {
        var viewModel = new PilotSetupWorkspaceViewModel(apiClient);
        viewModel.ApplyContext(OrganizationId, BranchId);
        viewModel.ApplyPermissions(new HashSet<string>(permissions));
        return viewModel;
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
        public static readonly Guid DeviceId = DeterministicGuid("device");

        public List<string> Calls { get; } = [];

        public IReadOnlySet<string> ExistingStaffUserNames { get; init; } = new HashSet<string>();

        public string? ExistingZoneName { get; init; }

        public IReadOnlySet<string> ExistingSeatNames { get; init; } = new HashSet<string>();

        public string? FailOnCall { get; init; }

        public Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken)
        {
            Record("get-staff");
            IReadOnlyList<StaffUserDto> staffUsers = ExistingStaffUserNames
                .Select(userName => CreateStaffUser(userName, "Existing Staff", "existing_role"))
                .ToList();
            return Task.FromResult(staffUsers);
        }

        public Task<StaffUserDto> CreateStaffUserAsync(
            Guid branchId,
            CreateStaffUserRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-staff:{request.UserName}");
            return Task.FromResult(CreateStaffUser(request.UserName, request.DisplayName, request.RoleNames[0]));
        }

        public Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken)
        {
            Record("get-zones");
            if (ExistingZoneName is null)
            {
                return Task.FromResult<IReadOnlyList<ZoneDto>>([]);
            }

            IReadOnlyList<ZoneDto> zones = [CreateZone(ExistingZoneName, ExistingSeatNames)];
            return Task.FromResult(zones);
        }

        public Task<ZoneDto> CreateZoneAsync(
            Guid branchId,
            CreateZoneRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-zone:{request.Name}");
            return Task.FromResult(CreateZone(request.Name, new HashSet<string>()));
        }

        public Task<SeatDto> CreateSeatAsync(
            Guid branchId,
            CreateSeatRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-seat:{request.Name}");
            return Task.FromResult(CreateSeat(request.Name, request.ZoneId, request.SortOrder));
        }

        public Task<TariffDto> CreateTariffAsync(
            Guid branchId,
            CreateTariffRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-tariff:{request.Name}");
            return Task.FromResult(new TariffDto(
                DeterministicGuid($"tariff:{request.Name}"),
                OrganizationId,
                branchId,
                request.Name,
                IsActive: true,
                DateTimeOffset.Parse("2026-05-19T07:00:00Z")));
        }

        public Task<TariffVersionDto> CreateTariffVersionAsync(
            Guid branchId,
            Guid tariffId,
            CreateTariffVersionRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-tariff-version:{request.CurrencyCode}");
            return Task.FromResult(new TariffVersionDto(
                DeterministicGuid($"tariff-version:{request.CurrencyCode}"),
                tariffId,
                VersionNumber: 1,
                request.CurrencyCode,
                request.PricePerMinuteMinorUnits,
                request.MinimumBillableMinutes,
                request.RoundingIncrementMinutes,
                request.EffectiveFromUtc,
                RetiredAtUtc: null,
                DateTimeOffset.Parse("2026-05-19T07:01:00Z")));
        }

        public Task<PosProductCategoryDto> CreateProductCategoryAsync(
            Guid branchId,
            CreateProductCategoryRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-pos-category:{request.Name}");
            return Task.FromResult(new PosProductCategoryDto(
                DeterministicGuid($"category:{request.Name}"),
                OrganizationId,
                branchId,
                request.Name,
                IsActive: true,
                DateTimeOffset.Parse("2026-05-19T07:02:00Z")));
        }

        public Task<PosProductDto> CreateProductAsync(
            Guid branchId,
            CreateProductRequest request,
            CancellationToken cancellationToken)
        {
            Record($"create-pos-product:{request.Sku}");
            return Task.FromResult(new PosProductDto(
                DeterministicGuid($"product:{request.Sku}"),
                OrganizationId,
                branchId,
                request.CategoryId,
                request.Name,
                request.Sku,
                request.Price,
                request.TrackStock,
                request.AllowNegativeStock,
                IsActive: true,
                StockOnHand: 0,
                DateTimeOffset.Parse("2026-05-19T07:03:00Z")));
        }

        public Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(
            Guid branchId,
            Guid deviceId,
            AssignDeviceSeatRequest request,
            CancellationToken cancellationToken)
        {
            Record("assign-device-seat");
            return Task.FromResult(new DeviceSeatAssignmentDto(
                DeterministicGuid($"assignment:{deviceId}:{request.SeatId}"),
                OrganizationId,
                branchId,
                request.SeatId,
                deviceId,
                DateTimeOffset.Parse("2026-05-19T07:04:00Z"),
                DetachedAtUtc: null));
        }

        private void Record(string call)
        {
            Calls.Add(call);
            if (call == FailOnCall)
            {
                throw new HttpRequestException("Platform API returned 500 Internal Server Error: staff create failed");
            }
        }

        private static StaffUserDto CreateStaffUser(string userName, string displayName, string roleName)
        {
            return new StaffUserDto(
                DeterministicGuid($"staff:{userName}"),
                OrganizationId,
                userName,
                displayName,
                IsActive: true,
                [roleName],
                DateTimeOffset.Parse("2026-05-19T07:05:00Z"));
        }

        private static ZoneDto CreateZone(string name, IReadOnlySet<string> seatNames)
        {
            var zoneId = DeterministicGuid($"zone:{name}");
            return new ZoneDto(
                zoneId,
                OrganizationId,
                BranchId,
                name,
                SortOrder: 1,
                DateTimeOffset.Parse("2026-05-19T07:06:00Z"),
                seatNames.Select((seatName, index) => CreateSeat(seatName, zoneId, index + 1)).ToList());
        }

        private static SeatDto CreateSeat(string name, Guid zoneId, int sortOrder)
        {
            return new SeatDto(
                DeterministicGuid($"seat:{name}"),
                OrganizationId,
                BranchId,
                zoneId,
                name,
                sortOrder,
                DateTimeOffset.Parse("2026-05-19T07:07:00Z"));
        }

        private static Guid DeterministicGuid(string value)
        {
            Span<byte> bytes = stackalloc byte[16];
            MD5.HashData(Encoding.UTF8.GetBytes(value), bytes);
            return new Guid(bytes);
        }
    }
}
