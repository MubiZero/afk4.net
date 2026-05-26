# AFK4 Phase 7 Operator App Production UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Operator App from a technician/floor-map shell into dense production operator software for day-to-day club work.

**Architecture:** Keep the backend as the business authority and the Operator App as a WPF + MVVM client with typed API clients, a realtime floor-map state store, explicit pending/failed states, and role-aware navigation. Add only small backend read endpoints required by production UX, while reusing the Phase 1-6 command endpoints for sessions, billing, POS, shifts, devices, and receipts.

**Tech Stack:** .NET 10, WPF, MVVM, typed `HttpClient`, SignalR client, ASP.NET Core Minimal APIs, EF Core/Npgsql, xUnit.

---

## Scope

Phase 7 implements:

- real/realtime floor map state loaded from `GET /api/branches/{branchId}/floor-map`;
- selected seat context panel with session/device/player actions;
- POS catalog, cart, manual payment, receipt, refund, and void workflow;
- player search, player creation, wallet/debt/package summary actions;
- shift open/current/cash movement/close workflow;
- settings surfaces for Operator App configuration, POS catalog/stock setup, tariffs/packages, and technician device tools;
- role-aware navigation based on staff permissions returned by sign-in/refresh;
- hotkeys for common operator workflows.

Phase 7 does not implement:

- Agent lock/unlock enforcement beyond commands already exposed by the backend;
- Player Shell production UI;
- centralized updates, installers, rollout, or rollback UX;
- web admin, local club server, microservices, or non-Windows client targets;
- fiscal printer, tax authority, card acquirer, or external gateway integrations.

## Current Baseline

Start from `main` commit `fc762ea fix: reconcile debt payments in shift close`.

Current Operator App baseline:

- static `MainWindowViewModel` seat list;
- `DeviceStatusStore` applies SignalR device status updates by machine name;
- token persistence via `ProtectedDataOperatorTokenStore`;
- typed device API client for technician enrollment, command, detail, credential workflows;
- technician panel embedded directly in `MainWindow.xaml`.

## File Structure

Create or modify these files during Phase 7:

```text
D:\afk4.net\
  src\AFK4.Shared.Contracts\
    Identity\StaffPermissionNames.cs
    Operator\PlayerSearchResultDto.cs
    Operator\TariffOptionDto.cs
    Operator\PackageOptionDto.cs
  src\AFK4.Platform.Api\
    Program.cs
    Billing\IOperatorReferenceDataService.cs
    Billing\EfOperatorReferenceDataService.cs
    Identity\PermissionCatalog.cs
  src\AFK4.Operator.App\
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Auth\IOperatorAuthApiClient.cs
    Auth\HttpOperatorAuthApiClient.cs
    Auth\SignInViewModel.cs
    Configuration\OperatorAppOptions.cs
    FloorMap\FloorMapWorkspaceViewModel.cs
    FloorMap\FloorMapSeatViewModel.cs
    FloorMap\IOperatorFloorMapApiClient.cs
    FloorMap\HttpOperatorFloorMapApiClient.cs
    FloorMap\SeatContextPanelViewModel.cs
    Hotkeys\OperatorHotkeyService.cs
    Mvvm\IdempotencyKeyFactory.cs
    Players\IOperatorPlayerApiClient.cs
    Players\HttpOperatorPlayerApiClient.cs
    Players\PlayerSearchViewModel.cs
    Pos\IOperatorPosApiClient.cs
    Pos\HttpOperatorPosApiClient.cs
    Pos\PosCartLineViewModel.cs
    Pos\PosWorkspaceViewModel.cs
    Sessions\IOperatorSessionApiClient.cs
    Sessions\HttpOperatorSessionApiClient.cs
    Settings\SettingsWorkspaceViewModel.cs
    Shell\OperatorNavigationItemViewModel.cs
    Shell\OperatorShellViewModel.cs
    Shell\OperatorUserContext.cs
    Shell\OperatorWorkspaceKind.cs
    Shifts\IOperatorShiftApiClient.cs
    Shifts\HttpOperatorShiftApiClient.cs
    Shifts\ShiftWorkspaceViewModel.cs
  tests\
    AFK4.Shared.Contracts.Tests\OperatorReferenceContractSerializationTests.cs
    AFK4.Platform.Api.Tests\OperatorReferenceDataEndpointTests.cs
    AFK4.Operator.App.Tests\OperatorAuthApiClientTests.cs
    AFK4.Operator.App.Tests\OperatorShellViewModelTests.cs
    AFK4.Operator.App.Tests\FloorMapWorkspaceViewModelTests.cs
    AFK4.Operator.App.Tests\SeatContextPanelViewModelTests.cs
    AFK4.Operator.App.Tests\PlayerSearchViewModelTests.cs
    AFK4.Operator.App.Tests\PosWorkspaceViewModelTests.cs
    AFK4.Operator.App.Tests\ShiftWorkspaceViewModelTests.cs
    AFK4.Operator.App.Tests\SettingsWorkspaceViewModelTests.cs
    AFK4.Operator.App.Tests\OperatorHotkeyServiceTests.cs
```

