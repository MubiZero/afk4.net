# Operator Pilot Setup UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a minimum `Pilot Setup` panel inside the WPF Operator App Settings workspace so pilot branch setup can be run through supported Platform API endpoints instead of direct SQL or the release-workstation script.

**Architecture:** Add a focused `AFK4.Operator.App.PilotSetup` module with an authenticated API client and one MVVM workspace view model. Wire the workspace into the existing Settings panel registry and render it in `MainWindow.xaml` using the same dense operational style as the current Settings tools.

**Tech Stack:** WPF + MVVM, .NET 10, shared DTOs from `AFK4.Shared.Contracts`, `HttpClient` + bearer token store, xUnit tests in `tests/AFK4.Operator.App.Tests`.

---

## File Structure

- Create `src/AFK4.Operator.App/PilotSetup/IOperatorPilotSetupApiClient.cs`
  - Interface for staff, layout, tariff, POS catalog, and device assignment setup calls.
- Create `src/AFK4.Operator.App/PilotSetup/HttpOperatorPilotSetupApiClient.cs`
  - Authenticated HTTP implementation using existing Operator App client conventions.
- Create `src/AFK4.Operator.App/PilotSetup/UnconfiguredOperatorPilotSetupApiClient.cs`
  - Fallback implementation for unconfigured design-time/default construction.
- Create `src/AFK4.Operator.App/PilotSetup/PilotSetupWorkspaceViewModel.cs`
  - Form state, permission gates, validation, execution order, result projection, and password redaction.
- Create `tests/AFK4.Operator.App.Tests/OperatorPilotSetupApiClientTests.cs`
  - HTTP route/body/token coverage.
- Create `tests/AFK4.Operator.App.Tests/PilotSetupWorkspaceViewModelTests.cs`
  - Defaults, context, permission gates, validation, execution order, reuse, error handling, and redaction.
- Modify `src/AFK4.Operator.App/Settings/SettingsWorkspaceViewModel.cs`
  - Add optional `PilotSetupWorkspaceViewModel`, `HasPilotSetup`, panel visibility, and context forwarding.
- Modify `src/AFK4.Operator.App/Shell/OperatorShellViewModel.cs`
  - Include pilot setup permissions as Settings workspace permissions and provide the default pilot setup client/view model.
- Modify `src/AFK4.Operator.App/MainWindow.xaml`
  - Render the new `pilot-setup` panel under Settings.
- Modify `docs/operations/pilot-branch-setup.md`
  - Document the Operator App panel as the preferred setup path and keep the script as fallback.
- Modify `docs/progress/2026-05-12-vertical-slice-progress.md`
  - Record the new implemented UI capability and local/remote verification after implementation is complete.

## Task 1: Pilot Setup API Client

**Files:**
- Create: `tests/AFK4.Operator.App.Tests/OperatorPilotSetupApiClientTests.cs`
- Create: `src/AFK4.Operator.App/PilotSetup/IOperatorPilotSetupApiClient.cs`
- Create: `src/AFK4.Operator.App/PilotSetup/HttpOperatorPilotSetupApiClient.cs`
- Create: `src/AFK4.Operator.App/PilotSetup/UnconfiguredOperatorPilotSetupApiClient.cs`

- [ ] **Step 1: Write failing API client tests**