Responsibility boundaries:

- `Shared.Contracts\Operator`: thin read DTOs needed by Operator UX only.
- `Platform.Api\Billing\EfOperatorReferenceDataService`: read-only EF projections for player search and branch tariff/package option lists.
- `Operator.App\Auth`: sign-in/refresh client and sign-in ViewModel.
- `Operator.App\Shell`: authenticated workspace shell and permission-filtered navigation.
- `Operator.App\FloorMap`: live floor map state and selected-seat context.
- `Operator.App\Sessions`, `Players`, `Pos`, `Shifts`, `Settings`: workflow-specific typed clients and ViewModels.
- `Operator.App\Hotkeys`: command routing for keyboard workflows.

## Test Helper Requirements

The snippets below reference recording fakes. Define them as private nested
classes inside the relevant test files so tests stay independent of WPF
rendering and network calls.

Use this HTTP helper shape in typed-client tests:

```csharp
private static HttpResponseMessage JsonContentResponse<T>(T body)
{
    return new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(body)
    };
}

private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public HttpMethod? LastMethod { get; private set; }
    public string? LastPathAndQuery { get; private set; }
    public string? LastRequestBody { get; private set; }
    public AuthenticationHeaderValue? LastAuthorization { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastMethod = request.Method;
        LastPathAndQuery = request.RequestUri?.PathAndQuery;
        LastAuthorization = request.Headers.Authorization;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return responder(request);
    }
}
```

Use this idempotency helper shape in ViewModel tests:

```csharp
private sealed class FixedIdempotencyKeyFactory(string key) : IIdempotencyKeyFactory
{
    public string Create(string operationName)
    {
        return key;
    }
}
```

Use this token-store helper shape in auth client tests:

```csharp
private sealed class RecordingOperatorTokenStore : IOperatorTokenStore
{
    public OperatorTokenSnapshot? SavedSnapshot { get; private set; }

    public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
    {
        SavedSnapshot = snapshot;
        return Task.CompletedTask;
    }

    public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(SavedSnapshot);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        SavedSnapshot = null;
        return Task.CompletedTask;
    }
}
```

Use this seat helper shape in floor-map and context-panel tests:

```csharp
private static SeatStatusDto Seat(string name, string state)
{
    return new SeatStatusDto(
        SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
        SeatName: name,
        ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
        ZoneName: "Main Hall",
        SortOrder: 1,
        State: state,
        DeviceId: Guid.Parse("33333333-3333-4333-8333-333333333333"),
        DeviceName: name,
        IsDeviceOnline: true,
        IsDeviceLocked: state == "Locked",
        LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
        AgentVersion: "0.1.1",
        ShellVersion: "0.1.2",
        ActiveSessionId: state == "Active" ? Guid.Parse("44444444-4444-4444-8444-444444444444") : null,
        RemainingSeconds: state == "Active" ? 1800 : null);
}
```

Task-specific fakes such as `RecordingSessionApiClient`,
`RecordingPlayerApiClient`, `RecordingPosApiClient`,
`RecordingShiftApiClient`, and `RecordingFloorMapApiClient` must implement the
interface under test, expose the last request properties asserted by the test,
and return the DTO instances shown in that test.

Use this command helper shape in hotkey tests:

```csharp
private sealed class RecordingCommand(bool canExecute) : ICommand
{
    public int ExecuteCount { get; private set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return canExecute;
    }

    public void Execute(object? parameter)
    {
        ExecuteCount++;
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

## Task 1: Operator Authentication And Role-Aware Shell

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Auth\IOperatorAuthApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Auth\HttpOperatorAuthApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Auth\SignInViewModel.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Configuration\OperatorAppOptions.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Shell\OperatorUserContext.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Shell\OperatorWorkspaceKind.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Shell\OperatorNavigationItemViewModel.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Shell\OperatorShellViewModel.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\OperatorAuthApiClientTests.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\OperatorShellViewModelTests.cs`