Create `tests/AFK4.Operator.App.Tests/OperatorPilotSetupApiClientTests.cs` with route-focused tests:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.PilotSetup;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorPilotSetupApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    private static readonly Guid ZoneId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly Guid TariffId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ProductCategoryId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [Fact]
    public async Task StaffMethods_UseBranchStaffEndpointsWithBearerToken()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse<IReadOnlyList<StaffUserDto>>([CreateStaffUser("cashier.pilot@afk4.test")]),
            JsonResponse(CreateStaffUser("technician.pilot@afk4.test"))
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var staff = await client.GetStaffUsersAsync(BranchId, CancellationToken.None);
        var getPath = handler.LastPathAndQuery;
        var created = await client.CreateStaffUserAsync(
            BranchId,
            new CreateStaffUserRequest(
                OrganizationId,
                "technician.pilot@afk4.test",
                "Pilot Technician",
                "ChangeMe!2026",
                ["technician"]),
            CancellationToken.None);

        Assert.Single(staff);
        Assert.Equal("technician.pilot@afk4.test", created.UserName);
        Assert.Equal($"/api/branches/{BranchId:D}/staff", getPath);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal($"/api/branches/{BranchId:D}/staff", handler.LastPathAndQuery);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);

        var body = DeserializeRequest<CreateStaffUserRequest>(handler.LastRequestBody);
        Assert.Equal("technician", body.RoleNames.Single());
        Assert.Equal("ChangeMe!2026", body.Password);
    }

    [Fact]
    public async Task LayoutMethods_UseZoneAndSeatEndpoints()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse<IReadOnlyList<ZoneDto>>([CreateZone("Main Hall", [])]),
            JsonResponse(CreateZone("VIP", [])),
            JsonResponse(CreateSeat("PC-001"))
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var zones = await client.GetLayoutZonesAsync(BranchId, CancellationToken.None);
        var getPath = handler.LastPathAndQuery;
        var zone = await client.CreateZoneAsync(
            BranchId,
            new CreateZoneRequest(OrganizationId, "VIP", 20),
            CancellationToken.None);
        var zonePath = handler.LastPathAndQuery;
        var seat = await client.CreateSeatAsync(
            BranchId,
            new CreateSeatRequest(OrganizationId, zone.ZoneId, "PC-001", 1),
            CancellationToken.None);

        Assert.Equal("Main Hall", zones.Single().Name);
        Assert.Equal("VIP", zone.Name);
        Assert.Equal("PC-001", seat.Name);
        Assert.Equal($"/api/branches/{BranchId:D}/layout/zones", getPath);
        Assert.Equal($"/api/branches/{BranchId:D}/layout/zones", zonePath);
        Assert.Equal($"/api/branches/{BranchId:D}/layout/seats", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task TariffMethods_UseTariffAndVersionEndpoints()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(new TariffDto(TariffId, OrganizationId, BranchId, "Standard", true, DateTimeOffset.Parse("2026-05-19T00:00:00Z"))),
            JsonResponse(new TariffVersionDto(
                Guid.Parse("66666666-6666-4666-8666-666666666666"),
                TariffId,
                1,
                "TJS",
                100,
                1,
                1,
                DateTimeOffset.Parse("2026-05-19T00:00:00Z"),
                null,
                DateTimeOffset.Parse("2026-05-19T00:00:00Z")))
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var tariff = await client.CreateTariffAsync(
            BranchId,
            new CreateTariffRequest(OrganizationId, "Standard", "pilot-setup-tariff-standard"),
            CancellationToken.None);
        var tariffPath = handler.LastPathAndQuery;
        var version = await client.CreateTariffVersionAsync(
            BranchId,
            TariffId,
            new CreateTariffVersionRequest(
                OrganizationId,
                TariffId,
                "TJS",
                100,
                1,
                1,
                DateTimeOffset.Parse("2026-05-19T00:00:00Z"),
                "pilot-setup-tariff-standard-v1"),
            CancellationToken.None);

        Assert.Equal(TariffId, tariff.TariffId);
        Assert.Equal(1, version.VersionNumber);
        Assert.Equal($"/api/branches/{BranchId:D}/tariffs", tariffPath);
        Assert.Equal($"/api/branches/{BranchId:D}/tariffs/{TariffId:D}/versions", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task PosMethods_UseCatalogSetupEndpoints()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse(new PosProductCategoryDto(ProductCategoryId, OrganizationId, BranchId, "Drinks", true, DateTimeOffset.Parse("2026-05-19T00:00:00Z"))),
            JsonResponse(new PosProductDto(
                Guid.Parse("77777777-7777-4777-8777-777777777777"),
                OrganizationId,
                BranchId,
                ProductCategoryId,
                "Water 0.5",
                "WATER-05",
                new MoneyDto("TJS", 500),
                true,
                false,
                true,
                0,
                DateTimeOffset.Parse("2026-05-19T00:00:00Z")))
        ]);
        var handler = new RecordingHttpMessageHandler(_ => responses.Dequeue());
        var client = CreateClient(handler);

        var category = await client.CreateProductCategoryAsync(
            BranchId,
            new CreateProductCategoryRequest(OrganizationId, "Drinks", "pilot-setup-pos-category-drinks"),
            CancellationToken.None);
        var categoryPath = handler.LastPathAndQuery;
        var product = await client.CreateProductAsync(
            BranchId,
            new CreateProductRequest(
                OrganizationId,
                category.CategoryId,
                "Water 0.5",
                "WATER-05",
                new MoneyDto("TJS", 500),
                true,
                false,
                "pilot-setup-pos-product-water-05"),
            CancellationToken.None);

        Assert.Equal(ProductCategoryId, category.CategoryId);
        Assert.Equal("WATER-05", product.Sku);
        Assert.Equal($"/api/branches/{BranchId:D}/pos/categories", categoryPath);
        Assert.Equal($"/api/branches/{BranchId:D}/pos/products", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task AssignDeviceSeatAsync_PostsDeviceSeatAssignment()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(new DeviceSeatAssignmentDto(
            Guid.Parse("88888888-8888-4888-8888-888888888888"),
            OrganizationId,
            BranchId,
            SeatId,
            DeviceId,
            DateTimeOffset.Parse("2026-05-19T00:00:00Z"),
            null)));
        var client = CreateClient(handler);

        var assignment = await client.AssignDeviceSeatAsync(
            BranchId,
            DeviceId,
            new AssignDeviceSeatRequest(OrganizationId, SeatId),
            CancellationToken.None);

        Assert.Equal(DeviceId, assignment.DeviceId);
        Assert.Equal($"/api/branches/{BranchId:D}/devices/{DeviceId:D}/seat-assignment", handler.LastPathAndQuery);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
    }

    private static HttpOperatorPilotSetupApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        return new HttpOperatorPilotSetupApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
    }

    private static StaffUserDto CreateStaffUser(string userName)
    {
        return new StaffUserDto(
            StaffUserId,
            OrganizationId,
            userName,
            "Pilot User",
            true,
            ["cashier_operator"],
            DateTimeOffset.Parse("2026-05-19T00:00:00Z"));
    }

    private static ZoneDto CreateZone(string name, IReadOnlyList<SeatDto> seats)
    {
        return new ZoneDto(ZoneId, OrganizationId, BranchId, name, 10, DateTimeOffset.Parse("2026-05-19T00:00:00Z"), seats);
    }

    private static SeatDto CreateSeat(string name)
    {
        return new SeatDto(SeatId, OrganizationId, BranchId, ZoneId, name, 1, DateTimeOffset.Parse("2026-05-19T00:00:00Z"));
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonResponse<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPathAndQuery { get; private set; }
        public string? LastRequestBody { get; private set; }
        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }

    private sealed class StaticOperatorTokenStore : IOperatorTokenStore
    {
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
                StaffUserId,
                OrganizationId,
                "Branch Manager",
                "staff-access-token",
                DateTimeOffset.Parse("2026-05-19T01:00:00Z"),
                "refresh-token",
                DateTimeOffset.Parse("2026-05-20T01:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Run API client tests to verify red**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter OperatorPilotSetupApiClientTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: FAIL because `AFK4.Operator.App.PilotSetup` and `HttpOperatorPilotSetupApiClient` do not exist.

- [ ] **Step 3: Implement the API client**

Create `src/AFK4.Operator.App/PilotSetup/IOperatorPilotSetupApiClient.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Layout;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Operator.App.PilotSetup;

public interface IOperatorPilotSetupApiClient
{
    Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken);
    Task<StaffUserDto> CreateStaffUserAsync(Guid branchId, CreateStaffUserRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken);
    Task<ZoneDto> CreateZoneAsync(Guid branchId, CreateZoneRequest request, CancellationToken cancellationToken);
    Task<SeatDto> CreateSeatAsync(Guid branchId, CreateSeatRequest request, CancellationToken cancellationToken);
    Task<TariffDto> CreateTariffAsync(Guid branchId, CreateTariffRequest request, CancellationToken cancellationToken);
    Task<TariffVersionDto> CreateTariffVersionAsync(Guid branchId, Guid tariffId, CreateTariffVersionRequest request, CancellationToken cancellationToken);
    Task<PosProductCategoryDto> CreateProductCategoryAsync(Guid branchId, CreateProductCategoryRequest request, CancellationToken cancellationToken);
    Task<PosProductDto> CreateProductAsync(Guid branchId, CreateProductRequest request, CancellationToken cancellationToken);
    Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(Guid branchId, Guid deviceId, AssignDeviceSeatRequest request, CancellationToken cancellationToken);
}
```

Create `HttpOperatorPilotSetupApiClient.cs` using the same `CreateRequestAsync`, `SendAndReadAsync`, and `JsonOptions` pattern used by `HttpOperatorPosApiClient`. Implement routes exactly as the tests assert:

```csharp
public Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken)
    => GetAsync<IReadOnlyList<StaffUserDto>>($"/api/branches/{branchId:D}/staff", cancellationToken);

public Task<StaffUserDto> CreateStaffUserAsync(Guid branchId, CreateStaffUserRequest request, CancellationToken cancellationToken)
    => SendAsync<StaffUserDto, CreateStaffUserRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/staff", request, cancellationToken);

public Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken)
    => GetAsync<IReadOnlyList<ZoneDto>>($"/api/branches/{branchId:D}/layout/zones", cancellationToken);

public Task<ZoneDto> CreateZoneAsync(Guid branchId, CreateZoneRequest request, CancellationToken cancellationToken)
    => SendAsync<ZoneDto, CreateZoneRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/layout/zones", request, cancellationToken);

public Task<SeatDto> CreateSeatAsync(Guid branchId, CreateSeatRequest request, CancellationToken cancellationToken)
    => SendAsync<SeatDto, CreateSeatRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/layout/seats", request, cancellationToken);

public Task<TariffDto> CreateTariffAsync(Guid branchId, CreateTariffRequest request, CancellationToken cancellationToken)
    => SendAsync<TariffDto, CreateTariffRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/tariffs", request, cancellationToken);

public Task<TariffVersionDto> CreateTariffVersionAsync(Guid branchId, Guid tariffId, CreateTariffVersionRequest request, CancellationToken cancellationToken)
    => SendAsync<TariffVersionDto, CreateTariffVersionRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/tariffs/{tariffId:D}/versions", request, cancellationToken);

public Task<PosProductCategoryDto> CreateProductCategoryAsync(Guid branchId, CreateProductCategoryRequest request, CancellationToken cancellationToken)
    => SendAsync<PosProductCategoryDto, CreateProductCategoryRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/pos/categories", request, cancellationToken);

public Task<PosProductDto> CreateProductAsync(Guid branchId, CreateProductRequest request, CancellationToken cancellationToken)
    => SendAsync<PosProductDto, CreateProductRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/pos/products", request, cancellationToken);

public Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(Guid branchId, Guid deviceId, AssignDeviceSeatRequest request, CancellationToken cancellationToken)
    => SendAsync<DeviceSeatAssignmentDto, AssignDeviceSeatRequest>(HttpMethod.Post, $"/api/branches/{branchId:D}/devices/{deviceId:D}/seat-assignment", request, cancellationToken);
```

Create `UnconfiguredOperatorPilotSetupApiClient.cs` with every method throwing:

```csharp
private static InvalidOperationException CreateException()
{
    return new InvalidOperationException("Operator pilot setup API client is not configured.");
}
```

- [ ] **Step 4: Run API client tests to verify green**

Run the same command as Step 2.

Expected: PASS with 5 tests.

- [ ] **Step 5: Commit API client slice**

```powershell
git add -- src/AFK4.Operator.App/PilotSetup tests/AFK4.Operator.App.Tests/OperatorPilotSetupApiClientTests.cs
git commit -m "feat: add operator pilot setup api client"
```

## Task 2: Pilot Setup ViewModel State And Permissions

**Files:**
- Create: `tests/AFK4.Operator.App.Tests/PilotSetupWorkspaceViewModelTests.cs`
- Create: `src/AFK4.Operator.App/PilotSetup/PilotSetupWorkspaceViewModel.cs`

- [ ] **Step 1: Write failing ViewModel defaults and permission tests**

Create the first tests in `PilotSetupWorkspaceViewModelTests.cs`:

```csharp
using AFK4.Operator.App.PilotSetup;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Tests;

public sealed partial class PilotSetupWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public void Constructor_UsesPilotRunbookDefaults()
    {
        var viewModel = new PilotSetupWorkspaceViewModel(new RecordingPilotSetupApiClient());

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
        Assert.Contains(viewModel.StaffUsers, staff => staff.RoleName == "cashier_operator");
        Assert.Contains(viewModel.StaffUsers, staff => staff.RoleName == "technician");
        Assert.Contains(viewModel.StaffUsers, staff => staff.RoleName == "shift_supervisor");
    }

    [Fact]
    public void ApplyContext_UpdatesOrganizationAndBranchText()
    {
        var viewModel = new PilotSetupWorkspaceViewModel(new RecordingPilotSetupApiClient());

        viewModel.ApplyContext(OrganizationId, BranchId);

        Assert.Equal(OrganizationId.ToString("D"), viewModel.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), viewModel.BranchIdText);
    }

    [Fact]
    public void ApplyPermissions_EnablesOnlyMatchingSections()
    {
        var viewModel = new PilotSetupWorkspaceViewModel(new RecordingPilotSetupApiClient());

        viewModel.ApplyPermissions(new HashSet<string> { StaffPermissionNames.ManageLayout, StaffPermissionNames.AssignDeviceSeat });

        Assert.False(viewModel.CanSetupStaff);
        Assert.True(viewModel.CanSetupLayout);
        Assert.False(viewModel.CanSetupTariff);
        Assert.False(viewModel.CanSetupPos);
        Assert.True(viewModel.CanAssignDeviceSeat);
        Assert.True(viewModel.HasAnySetupPermission);
    }
}
```

- [ ] **Step 2: Run ViewModel tests to verify red**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter PilotSetupWorkspaceViewModelTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: FAIL because `PilotSetupWorkspaceViewModel` and `RecordingPilotSetupApiClient` do not exist.

- [ ] **Step 3: Implement ViewModel state, defaults, and permission gates**

Create `PilotSetupWorkspaceViewModel.cs` with these public support types:

```csharp
public sealed class PilotSetupStaffUserViewModel : INotifyPropertyChanged
{
    public PilotSetupStaffUserViewModel(string userName, string displayName, string password, string roleName)
    {
        this.userName = userName;
        this.displayName = displayName;
        this.password = password;
        this.roleName = roleName;
    }

    public string UserName { get; set; }
    public string DisplayName { get; set; }
    public string Password { get; set; }
    public string RoleName { get; set; }
}

public sealed record PilotSetupStepResultViewModel(
    string Key,
    string Label,
    string State,
    string Detail,
    string? EntityId);
```

Implement `PilotSetupWorkspaceViewModel` with:

```csharp
public ObservableCollection<PilotSetupStaffUserViewModel> StaffUsers { get; } =
[
    new("cashier.pilot@afk4.test", "Pilot Cashier", "ChangeMe!2026", "cashier_operator"),
    new("technician.pilot@afk4.test", "Pilot Technician", "ChangeMe!2026", "technician"),
    new("supervisor.pilot@afk4.test", "Pilot Supervisor", "ChangeMe!2026", "shift_supervisor")
];

public ObservableCollection<PilotSetupStepResultViewModel> Results { get; } = [];

public string ZoneName { get; set; } = "Main Hall";
public string SeatPrefix { get; set; } = "PC-";
public int SeatCount { get; set; } = 10;
public int SeatSortOrderStart { get; set; } = 1;
public string TargetAssignmentSeatName { get; set; } = "PC-001";
public string TariffName { get; set; } = "Standard";
public string CurrencyCode { get; set; } = "TJS";
public long PricePerMinuteMinorUnits { get; set; } = 100;
public int MinimumBillableMinutes { get; set; } = 1;
public int RoundingIncrementMinutes { get; set; } = 1;
public DateTimeOffset EffectiveFromUtc { get; set; } = DateTimeOffset.UtcNow.Date;
public string ProductCategoryName { get; set; } = "Drinks";
public string ProductName { get; set; } = "Water 0.5";
public string ProductSku { get; set; } = "WATER-05";
public long ProductPriceMinorUnits { get; set; } = 500;
public bool ProductTrackStock { get; set; } = true;
public bool ProductAllowNegativeStock { get; set; }
public string DeviceIdText { get; set; } = string.Empty;
```

Add `ApplyContext(Guid organizationId, Guid branchId)` and `ApplyPermissions(IReadOnlySet<string> permissions)`. Permission booleans must map exactly:

```csharp
CanSetupStaff = permissions.Contains(StaffPermissionNames.ManageBranchStaff);
CanSetupLayout = permissions.Contains(StaffPermissionNames.ManageLayout);
CanSetupTariff = permissions.Contains(StaffPermissionNames.ManageTariffs);
CanSetupPos = permissions.Contains(StaffPermissionNames.ManagePosCatalog);
CanAssignDeviceSeat = permissions.Contains(StaffPermissionNames.AssignDeviceSeat);
HasAnySetupPermission = CanSetupStaff || CanSetupLayout || CanSetupTariff || CanSetupPos || CanAssignDeviceSeat;
```

Add a private `SetField<T>` helper and raise `PropertyChanged` for all permission booleans in `ApplyPermissions`.

- [ ] **Step 4: Add RecordingPilotSetupApiClient test helper**

At the bottom of `PilotSetupWorkspaceViewModelTests.cs`, add a nested helper implementing `IOperatorPilotSetupApiClient`. For Task 2, every method can throw `NotSupportedException`; Task 3 will extend it with recordings and responses:

```csharp
private sealed class RecordingPilotSetupApiClient : IOperatorPilotSetupApiClient
{
    public Task<IReadOnlyList<StaffUserDto>> GetStaffUsersAsync(Guid branchId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<StaffUserDto> CreateStaffUserAsync(Guid branchId, CreateStaffUserRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<ZoneDto>> GetLayoutZonesAsync(Guid branchId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ZoneDto> CreateZoneAsync(Guid branchId, CreateZoneRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<SeatDto> CreateSeatAsync(Guid branchId, CreateSeatRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<TariffDto> CreateTariffAsync(Guid branchId, CreateTariffRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<TariffVersionDto> CreateTariffVersionAsync(Guid branchId, Guid tariffId, CreateTariffVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<PosProductCategoryDto> CreateProductCategoryAsync(Guid branchId, CreateProductCategoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<PosProductDto> CreateProductAsync(Guid branchId, CreateProductRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<DeviceSeatAssignmentDto> AssignDeviceSeatAsync(Guid branchId, Guid deviceId, AssignDeviceSeatRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
```

- [ ] **Step 5: Run ViewModel tests to verify green**

Run the same command as Step 2.

Expected: PASS with 3 tests.

- [ ] **Step 6: Commit ViewModel state slice**

```powershell
git add -- src/AFK4.Operator.App/PilotSetup/PilotSetupWorkspaceViewModel.cs tests/AFK4.Operator.App.Tests/PilotSetupWorkspaceViewModelTests.cs
git commit -m "feat: add pilot setup workspace state"
```

## Task 3: Pilot Setup Execution, Reuse, Validation, And Redaction

**Files:**
- Modify: `tests/AFK4.Operator.App.Tests/PilotSetupWorkspaceViewModelTests.cs`
- Modify: `src/AFK4.Operator.App/PilotSetup/PilotSetupWorkspaceViewModel.cs`

- [ ] **Step 1: Add failing execution and validation tests**

Append these tests to `PilotSetupWorkspaceViewModelTests.cs`:

```csharp
[Fact]
public async Task ApplyAsync_WithAllPermissions_CallsSetupEndpointsInOrder()
{
    var apiClient = new RecordingPilotSetupApiClient();
    var viewModel = CreateReadyViewModel(apiClient, StaffPermissionNames.ManageBranchStaff, StaffPermissionNames.ManageLayout, StaffPermissionNames.ManageTariffs, StaffPermissionNames.ManagePosCatalog, StaffPermissionNames.AssignDeviceSeat);
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
        ],
        apiClient.Calls);
    Assert.Equal("Pilot setup applied.", viewModel.StatusMessage);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Contains(viewModel.Results, result => result.Key == "device-assignment" && result.State == "created");
}

[Fact]
public async Task ApplyAsync_ReusesExistingStaffZoneAndSeats()
{
    var apiClient = new RecordingPilotSetupApiClient
    {
        ExistingStaffUserNames = ["cashier.pilot@afk4.test"],
        ExistingZoneName = "Main Hall",
        ExistingSeatNames = ["PC-001", "PC-002"]
    };
    var viewModel = CreateReadyViewModel(apiClient, StaffPermissionNames.ManageBranchStaff, StaffPermissionNames.ManageLayout);
    viewModel.SeatCount = 3;

    await viewModel.ApplyAsync(CancellationToken.None);

    Assert.DoesNotContain("create-staff:cashier.pilot@afk4.test", apiClient.Calls);
    Assert.DoesNotContain("create-zone:Main Hall", apiClient.Calls);
    Assert.DoesNotContain("create-seat:PC-001", apiClient.Calls);
    Assert.DoesNotContain("create-seat:PC-002", apiClient.Calls);
    Assert.Contains("create-seat:PC-003", apiClient.Calls);
    Assert.Contains(viewModel.Results, result => result.Label.Contains("cashier.pilot@afk4.test", StringComparison.Ordinal) && result.State == "reused");
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
    Assert.DoesNotContain(viewModel.Results, result => result.Detail.Contains("ChangeMe!2026", StringComparison.Ordinal));
    Assert.DoesNotContain("create-staff:supervisor.pilot@afk4.test", apiClient.Calls);
}
```

Add this helper factory:

```csharp
private static PilotSetupWorkspaceViewModel CreateReadyViewModel(RecordingPilotSetupApiClient apiClient, params string[] permissions)
{
    var viewModel = new PilotSetupWorkspaceViewModel(apiClient);
    viewModel.ApplyContext(OrganizationId, BranchId);
    viewModel.ApplyPermissions(permissions.ToHashSet(StringComparer.OrdinalIgnoreCase));
    return viewModel;
}
```

- [ ] **Step 2: Run ViewModel tests to verify red**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter PilotSetupWorkspaceViewModelTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: FAIL because `ApplyAsync`, `ApplyCommand`, and execution logic are missing.

- [ ] **Step 3: Implement execution command and validation**

In `PilotSetupWorkspaceViewModel`, add:

```csharp
public AsyncRelayCommand ApplyCommand { get; }
public bool IsBusy { get; private set; }
public string StatusMessage { get; private set; } = string.Empty;
public string? ErrorMessage { get; private set; }

public Task ApplyAsync(CancellationToken cancellationToken)
{
    ClearMessagesAndResults();
    if (!TryParseGuid(OrganizationIdText, "OrganizationId", out var organizationId) ||
        !TryParseGuid(BranchIdText, "BranchId", out var branchId) ||
        !ValidateEnabledSections(out var validationError))
    {
        ErrorMessage = validationError;
        return Task.CompletedTask;
    }

    return RunSetupAsync(organizationId, branchId, cancellationToken);
}
```

Validation rules:

- `OrganizationIdText` and `BranchIdText` must be GUIDs before any API call.
- staff setup requires every enabled staff row to have non-empty username, display name, password, and role.
- layout setup requires non-empty zone name, non-empty seat prefix, `SeatCount` between 1 and 200, and `SeatSortOrderStart` positive.
- tariff setup requires non-empty tariff name/currency, positive price, positive minimum minutes, positive rounding increment.
- POS setup requires non-empty category, product name, SKU, positive product price.
- device assignment requires valid `DeviceIdText`; if `TargetAssignmentSeatName` is blank, use the first configured seat name.

- [ ] **Step 4: Implement step execution**

Implement the sequence in separate private methods:

```csharp
private async Task RunSetupAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken)
{
    IsBusy = true;
    NotifyCommandStateChanged();
    try
    {
        if (CanSetupStaff)
        {
            await SetupStaffAsync(branchId, organizationId, cancellationToken);
        }

        Dictionary<string, SeatDto> seatsByName = [];
        if (CanSetupLayout)
        {
            seatsByName = await SetupLayoutAsync(branchId, organizationId, cancellationToken);
        }

        if (CanSetupTariff)
        {
            await SetupTariffAsync(branchId, organizationId, cancellationToken);
        }

        if (CanSetupPos)
        {
            await SetupPosAsync(branchId, organizationId, cancellationToken);
        }

        if (CanAssignDeviceSeat && !string.IsNullOrWhiteSpace(DeviceIdText))
        {
            await AssignDeviceSeatAsync(branchId, organizationId, seatsByName, cancellationToken);
        }

        StatusMessage = "Pilot setup applied.";
    }
    catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException)
    {
        ErrorMessage = RedactSensitive(exception.Message);
    }
    finally
    {
        IsBusy = false;
        NotifyCommandStateChanged();
    }
}
```

Stable idempotency key helper:

```csharp
private static string CreateIdempotencyKey(Guid branchId, params string[] parts)
{
    var normalizedParts = parts
        .Select(part => new string(part.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()))
        .Select(part => part.Trim('-'))
        .Where(part => part.Length > 0);
    return $"pilot-setup-{branchId:N}-{string.Join("-", normalizedParts)}";
}
```

Redaction helper:

```csharp
private string RedactSensitive(string value)
{
    var redacted = value;
    foreach (var staff in StaffUsers)
    {
        if (!string.IsNullOrEmpty(staff.Password))
        {
            redacted = redacted.Replace(staff.Password, "[redacted]", StringComparison.Ordinal);
        }
    }

    return redacted;
}
```

Result helper:

```csharp
private void AddResult(string key, string label, string state, string detail, Guid? entityId = null)
{
    Results.Add(new PilotSetupStepResultViewModel(key, label, state, RedactSensitive(detail), entityId?.ToString("D")));
}
```

- [ ] **Step 5: Extend RecordingPilotSetupApiClient**

Replace the Task 2 throwing helper with a full recording helper that:

- records `Calls` strings matching the tests;
- returns existing staff for `ExistingStaffUserNames`;
- returns one existing zone with existing seats when `ExistingZoneName` is set;
- throws `HttpRequestException("Platform API returned 500 Internal Server Error: staff create failed")` when `FailOnCall` matches the call string;
- returns deterministic DTO IDs for staff, zone, seats, tariff, tariff version, category, product, and assignment.

Use deterministic ID creation:

```csharp
private static Guid DeterministicGuid(string value)
{
    Span<byte> bytes = stackalloc byte[16];
    System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value), bytes);
    return new Guid(bytes);
}
```

- [ ] **Step 6: Run ViewModel tests to verify green**

Run the same command as Step 2.

Expected: PASS with 7 tests.

- [ ] **Step 7: Commit execution slice**

```powershell
git add -- src/AFK4.Operator.App/PilotSetup/PilotSetupWorkspaceViewModel.cs tests/AFK4.Operator.App.Tests/PilotSetupWorkspaceViewModelTests.cs
git commit -m "feat: orchestrate operator pilot setup"
```

## Task 4: Settings And Shell Integration

**Files:**
- Modify: `src/AFK4.Operator.App/Settings/SettingsWorkspaceViewModel.cs`
- Modify: `src/AFK4.Operator.App/Shell/OperatorShellViewModel.cs`
- Modify: `tests/AFK4.Operator.App.Tests/SettingsWorkspaceViewModelTests.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorShellViewModelTests.cs`

- [ ] **Step 1: Write failing Settings integration tests**

Add to `SettingsWorkspaceViewModelTests.cs`:

```csharp
[Fact]
public void SettingsWorkspace_WithPilotSetupPermission_ExposesPilotSetupPanel()
{
    var pilotSetup = new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient());
    var viewModel = new SettingsWorkspaceViewModel(
        new HashSet<string> { StaffPermissionNames.ManageBranchStaff },
        technicianTools: null,
        updateStatus: null,
        auditSearch: null,
        diagnostics: null,
        pilotSetup: pilotSetup);

    Assert.True(viewModel.HasPilotSetup);
    Assert.Same(pilotSetup, viewModel.PilotSetup);
    Assert.Contains(viewModel.Panels, panel => panel.Key == "pilot-setup");
}

[Fact]
public void ApplyContext_UpdatesPilotSetupContextAndPermissions()
{
    var pilotSetup = new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient());
    var viewModel = new SettingsWorkspaceViewModel(
        new HashSet<string> { StaffPermissionNames.ManageLayout },
        technicianTools: null,
        updateStatus: null,
        auditSearch: null,
        diagnostics: null,
        pilotSetup: pilotSetup);

    viewModel.ApplyContext(OrganizationId, BranchId, new HashSet<string> { StaffPermissionNames.ManageLayout });

    Assert.Equal(OrganizationId.ToString("D"), pilotSetup.OrganizationIdText);
    Assert.Equal(BranchId.ToString("D"), pilotSetup.BranchIdText);
    Assert.True(pilotSetup.CanSetupLayout);
}
```

Add to `OperatorShellViewModelTests.cs`:

```csharp
[Fact]
public void ApplySignedInContext_WithPilotSetupPermission_ShowsSettings()
{
    var shell = new OperatorShellViewModel();

    shell.ApplySignedInContext(CreateContext(StaffPermissionNames.ManageBranchStaff));

    Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Settings);
    Assert.Contains(shell.Settings.Panels, panel => panel.Key == "pilot-setup");
}
```

- [ ] **Step 2: Run integration tests to verify red**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: FAIL because `SettingsWorkspaceViewModel` has no `PilotSetup` constructor parameter and Settings workspace does not include setup permissions.

- [ ] **Step 3: Wire SettingsWorkspaceViewModel**

Modify constructors to carry optional `PilotSetupWorkspaceViewModel? pilotSetup`. Preserve existing constructor overloads by forwarding `new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient())` where the current defaults create other unconfigured panels.

Add:

```csharp
private readonly PilotSetupWorkspaceViewModel? pilotSetup;
public PilotSetupWorkspaceViewModel? PilotSetup => pilotSetup;
public bool HasPilotSetup => pilotSetup is not null && Panels.Any(panel => panel.Key == "pilot-setup");
```

Update `ApplyContext`:

```csharp
pilotSetup?.ApplyContext(organizationId, branchId);
```

Update `ApplyContext(Guid organizationId, Guid branchId, IReadOnlySet<string> permissions)`:

```csharp
RebuildPanels(permissions);
pilotSetup?.ApplyPermissions(permissions);
ApplyContext(organizationId, branchId);
```

Update `ApplyPermissions`:

```csharp
pilotSetup?.ApplyPermissions(permissions);
RebuildPanels(permissions);
```

Add panel in `RebuildPanels`:

```csharp
if (HasAny(
    permissions,
    StaffPermissionNames.ManageBranchStaff,
    StaffPermissionNames.ManageLayout,
    StaffPermissionNames.ManageTariffs,
    StaffPermissionNames.ManagePosCatalog,
    StaffPermissionNames.AssignDeviceSeat))
{
    AddPanel("pilot-setup", "Pilot Setup", "Branch configuration");
}
```

Raise `OnPropertyChanged(nameof(HasPilotSetup));`.

- [ ] **Step 4: Wire OperatorShellViewModel**

Add `using AFK4.Operator.App.PilotSetup;`.

Include setup permissions under `OperatorWorkspaceKind.Settings`:

```csharp
StaffPermissionNames.ManageBranchStaff,
StaffPermissionNames.ManageLayout,
StaffPermissionNames.AssignDeviceSeat,
```

`ManageTariffs` and `ManagePosCatalog` already exist in the Settings permission list; keep them.

Update `CreateDefaultSettingsWorkspace()` to pass a `PilotSetupWorkspaceViewModel`:

```csharp
new PilotSetupWorkspaceViewModel(new UnconfiguredOperatorPilotSetupApiClient())
```

- [ ] **Step 5: Run integration tests to verify green**

Run the same command as Step 2.

Expected: PASS for Settings and Shell tests.

- [ ] **Step 6: Commit integration slice**

```powershell
git add -- src/AFK4.Operator.App/Settings/SettingsWorkspaceViewModel.cs src/AFK4.Operator.App/Shell/OperatorShellViewModel.cs tests/AFK4.Operator.App.Tests/SettingsWorkspaceViewModelTests.cs tests/AFK4.Operator.App.Tests/OperatorShellViewModelTests.cs
git commit -m "feat: expose pilot setup in operator settings"
```

## Task 5: WPF Settings Panel Rendering

**Files:**
- Modify: `src/AFK4.Operator.App/MainWindow.xaml`

- [ ] **Step 1: Add the Pilot Setup panel XAML**

Inside the Settings workspace `StackPanel` after the connection fields and before technician tools, add:

```xml
<StackPanel DataContext="{Binding PilotSetup}"
            Margin="0,16,0,0">
    <StackPanel.Style>
        <Style TargetType="StackPanel">
            <Setter Property="Visibility" Value="Collapsed" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding DataContext.SelectedPanel.Key, RelativeSource={RelativeSource AncestorType=Grid}}"
                             Value="pilot-setup">
                    <Setter Property="Visibility" Value="Visible" />
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </StackPanel.Style>

    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <StackPanel>
            <TextBlock Text="Pilot Setup"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="#111827" />
            <TextBlock Text="{Binding StatusMessage}"
                       Margin="0,4,0,0"
                       Foreground="#047857"
                       TextWrapping="Wrap" />
            <TextBlock Text="{Binding ErrorMessage}"
                       Margin="0,4,0,0"
                       Foreground="#B91C1C"
                       TextWrapping="Wrap" />
        </StackPanel>
        <Button Grid.Column="1"
                Content="Apply Pilot Setup"
                Command="{Binding ApplyCommand}"
                MinWidth="130"
                Height="32" />
    </Grid>

    <Separator Margin="0,14,0,14" />

    <TextBlock Text="Staff"
               FontWeight="SemiBold"
               Foreground="#111827" />
    <DataGrid ItemsSource="{Binding StaffUsers}"
              AutoGenerateColumns="False"
              CanUserAddRows="False"
              MinHeight="112"
              Margin="0,8,0,0">
        <DataGrid.Columns>
            <DataGridTextColumn Header="User" Binding="{Binding UserName}" Width="180" />
            <DataGridTextColumn Header="Display" Binding="{Binding DisplayName}" Width="160" />
            <DataGridTextColumn Header="Password" Binding="{Binding Password}" Width="130" />
            <DataGridTextColumn Header="Role" Binding="{Binding RoleName}" Width="140" />
        </DataGrid.Columns>
    </DataGrid>

    <Separator Margin="0,14,0,14" />

    <TextBlock Text="Layout"
               FontWeight="SemiBold"
               Foreground="#111827" />
    <WrapPanel Margin="0,8,0,0">
        <StackPanel Width="160" Margin="0,0,8,8">
            <TextBlock Text="Zone" Foreground="#4B5563" />
            <TextBox Text="{Binding ZoneName, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="90" Margin="0,0,8,8">
            <TextBlock Text="Seat prefix" Foreground="#4B5563" />
            <TextBox Text="{Binding SeatPrefix, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="80" Margin="0,0,8,8">
            <TextBlock Text="Seats" Foreground="#4B5563" />
            <TextBox Text="{Binding SeatCount, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="100" Margin="0,0,8,8">
            <TextBlock Text="Start order" Foreground="#4B5563" />
            <TextBox Text="{Binding SeatSortOrderStart, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
    </WrapPanel>

    <TextBlock Text="Tariff"
               FontWeight="SemiBold"
               Foreground="#111827"
               Margin="0,8,0,0" />
    <WrapPanel Margin="0,8,0,0">
        <StackPanel Width="150" Margin="0,0,8,8">
            <TextBlock Text="Name" Foreground="#4B5563" />
            <TextBox Text="{Binding TariffName, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="70" Margin="0,0,8,8">
            <TextBlock Text="Currency" Foreground="#4B5563" />
            <TextBox Text="{Binding CurrencyCode, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="90" Margin="0,0,8,8">
            <TextBlock Text="Minor/min" Foreground="#4B5563" />
            <TextBox Text="{Binding PricePerMinuteMinorUnits, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="90" Margin="0,0,8,8">
            <TextBlock Text="Min mins" Foreground="#4B5563" />
            <TextBox Text="{Binding MinimumBillableMinutes, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="90" Margin="0,0,8,8">
            <TextBlock Text="Round mins" Foreground="#4B5563" />
            <TextBox Text="{Binding RoundingIncrementMinutes, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
    </WrapPanel>

    <TextBlock Text="POS"
               FontWeight="SemiBold"
               Foreground="#111827"
               Margin="0,8,0,0" />
    <WrapPanel Margin="0,8,0,0">
        <StackPanel Width="140" Margin="0,0,8,8">
            <TextBlock Text="Category" Foreground="#4B5563" />
            <TextBox Text="{Binding ProductCategoryName, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="150" Margin="0,0,8,8">
            <TextBlock Text="Product" Foreground="#4B5563" />
            <TextBox Text="{Binding ProductName, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="100" Margin="0,0,8,8">
            <TextBlock Text="SKU" Foreground="#4B5563" />
            <TextBox Text="{Binding ProductSku, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="90" Margin="0,0,8,8">
            <TextBlock Text="Price" Foreground="#4B5563" />
            <TextBox Text="{Binding ProductPriceMinorUnits, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <CheckBox Content="Track stock"
                  IsChecked="{Binding ProductTrackStock}"
                  VerticalAlignment="Bottom"
                  Margin="0,0,10,10" />
        <CheckBox Content="Negative stock"
                  IsChecked="{Binding ProductAllowNegativeStock}"
                  VerticalAlignment="Bottom"
                  Margin="0,0,10,10" />
    </WrapPanel>

    <TextBlock Text="Device Assignment"
               FontWeight="SemiBold"
               Foreground="#111827"
               Margin="0,8,0,0" />
    <WrapPanel Margin="0,8,0,0">
        <StackPanel Width="280" Margin="0,0,8,8">
            <TextBlock Text="Device ID" Foreground="#4B5563" />
            <TextBox Text="{Binding DeviceIdText, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
        <StackPanel Width="120" Margin="0,0,8,8">
            <TextBlock Text="Seat" Foreground="#4B5563" />
            <TextBox Text="{Binding TargetAssignmentSeatName, UpdateSourceTrigger=PropertyChanged}" Margin="0,4,0,0" />
        </StackPanel>
    </WrapPanel>

    <TextBlock Text="Results"
               FontWeight="SemiBold"
               Foreground="#111827"
               Margin="0,12,0,0" />
    <DataGrid ItemsSource="{Binding Results}"
              AutoGenerateColumns="False"
              CanUserAddRows="False"
              HeadersVisibility="Column"
              MinHeight="180"
              Margin="0,8,0,0">
        <DataGrid.Columns>
            <DataGridTextColumn Header="Step" Binding="{Binding Label}" Width="180" />
            <DataGridTextColumn Header="State" Binding="{Binding State}" Width="90" />
            <DataGridTextColumn Header="Detail" Binding="{Binding Detail}" Width="*" />
            <DataGridTextColumn Header="ID" Binding="{Binding EntityId}" Width="210" />
        </DataGrid.Columns>
    </DataGrid>
</StackPanel>
```

- [ ] **Step 2: Build Operator App to verify XAML compile**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build src\AFK4.Operator.App\AFK4.Operator.App.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: build succeeds with 0 errors.

- [ ] **Step 3: Commit XAML slice**

```powershell
git add -- src/AFK4.Operator.App/MainWindow.xaml
git commit -m "feat: render pilot setup settings panel"
```

## Task 6: Documentation And Progress

**Files:**
- Modify: `docs/operations/pilot-branch-setup.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Update pilot setup runbook**

In `docs/operations/pilot-branch-setup.md`, add a section before `## Configure Branch`:

```markdown
## Preferred Path: Operator App

Use the Operator App `Settings` -> `Pilot Setup` panel when a signed-in owner or
branch manager has the required setup permissions. The panel creates or reuses
branch staff users, one zone, seats, one tariff/version, one POS category and
product, and can optionally assign an already enrolled device to a configured
seat.

The PowerShell script below remains the release-workstation fallback for
headless setup or recovery when the Operator App is not available.
```

- [ ] **Step 2: Update progress snapshot**

In `docs/progress/2026-05-12-vertical-slice-progress.md`, update:

- `Implemented Capabilities` -> `Operator App`: add that Settings now includes a minimum pilot setup panel for staff/layout/tariff/POS/device assignment.
- `Known gaps` or `Recommended Next Work`: change the operator-facing pilot setup UI item from "next branch" to the next remaining usability gap after this implementation, unless implementation verification exposes a new gap.
- `Latest Verification`: add the exact focused tests and build commands from Task 7 once they pass.

- [ ] **Step 3: Commit docs**

```powershell
git add -- docs/operations/pilot-branch-setup.md docs/progress/2026-05-12-vertical-slice-progress.md
git commit -m "docs: record operator pilot setup ui"
```

## Task 7: Final Verification And PR

**Files:**
- All changed files from Tasks 1-6

- [ ] **Step 1: Run focused Operator App tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorPilotSetupApiClientTests|PilotSetupWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: all selected tests pass with 0 failures.

- [ ] **Step 2: Run full solution build**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Run whitespace check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors. CRLF warnings are acceptable if there are no reported whitespace errors.

- [ ] **Step 4: Inspect final diff**

Run:

```powershell
git status --short --branch
git diff --stat main...HEAD
```

Expected: only pilot setup UI, tests, design/plan, and documentation files are changed.

- [ ] **Step 5: Push and create PR**

Create the PR body and run:

```powershell
$prBodyPath = Join-Path $env:TEMP "operator-pilot-setup-ui-pr.md"
@'
## Summary
- Add an Operator App Settings Pilot Setup panel for staff, layout, tariff, POS, and optional device-seat setup.
- Add authenticated Operator App pilot setup API client coverage.
- Document the Operator App setup path and record verification evidence.

## Validation
- `dotnet test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorPilotSetupApiClientTests|PilotSetupWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal`
- `dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal`
- `git diff --check`
'@ | Set-Content -LiteralPath $prBodyPath -Encoding UTF8
git push -u origin codex/operator-pilot-setup-ui
gh pr create --title "[codex] add operator pilot setup ui" --base main --head codex/operator-pilot-setup-ui --body-file $prBodyPath
```

- [ ] **Step 6: Wait for remote gate and merge**

Run:

```powershell
$prNumber = gh pr view codex/operator-pilot-setup-ui --json number --jq .number
gh pr checks $prNumber --watch --interval 10
```

Expected: `PR Verification Result` passes for the current PR head commit.

Only after the gate is green:

```powershell
gh pr merge $prNumber --merge --delete-branch
```

- [ ] **Step 7: Record merge evidence if merged**

After merge, update `docs/progress/2026-05-12-vertical-slice-progress.md` on `main` with PR number, merge commit, head commit, and workflow run id if that information is not already recorded in the PR branch docs.

Commit and push the progress-only follow-up on `main`:

```powershell
git add -- docs/progress/2026-05-12-vertical-slice-progress.md
git commit -m "docs: record operator pilot setup ui merge"
git push origin main
```