- [ ] **Step 1: Write failing auth client tests**

Create `OperatorAuthApiClientTests` with this core assertion:

```csharp
[Fact]
public async Task SignInAsync_PostsCredentialsAndSavesTokenSnapshot()
{
    var response = new StaffSignInResponse(
        StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
        OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        DisplayName: "Cashier One",
        AccessToken: "access-token",
        AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
        RefreshToken: "refresh-token",
        RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-15T10:00:00Z"),
        BranchIds: [Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2")],
        Permissions: [StaffPermissionNames.ViewFloorMap]);
    var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(response));
    var tokenStore = new RecordingOperatorTokenStore();
    var client = new HttpOperatorAuthApiClient(new HttpClient(handler)
    {
        BaseAddress = new Uri("http://localhost:5074")
    }, tokenStore);

    var result = await client.SignInAsync(
        response.OrganizationId,
        "cashier",
        "password",
        CancellationToken.None);

    Assert.Equal("/api/auth/staff/sign-in", handler.LastPathAndQuery);
    Assert.Equal("access-token", tokenStore.SavedSnapshot?.AccessToken);
    Assert.Contains(StaffPermissionNames.ViewFloorMap, result.Permissions);
}
```

- [ ] **Step 2: Write failing shell navigation tests**

Create `OperatorShellViewModelTests`:

```csharp
[Fact]
public void SignIn_WithCashierPermissions_ShowsOperationalWorkspacesOnly()
{
    var context = new OperatorUserContext(
        StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
        OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        DisplayName: "Cashier One",
        Permissions:
        [
            StaffPermissionNames.ViewFloorMap,
            StaffPermissionNames.StartSession,
            StaffPermissionNames.CreatePosSale,
            StaffPermissionNames.PayPosSale,
            StaffPermissionNames.OpenShift,
            StaffPermissionNames.ViewShift
        ]);
    var shell = new OperatorShellViewModel();

    shell.ApplySignedInContext(context);

    Assert.True(shell.IsSignedIn);
    Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.FloorMap);
    Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Pos);
    Assert.DoesNotContain(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Settings);
}
```

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorAuthApiClientTests|OperatorShellViewModelTests" --no-restore -p:UseSharedCompilation=false
```

Expected: fail because the auth client and shell types do not exist.

- [ ] **Step 4: Implement shell types**

Implement these public shapes:

```csharp
namespace AFK4.Operator.App.Shell;

public sealed record OperatorUserContext(
    Guid StaffUserId,
    Guid OrganizationId,
    Guid BranchId,
    string DisplayName,
    IReadOnlySet<string> Permissions);

public enum OperatorWorkspaceKind
{
    FloorMap,
    Pos,
    Players,
    Shifts,
    Settings
}

public sealed class OperatorNavigationItemViewModel
{
    public OperatorNavigationItemViewModel(OperatorWorkspaceKind kind, string label, string requiredPermission)
    {
        Kind = kind;
        Label = label;
        RequiredPermission = requiredPermission;
    }

    public OperatorWorkspaceKind Kind { get; }
    public string Label { get; }
    public string RequiredPermission { get; }
}
```

`OperatorShellViewModel` must expose `IsSignedIn`, `CurrentUser`, `SelectedWorkspace`, `NavigationItems`, `ApplySignedInContext`, `SignOutCommand`, and `NavigateCommand`. Navigation items must be filtered from permissions, not role names.

- [ ] **Step 5: Implement auth client and sign-in ViewModel**

`IOperatorAuthApiClient` must expose:

```csharp
Task<StaffSignInResponse> SignInAsync(
    Guid organizationId,
    string userName,
    string password,
    CancellationToken cancellationToken);

Task<StaffSignInResponse> RefreshAsync(
    string refreshToken,
    CancellationToken cancellationToken);
```

`HttpOperatorAuthApiClient` must call `/api/auth/staff/sign-in` and `/api/auth/staff/refresh`, save an `OperatorTokenSnapshot`, and return the backend response. `SignInViewModel` must validate organization id, username, and password before calling the backend and must expose `ErrorMessage`, `StatusMessage`, and `IsBusy`.

- [ ] **Step 6: Wire MainWindow to the shell**

Replace the current top-level `DataContext` with `OperatorShellViewModel`. The first screen must be sign-in when no protected token is loaded. After sign-in, default workspace must be floor map if `floor_map.view` is present.

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorAuthApiClientTests|OperatorShellViewModelTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator auth shell"
```

## Task 2: Real Floor Map Load And Realtime State Store

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\IOperatorFloorMapApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\HttpOperatorFloorMapApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\FloorMapWorkspaceViewModel.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\FloorMapSeatViewModel.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\DeviceStatusStore.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\Realtime\OperatorRealtimeClient.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\FloorMapWorkspaceViewModelTests.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\DeviceStatusStoreTests.cs`

- [ ] **Step 1: Write failing floor map load test**

```csharp
[Fact]
public async Task LoadAsync_ReplacesStaticSeatsWithBackendFloorMap()
{
    var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    var apiClient = new RecordingFloorMapApiClient(new FloorMapDto(
        branchId,
        "Demo Branch",
        [
            new SeatStatusDto(
                SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
                SeatName: "PC-010",
                ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
                ZoneName: "Main Hall",
                SortOrder: 10,
                State: "Active",
                DeviceId: Guid.Parse("33333333-3333-4333-8333-333333333333"),
                DeviceName: "PC-010",
                IsDeviceOnline: true,
                IsDeviceLocked: false,
                LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                AgentVersion: "0.1.1",
                ShellVersion: "0.1.2",
                ActiveSessionId: Guid.Parse("44444444-4444-4444-8444-444444444444"),
                RemainingSeconds: 1800)
        ]));
    var viewModel = new FloorMapWorkspaceViewModel(apiClient);

    await viewModel.LoadAsync(branchId, CancellationToken.None);

    Assert.Equal("Demo Branch", viewModel.BranchName);
    Assert.Single(viewModel.Seats);
    Assert.Equal("PC-010", viewModel.Seats[0].Name);
    Assert.Equal("Active", viewModel.Seats[0].State);
    Assert.Equal(1800, viewModel.Seats[0].RemainingSeconds);
}
```

- [ ] **Step 2: Write failing realtime merge test**

Extend `DeviceStatusStoreTests` so realtime updates match by `DeviceId` when present and by machine name only as a fallback.

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "FloorMapWorkspaceViewModelTests|DeviceStatusStoreTests" --no-restore -p:UseSharedCompilation=false
```

Expected: fail because floor map client/workspace and richer seat state do not exist.

- [ ] **Step 4: Implement floor map client**

`HttpOperatorFloorMapApiClient` must send bearer-authenticated `GET /api/branches/{branchId:D}/floor-map` and deserialize `FloorMapDto` with `JsonSerializerDefaults.Web`.

- [ ] **Step 5: Implement floor map workspace**

`FloorMapWorkspaceViewModel` must expose:

```csharp
public string BranchName { get; }
public ObservableCollection<FloorMapSeatViewModel> Seats { get; }
public FloorMapSeatViewModel? SelectedSeat { get; set; }
public bool IsLoading { get; }
public string? ErrorMessage { get; }
public AsyncRelayCommand RefreshCommand { get; }
public bool ApplyDeviceStatus(DeviceStatusChangedDto status);
```

`FloorMapSeatViewModel` must keep `SeatId`, `DeviceId`, `Name`, `Zone`, `State`, `IsOnline`, `IsLocked`, `ActiveSessionId`, `RemainingSeconds`, `AgentVersion`, `ShellVersion`, `LastHeartbeatAtUtc`, and `IsSelected`. It must expose `public static FloorMapSeatViewModel FromDto(SeatStatusDto dto)` for test and client mapping. It must not infer financial or session authority locally.

- [ ] **Step 6: Wire realtime to the floor map workspace**

`OperatorRealtimeClient` must apply status updates to `FloorMapWorkspaceViewModel`, not the old static `MainWindowViewModel`.

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "FloorMapWorkspaceViewModelTests|DeviceStatusStoreTests|OperatorRealtimeClientTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: load realtime operator floor map"
```

## Task 3: Seat Context Panel Session Actions

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Mvvm\IdempotencyKeyFactory.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Sessions\IOperatorSessionApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Sessions\HttpOperatorSessionApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\SeatContextPanelViewModel.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\SeatContextPanelViewModelTests.cs`

- [ ] **Step 1: Write failing session action tests**

```csharp
[Fact]
public async Task StartGuestSessionAsync_SendsBackendCommandAndMarksPending()
{
    var apiClient = new RecordingSessionApiClient();
    var keyFactory = new FixedIdempotencyKeyFactory("session-start-001");
    var selectedSeat = FloorMapSeatViewModel.FromDto(Seat("PC-001", state: "Free"));
    var panel = new SeatContextPanelViewModel(apiClient, keyFactory);
    panel.SelectSeat(selectedSeat);
    panel.DurationMinutes = 60;
    panel.BillingMode = BillingModeNames.PostpaidDebt;

    await panel.StartGuestSessionAsync(CancellationToken.None);

    Assert.Equal(selectedSeat.SeatId, apiClient.LastStartRequest?.SeatId);
    Assert.Equal("session-start-001", apiClient.LastStartRequest?.IdempotencyKey);
    Assert.Equal("Waiting for backend confirmation", panel.PendingOperation);
    Assert.Null(panel.ErrorMessage);
}

[Fact]
public async Task EndSessionAsync_RequiresActiveSession()
{
    var panel = new SeatContextPanelViewModel(new RecordingSessionApiClient(), new FixedIdempotencyKeyFactory("end-001"));
    panel.SelectSeat(FloorMapSeatViewModel.FromDto(Seat("PC-002", state: "Free")));

    await panel.EndSessionAsync(CancellationToken.None);

    Assert.Equal("Selected seat has no active session.", panel.ErrorMessage);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter SeatContextPanelViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because session client and context panel do not exist.

- [ ] **Step 3: Implement session API client**

`IOperatorSessionApiClient` must expose:

```csharp
Task<SessionCommandResponse> StartGuestSessionAsync(Guid branchId, StartGuestSessionRequest request, CancellationToken cancellationToken);
Task<SessionCommandResponse> ExtendSessionAsync(Guid sessionId, ExtendSessionRequest request, CancellationToken cancellationToken);
Task<SessionCommandResponse> TransferSessionAsync(Guid sessionId, TransferSessionRequest request, CancellationToken cancellationToken);
Task<SessionCommandResponse> EndSessionAsync(Guid sessionId, EndSessionRequest request, CancellationToken cancellationToken);
```

`HttpOperatorSessionApiClient` must call the existing Phase 4/5 endpoints with bearer tokens and surface non-success responses as actionable `HttpRequestException` messages.

- [ ] **Step 4: Implement context panel ViewModel**

`SeatContextPanelViewModel` must:

- expose selected seat, selected player id, duration, billing mode, tariff version id, package id, target transfer seat id, reason, `ErrorMessage`, `PendingOperation`, and `StatusMessage`;
- disable critical commands while `IsBusy`;
- generate a new idempotency key for every command;
- wait for backend response before updating success state;
- ask `FloorMapWorkspaceViewModel.RefreshCommand` to refresh after successful start, extend, transfer, or end.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter SeatContextPanelViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator session context actions"
```

## Task 4: Operator Reference Data Backend Endpoints

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Operator\PlayerSearchResultDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Operator\TariffOptionDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Operator\PackageOptionDto.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\IOperatorReferenceDataService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Billing\EfOperatorReferenceDataService.cs`
- Modify: `D:\afk4.net\src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Test: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\OperatorReferenceContractSerializationTests.cs`
- Test: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\OperatorReferenceDataEndpointTests.cs`

- [ ] **Step 1: Write failing contract serialization tests**

```csharp
[Fact]
public void PlayerSearchResultDto_RoundTripsThroughJson()
{
    var result = new PlayerSearchResultDto(
        PlayerAccountId: Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2"),
        DisplayName: "Alex Player",
        PhoneNumber: "+992000000001",
        WalletBalanceMinorUnits: 12000,
        DebtBalanceMinorUnits: 0,
        ActivePackageCount: 1,
        IsActive: true);

    var json = JsonSerializer.Serialize(result);
    var copy = JsonSerializer.Deserialize<PlayerSearchResultDto>(json);

    Assert.NotNull(copy);
    Assert.Equal("Alex Player", copy.DisplayName);
    Assert.Equal(12000, copy.WalletBalanceMinorUnits);
}
```

- [ ] **Step 2: Write failing endpoint tests**

Add tests for:

- `GET /api/branches/{branchId}/players?query=Alex&limit=20` returns matching players from the same organization/branch;
- `GET /api/branches/{branchId}/tariffs/options` returns active tariff versions for session start/extend modals;
- `GET /api/branches/{branchId}/packages/options` returns active package definitions;
- denied requests write audit rows when the staff token lacks the matching view permission.

- [ ] **Step 3: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter OperatorReferenceContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter OperatorReferenceDataEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because contracts, services, and endpoints do not exist.

- [ ] **Step 4: Implement read DTOs and permissions**

Add:

```csharp
public const string ViewPlayers = "players.view";
public const string ViewTariffs = "tariffs.view";
public const string ViewPackages = "packages.view";
```

Add these permissions to owner, branch manager, shift supervisor, cashier/operator, and accountant/auditor where the existing role intent requires read visibility. Technician gets only permissions needed for device and inventory diagnostics.

- [ ] **Step 5: Implement EF reference data service**

`EfOperatorReferenceDataService` must:

- search active `PlayerAccountEntity` rows by display name or phone substring, scoped by organization and branch;
- project wallet and debt from immutable `ledger_entries`;
- count active player packages from `player_packages`;
- list active tariffs with latest non-retired `TariffVersionEntity`;
- list active package definitions scoped by branch.

- [ ] **Step 6: Wire endpoints**

Add endpoints:

```text
GET /api/branches/{branchId:guid}/players
GET /api/branches/{branchId:guid}/tariffs/options
GET /api/branches/{branchId:guid}/packages/options
```

Each endpoint must use `RequireBranchPermissionAsync`, return `Forbid` through the existing helper path when authorization fails, and include audit coverage for denied privileged reads.

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter OperatorReferenceContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter OperatorReferenceDataEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Shared.Contracts.Tests tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator reference data endpoints"
```

## Task 5: Player Search Workflow

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Players\IOperatorPlayerApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Players\HttpOperatorPlayerApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Players\PlayerSearchViewModel.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\PlayerSearchViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel tests**

```csharp
[Fact]
public async Task SearchAsync_RequiresTwoCharactersAndShowsBackendResults()
{
    var apiClient = new RecordingPlayerApiClient
    {
        SearchResults =
        [
            new PlayerSearchResultDto(
                Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2"),
                "Alex Player",
                "+992000000001",
                WalletBalanceMinorUnits: 12000,
                DebtBalanceMinorUnits: 0,
                ActivePackageCount: 1,
                IsActive: true)
        ]
    };
    var viewModel = new PlayerSearchViewModel(apiClient);
    viewModel.SearchText = "Al";

    await viewModel.SearchAsync(CancellationToken.None);

    Assert.Single(viewModel.Results);
    Assert.Equal("Alex Player", viewModel.Results[0].DisplayName);
    Assert.Equal("Wallet 120.00 USD / debt 0.00 USD", viewModel.Results[0].BalanceSummary);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter PlayerSearchViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because player workflow types do not exist.

- [ ] **Step 3: Implement typed client**

`IOperatorPlayerApiClient` must wrap:

- `GET /api/branches/{branchId}/players?query={query}&limit={limit}`;
- `POST /api/branches/{branchId}/players`;
- `GET /api/players/{playerAccountId}/wallet-summary`;
- `GET /api/players/{playerAccountId}/packages`;
- `POST /api/players/{playerAccountId}/wallet/top-ups`;
- `POST /api/players/{playerAccountId}/debts/payments`.

- [ ] **Step 4: Implement ViewModel**

`PlayerSearchViewModel` must expose search text, result list, selected player, create-player fields, wallet summary, package summary, top-up/debt payment commands, and explicit permission-denied/network errors. It must not cache wallet/debt as authority; every money-changing operation must wait for backend confirmation and refresh summaries afterward.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter PlayerSearchViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator player search workflow"
```

## Task 6: POS Workflow

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Pos\IOperatorPosApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Pos\HttpOperatorPosApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Pos\PosCartLineViewModel.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Pos\PosWorkspaceViewModel.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\PosWorkspaceViewModelTests.cs`

- [ ] **Step 1: Write failing POS workflow tests**

```csharp
[Fact]
public async Task PayCashAsync_CreatesSaleThenManualPaymentAndExposesReceiptState()
{
    var apiClient = new RecordingPosApiClient();
    var viewModel = new PosWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("pos-sale-001"));
    viewModel.SetCurrentShift(Guid.Parse("55555555-5555-4555-8555-555555555555"));
    await viewModel.LoadCatalogAsync(CancellationToken.None);
    viewModel.AddProduct(apiClient.Catalog[0]);

    await viewModel.PayCashAsync(CancellationToken.None);

    Assert.Equal("pos-sale-001", apiClient.LastCreateSaleRequest?.IdempotencyKey);
    Assert.Equal(PaymentMethodNames.Cash, apiClient.LastManualPaymentRequest?.Method);
    Assert.Equal("Paid", viewModel.LastSaleState);
    Assert.Null(viewModel.ErrorMessage);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter PosWorkspaceViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because POS workflow types do not exist.

- [ ] **Step 3: Implement typed POS client**

Wrap these existing endpoints:

- `GET /api/branches/{branchId}/pos/catalog`;
- `POST /api/branches/{branchId}/pos/sales`;
- `POST /api/pos/sales/{saleId}/payments/manual`;
- `POST /api/pos/sales/{saleId}/refunds`;
- `POST /api/pos/sales/{saleId}/void`;
- `GET /api/pos/sales/{saleId}`;
- `GET /api/receipts/{receiptId}`.

- [ ] **Step 4: Implement POS ViewModel**

`PosWorkspaceViewModel` must:

- load catalog grouped by product category;
- track cart quantities and totals in minor units;
- require an open shift before create/pay;
- generate idempotency keys for create sale, manual payment, refund, and void;
- support cash and `card_manual` payments;
- expose receipt id and sale state after payment;
- show stock validation failures from backend without locally changing stock as authority.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter PosWorkspaceViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator pos workflow"
```

## Task 7: Shift Workflow

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Shifts\IOperatorShiftApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Shifts\HttpOperatorShiftApiClient.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\Shifts\ShiftWorkspaceViewModel.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\ShiftWorkspaceViewModelTests.cs`

- [ ] **Step 1: Write failing shift workflow tests**

```csharp
[Fact]
public async Task OpenShiftAsync_SendsStartingCashAndStoresCurrentShift()
{
    var apiClient = new RecordingShiftApiClient();
    var viewModel = new ShiftWorkspaceViewModel(apiClient, new FixedIdempotencyKeyFactory("shift-open-001"));
    viewModel.StartingCashMinorUnits = 50000;
    viewModel.OpeningNote = "Morning shift";

    await viewModel.OpenShiftAsync(CancellationToken.None);

    Assert.Equal("shift-open-001", apiClient.LastOpenRequest?.IdempotencyKey);
    Assert.Equal(50000, apiClient.LastOpenRequest?.StartingCash.MinorUnits);
    Assert.Equal(ShiftStateNames.Open, viewModel.CurrentShift?.State);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter ShiftWorkspaceViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because shift workflow types do not exist.

- [ ] **Step 3: Implement shift client**

Wrap:

- `POST /api/branches/{branchId}/shifts/open`;
- `GET /api/branches/{branchId}/shifts/current`;
- `POST /api/shifts/{shiftId}/cash-movements`;
- `POST /api/shifts/{shiftId}/close`.

- [ ] **Step 4: Implement shift ViewModel**

`ShiftWorkspaceViewModel` must expose current shift state, starting cash, counted cash, cash movement amount/type/reason, closing note, expected cash, difference, and command state. It must reject POS and money workflows when the current shift is missing or closed.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter ShiftWorkspaceViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator shift workflow"
```

## Task 8: Settings And Technician Workspace

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Settings\SettingsWorkspaceViewModel.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\Devices\TechnicianDeviceWorkflowViewModel.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\SettingsWorkspaceViewModelTests.cs`

- [ ] **Step 1: Write failing settings tests**

```csharp
[Fact]
public void SettingsWorkspace_ExposesOnlyPermissionAllowedPanels()
{
    var permissions = new HashSet<string>
    {
        StaffPermissionNames.ViewDeviceDetail,
        StaffPermissionNames.ManageInventoryStock,
        StaffPermissionNames.ManagePosCatalog
    };

    var viewModel = new SettingsWorkspaceViewModel(permissions);

    Assert.Contains(viewModel.Panels, panel => panel.Key == "devices");
    Assert.Contains(viewModel.Panels, panel => panel.Key == "pos-catalog");
    Assert.DoesNotContain(viewModel.Panels, panel => panel.Key == "roles");
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter SettingsWorkspaceViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because settings workspace types do not exist.

- [ ] **Step 3: Move technician tools into settings**

Keep `TechnicianDeviceWorkflowViewModel`, but remove the technician panel from the default floor-map context. Display it under Settings when the user has device detail or credential permissions.

- [ ] **Step 4: Add settings panels**

`SettingsWorkspaceViewModel` must expose panels for:

- Operator App connection settings: API base URL, organization id, branch id;
- POS catalog and stock setup, using existing Phase 6 product/category/stock endpoints;
- tariff and package management, using existing create endpoints and Phase 7 option read endpoints;
- technician device tools, reusing the existing device workflow.

Do not add updates/installers settings in Phase 7.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter SettingsWorkspaceViewModelTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator settings workspace"
```

## Task 9: Hotkeys And Dense WPF Layout Integration

**Files:**

- Create: `D:\afk4.net\src\AFK4.Operator.App\Hotkeys\OperatorHotkeyService.cs`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml.cs`
- Test: `D:\afk4.net\tests\AFK4.Operator.App.Tests\OperatorHotkeyServiceTests.cs`

- [ ] **Step 1: Write failing hotkey tests**

```csharp
[Fact]
public void Resolve_ReturnsOnlyEnabledCommandForCurrentWorkspace()
{
    var service = new OperatorHotkeyService();
    var command = new RecordingCommand(canExecute: true);
    service.Register(OperatorWorkspaceKind.FloorMap, "F2", command);

    var resolved = service.Resolve(OperatorWorkspaceKind.FloorMap, "F2");

    Assert.Same(command, resolved);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter OperatorHotkeyServiceTests --no-restore -p:UseSharedCompilation=false
```

Expected: fail because hotkey service does not exist.

- [ ] **Step 3: Implement hotkey service**

Register these defaults:

```text
F1  Focus floor map
F2  Start session for selected free seat
F3  Extend selected active session
F4  End selected active session
F5  Refresh current workspace
F6  Open POS
F7  Search players
F8  Open/current shift
Ctrl+L Sign out
Esc Close active modal or clear transient panel state
```

Hotkeys must respect command `CanExecute` and role-aware workspace availability.

- [ ] **Step 4: Integrate WPF layout**

Update `MainWindow.xaml` into a dense operator layout:

- top bar: signed-in user, branch, current shift state, realtime connection state;
- left rail: permission-filtered workspaces;
- center: selected workspace content;
- right panel: selected seat/player/sale/shift context;
- bottom status strip: last operation status and error.

The main screen after sign-in must remain floor map.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorHotkeyServiceTests|OperatorShellViewModelTests|FloorMapWorkspaceViewModelTests|SeatContextPanelViewModelTests|PosWorkspaceViewModelTests|PlayerSearchViewModelTests|ShiftWorkspaceViewModelTests" --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: integrate operator production layout"
```

## Task 10: Full Verification And Local Smoke

**Files:**

- Modify only files required to fix concrete failures found by verification.

- [ ] **Step 1: Run targeted contract and backend tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter OperatorReferenceContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter OperatorReferenceDataEndpointTests --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

- [ ] **Step 2: Run Operator App tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Expected: pass.

- [ ] **Step 3: Run full build and full tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
0 failed
```

- [ ] **Step 4: Run live PostgreSQL Operator smoke**

Run the API with a PostgreSQL database already migrated through Phase 6. Then sign in through the Operator App and verify:

- current shift loads or can be opened;
- floor map loads from backend persisted seats and updates on heartbeat;
- selected free seat can start a guest session and shows pending/success state;
- selected active session can be extended and ended;
- player search returns seeded player rows;
- POS catalog loads, cart can be paid with manual cash, and receipt can be read;
- shift can record a cash movement and close with expected/count/difference displayed;
- technician device detail still works from Settings;
- cashier role cannot see manager-only settings panels.

- [ ] **Step 5: Update progress and commit final Phase 7 evidence**

Update `docs/progress/2026-05-12-vertical-slice-progress.md` with exact commands, pass counts, live smoke date, known limitations, and next roadmap recommendation.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs src tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: record phase 7 verification"
```

## Plan Self-Review

Spec coverage:

- Realtime floor map state is covered by Task 2.
- Context panel actions are covered by Task 3.
- POS workflow is covered by Task 6.
- Player search is covered by Tasks 4 and 5.
- Shift workflow is covered by Task 7.
- Settings are covered by Task 8.
- Role-aware navigation is covered by Task 1.
- Hotkeys are covered by Task 9.

Architecture alignment:

- Operator App remains native WPF + MVVM.
- Backend remains the source of truth for sessions, money, POS, and shifts.
- Local Operator state is UI acceleration only.
- Critical commands wait for REST responses and use idempotency keys.
- Shared DTOs stay in `AFK4.Shared.Contracts`.
- The backend stays a modular monolith with EF read services.

Out of Phase 7:

- Agent enforcement and Player Shell production UI are owned by Phase 8.
- Updates, installers, audit search, reports, diagnostics, and backup/restore runbooks are owned by Phase 9.
- Web admin, local club server, microservices, fiscal integrations, and external payment gateways remain excluded from the MVP decisions unless the PRD and architecture spec change first.
