# AFK4 Realtime Device Channel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first realtime device channel foundation: Agent Service opens an outgoing SignalR connection, backend can dispatch device commands over the hub, Agent reports command acknowledgements, and Operator App updates floor-map state from realtime device status events.

**Architecture:** Keep the existing ASP.NET Core modular monolith and WPF/Worker projects. This slice adds SignalR as the device command and operator status transport while preserving the current HTTP heartbeat endpoint as a compatibility heartbeat path until enrollment, credentials, command persistence, and full device state storage get their own plans.

**Tech Stack:** .NET 10, ASP.NET Core SignalR, `Microsoft.AspNetCore.SignalR.Client`, WPF + MVVM, Worker Service, xUnit, in-memory services for the current foundation slice.

---

## Scope

This plan is intentionally limited to realtime transport wiring and testable state flow. It does not implement device authentication, enrollment credentials, PostgreSQL persistence, real lock/unlock enforcement, command retries, signed leases, session lifecycle, RBAC, audit persistence, or update rollout.

The current HTTP heartbeat remains in place:

```text
Agent -> POST /api/devices/{deviceId}/heartbeat -> Backend -> SignalR deviceStatusChanged broadcast
```

This plan adds:

```text
Agent -> SignalR /hubs/devices -> Backend device group registration
Backend -> SignalR deviceCommand -> Agent
Agent -> SignalR ReportCommandResultAsync -> Backend/Operator broadcast
Backend -> SignalR deviceStatusChanged -> Operator App floor-map state store
```

## Prerequisites

Work in the isolated branch workspace:

```powershell
Set-Location 'D:\afk4.net\.worktrees\realtime-device-channel'
git status --short --branch
```

Expected:

```text
## feature/realtime-device-channel
```

Baseline verification before coding:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' restore AFK4.sln
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
Failed: 0, Passed: 14
```

## File Structure

Create and modify these files:

```text
D:\afk4.net\.worktrees\realtime-device-channel\
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\plans\2026-05-12-afk4-realtime-device-channel.md
  src\
    AFK4.Shared.Contracts\
      Devices\
        DeviceConnectionRequest.cs
        DeviceCommandResultDto.cs
        DeviceRealtimeEvents.cs
        DeviceRealtimeMethods.cs
    AFK4.Platform.Api\
      Devices\
        CreateDeviceCommandRequest.cs
        DeviceCommandDispatchService.cs
        DeviceHub.cs
        DeviceHubGroups.cs
        IDeviceCommandDispatchService.cs
      Program.cs
    AFK4.Agent.Service\
      AFK4.Agent.Service.csproj
      DeviceConnectionRequestFactory.cs
      DeviceRealtimeClient.cs
      DefaultDeviceCommandHandler.cs
      IDeviceCommandHandler.cs
      Program.cs
      Worker.cs
    AFK4.Operator.App\
      AFK4.Operator.App.csproj
      MainWindow.xaml.cs
      Realtime\
        OperatorRealtimeClient.cs
      FloorMap\
        DeviceStatusStore.cs
        FloorMapSeatViewModel.cs
        MainWindowViewModel.cs
  tests\
    AFK4.Shared.Contracts.Tests\
      DeviceRealtimeContractSerializationTests.cs
    AFK4.Platform.Api.Tests\
      DeviceCommandEndpointTests.cs
      DeviceHubGroupsTests.cs
    AFK4.Agent.Service.Tests\
      DeviceConnectionRequestFactoryTests.cs
      DefaultDeviceCommandHandlerTests.cs
    AFK4.Operator.App.Tests\
      DeviceStatusStoreTests.cs
```

Responsibility boundaries:

- `AFK4.Shared.Contracts`: shared DTOs and stable SignalR event/method names.
- `AFK4.Platform.Api.Devices`: hub registration, device groups, in-memory command dispatch, and command result rebroadcast.
- `AFK4.Agent.Service`: outgoing hub connection, registration payload creation, and current no-op command acknowledgement.
- `AFK4.Operator.App.FloorMap`: testable floor-map state changes from device status events.
- `AFK4.Operator.App.Realtime`: WPF runtime SignalR subscription glue.

## Task 1: Shared Realtime Device Contracts

**Files:**

- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceConnectionRequest.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceCommandResultDto.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceRealtimeEvents.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceRealtimeMethods.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Shared.Contracts.Tests\DeviceRealtimeContractSerializationTests.cs`

- [ ] **Step 1: Write failing realtime contract tests**

Create `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Shared.Contracts.Tests\DeviceRealtimeContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceRealtimeContractSerializationTests
{
    [Fact]
    public void DeviceConnectionRequest_RoundTripsThroughJson()
    {
        var request = new DeviceConnectionRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ConnectedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<DeviceConnectionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.Equal("0.1.0", copy.AgentVersion);
    }

    [Fact]
    public void DeviceCommandResultDto_RoundTripsThroughJson()
    {
        var result = new DeviceCommandResultDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Status: "Accepted",
            Message: "Command accepted by Agent skeleton.",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:05Z"));

        var json = JsonSerializer.Serialize(result);
        var copy = JsonSerializer.Deserialize<DeviceCommandResultDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(result.CommandId, copy.CommandId);
        Assert.Equal("Accepted", copy.Status);
        Assert.Equal("Command accepted by Agent skeleton.", copy.Message);
    }

    [Fact]
    public void DeviceRealtimeNames_AreStable()
    {
        Assert.Equal("deviceStatusChanged", DeviceRealtimeEvents.DeviceStatusChanged);
        Assert.Equal("deviceCommand", DeviceRealtimeEvents.DeviceCommand);
        Assert.Equal("deviceCommandResult", DeviceRealtimeEvents.DeviceCommandResult);
        Assert.Equal("deviceRegistered", DeviceRealtimeEvents.DeviceRegistered);
        Assert.Equal("RegisterDeviceAsync", DeviceRealtimeMethods.RegisterDeviceAsync);
        Assert.Equal("ReportCommandResultAsync", DeviceRealtimeMethods.ReportCommandResultAsync);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DeviceRealtimeContractSerializationTests
```

Expected:

```text
error CS0246: The type or namespace name 'DeviceConnectionRequest' could not be found
```

- [ ] **Step 3: Add realtime connection DTO**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceConnectionRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceConnectionRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset ConnectedAtUtc);
```

- [ ] **Step 4: Add command result DTO**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceCommandResultDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceCommandResultDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    Guid CommandId,
    string Status,
    string Message,
    DateTimeOffset ObservedAtUtc);
```

- [ ] **Step 5: Add realtime event constants**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceRealtimeEvents.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public static class DeviceRealtimeEvents
{
    public const string DeviceStatusChanged = "deviceStatusChanged";

    public const string DeviceCommand = "deviceCommand";

    public const string DeviceCommandResult = "deviceCommandResult";

    public const string DeviceRegistered = "deviceRegistered";
}
```

- [ ] **Step 6: Add realtime hub method constants**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Shared.Contracts\Devices\DeviceRealtimeMethods.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public static class DeviceRealtimeMethods
{
    public const string RegisterDeviceAsync = nameof(RegisterDeviceAsync);

    public const string ReportCommandResultAsync = nameof(ReportCommandResultAsync);
}
```

- [ ] **Step 7: Run contract tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DeviceRealtimeContractSerializationTests
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 8: Commit realtime contracts**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts/Devices tests/AFK4.Shared.Contracts.Tests/DeviceRealtimeContractSerializationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add realtime device contracts"
```

Expected:

```text
[feature/realtime-device-channel ...] feat: add realtime device contracts
```

## Task 2: Backend Device Hub Registration And Command Dispatch

**Files:**

- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\CreateDeviceCommandRequest.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\DeviceCommandDispatchService.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\DeviceHubGroups.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\IDeviceCommandDispatchService.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\DeviceHub.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\InMemoryDeviceHeartbeatService.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Platform.Api.Tests\DeviceCommandEndpointTests.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Platform.Api.Tests\DeviceHubGroupsTests.cs`

- [ ] **Step 1: Write failing hub group tests**

Create `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Platform.Api.Tests\DeviceHubGroupsTests.cs`:

```csharp
using AFK4.Platform.Api.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceHubGroupsTests
{
    [Fact]
    public void Device_ReturnsStableDeviceGroupName()
    {
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");

        var group = DeviceHubGroups.Device(deviceId);

        Assert.Equal("device:d76eff15-9cf9-4c30-a6d4-c05fd215793f", group);
    }
}
```

- [ ] **Step 2: Write failing command endpoint test**

Create `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Platform.Api.Tests\DeviceCommandEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCommandEndpointTests
{
    [Fact]
    public async Task PostDeviceCommand_ReturnsCommandDispatchedToDeviceGroup()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var response = await client.PostAsJsonAsync(
            $"/api/devices/{deviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var command = await response.Content.ReadFromJsonAsync<DeviceCommandDto>();

        Assert.NotNull(command);
        Assert.NotEqual(Guid.Empty, command.CommandId);
        Assert.Equal("lock", command.Type);
        Assert.Equal("operator-request", command.Payload["reason"]);
    }
}
```

- [ ] **Step 3: Run API tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "DeviceHubGroupsTests|DeviceCommandEndpointTests"
```

Expected:

```text
error CS0246: The type or namespace name 'DeviceHubGroups' could not be found
```

- [ ] **Step 4: Add device hub group helper**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\DeviceHubGroups.cs`:

```csharp
namespace AFK4.Platform.Api.Devices;

public static class DeviceHubGroups
{
    public static string Device(Guid deviceId) => $"device:{deviceId:D}";
}
```

- [ ] **Step 5: Add command request contract for the API host**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\CreateDeviceCommandRequest.cs`:

```csharp
namespace AFK4.Platform.Api.Devices;

public sealed record CreateDeviceCommandRequest(
    string Type,
    IReadOnlyDictionary<string, string> Payload);
```

- [ ] **Step 6: Add command dispatch service interface**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\IDeviceCommandDispatchService.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceCommandDispatchService
{
    Task<DeviceCommandDto> DispatchAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 7: Add in-memory SignalR command dispatcher**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\DeviceCommandDispatchService.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceCommandDispatchService(IHubContext<DeviceHub> hubContext) : IDeviceCommandDispatchService
{
    public async Task<DeviceCommandDto> DispatchAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: request.Type,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: request.Payload);

        await hubContext.Clients
            .Group(DeviceHubGroups.Device(deviceId))
            .SendAsync(DeviceRealtimeEvents.DeviceCommand, command, cancellationToken);

        return command;
    }
}
```

- [ ] **Step 8: Expand DeviceHub for registration and command result reports**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\DeviceHub.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHub(ILogger<DeviceHub> logger) : Hub
{
    public async Task RegisterDeviceAsync(DeviceConnectionRequest request)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DeviceHubGroups.Device(request.DeviceId),
            Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            DeviceRealtimeEvents.DeviceRegistered,
            request.DeviceId,
            Context.ConnectionAborted);

        logger.LogInformation(
            "Device {DeviceId} registered realtime connection {ConnectionId}.",
            request.DeviceId,
            Context.ConnectionId);
    }

    public async Task ReportCommandResultAsync(DeviceCommandResultDto result)
    {
        await Clients.All.SendAsync(
            DeviceRealtimeEvents.DeviceCommandResult,
            result,
            Context.ConnectionAborted);

        logger.LogInformation(
            "Device {DeviceId} reported command {CommandId} as {Status}.",
            result.DeviceId,
            result.CommandId,
            result.Status);
    }
}
```

- [ ] **Step 9: Use event constants in heartbeat broadcast**

Modify `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Devices\InMemoryDeviceHeartbeatService.cs` so the broadcast line is:

```csharp
await hubContext.Clients.All.SendAsync(DeviceRealtimeEvents.DeviceStatusChanged, status, cancellationToken);
```

The complete file after the change:

```csharp
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class InMemoryDeviceHeartbeatService(IHubContext<DeviceHub> hubContext) : IDeviceHeartbeatService
{
    public async Task<DeviceHeartbeatResponse> RecordHeartbeatAsync(
        Guid deviceId,
        DeviceHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var status = new DeviceStatusChangedDto(
            OrganizationId: request.OrganizationId,
            BranchId: request.BranchId,
            DeviceId: deviceId,
            MachineName: request.MachineName,
            IsOnline: true,
            IsLocked: request.IsLocked,
            ObservedAtUtc: request.ObservedAtUtc);

        await hubContext.Clients.All.SendAsync(DeviceRealtimeEvents.DeviceStatusChanged, status, cancellationToken);

        return new DeviceHeartbeatResponse(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            HeartbeatIntervalSeconds: 10,
            Commands: []);
    }
}
```

- [ ] **Step 10: Wire command dispatch endpoint**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Platform.Api\Program.cs`:

```csharp
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Shared.Contracts.Devices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<IDeviceCommandDispatchService, DeviceCommandDispatchService>();
builder.Services.AddSingleton<IDeviceHeartbeatService, InMemoryDeviceHeartbeatService>();
builder.Services.AddSingleton<IFloorMapReadService, InMemoryFloorMapReadService>();

var app = builder.Build();

app.MapGet("/api/health", () =>
{
    return Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow));
});

app.MapGet("/api/branches/{branchId:guid}/floor-map", (
    Guid branchId,
    IFloorMapReadService floorMapReadService) =>
{
    return Results.Ok(floorMapReadService.GetFloorMap(branchId));
});

app.MapPost("/api/devices/{deviceId:guid}/heartbeat", async (
    Guid deviceId,
    DeviceHeartbeatRequest request,
    IDeviceHeartbeatService heartbeatService,
    CancellationToken cancellationToken) =>
{
    if (deviceId != request.DeviceId)
    {
        return Results.BadRequest(new { Error = "Route deviceId must match request DeviceId." });
    }

    var response = await heartbeatService.RecordHeartbeatAsync(deviceId, request, cancellationToken);

    return Results.Ok(response);
});

app.MapPost("/api/devices/{deviceId:guid}/commands", async (
    Guid deviceId,
    CreateDeviceCommandRequest request,
    IDeviceCommandDispatchService commandDispatchService,
    CancellationToken cancellationToken) =>
{
    var command = await commandDispatchService.DispatchAsync(deviceId, request, cancellationToken);

    return Results.Ok(command);
});

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
```

- [ ] **Step 11: Run API tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "DeviceHubGroupsTests|DeviceCommandEndpointTests"
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 12: Commit backend realtime command dispatch**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add backend device realtime command dispatch"
```

Expected:

```text
[feature/realtime-device-channel ...] feat: add backend device realtime command dispatch
```

## Task 3: Agent SignalR Client And Command Acknowledgement

**Files:**

- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\AFK4.Agent.Service.csproj`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\DeviceConnectionRequestFactory.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\DeviceRealtimeClient.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\DefaultDeviceCommandHandler.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\IDeviceCommandHandler.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\Program.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\Worker.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Agent.Service.Tests\DeviceConnectionRequestFactoryTests.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Agent.Service.Tests\DefaultDeviceCommandHandlerTests.cs`

- [ ] **Step 1: Add SignalR client package**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' add src/AFK4.Agent.Service/AFK4.Agent.Service.csproj package Microsoft.AspNetCore.SignalR.Client --version 10.0.7
```

Expected:

```text
PackageReference for package 'Microsoft.AspNetCore.SignalR.Client' added
```

- [ ] **Step 2: Write failing connection request factory test**

Create `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Agent.Service.Tests\DeviceConnectionRequestFactoryTests.cs`:

```csharp
using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

public sealed class DeviceConnectionRequestFactoryTests
{
    [Fact]
    public void Create_BuildsConnectionRequestFromAgentOptions()
    {
        var options = new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0"
        };

        var request = DeviceConnectionRequestFactory.Create(
            options,
            DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        Assert.Equal(options.OrganizationId, request.OrganizationId);
        Assert.Equal(options.BranchId, request.BranchId);
        Assert.Equal(options.DeviceId, request.DeviceId);
        Assert.Equal("PC-001", request.MachineName);
        Assert.Equal("0.1.0", request.AgentVersion);
        Assert.Equal("0.1.0", request.ShellVersion);
    }
}
```

- [ ] **Step 3: Write failing command handler test**

Create `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Agent.Service.Tests\DefaultDeviceCommandHandlerTests.cs`:

```csharp
using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class DefaultDeviceCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_AcknowledgesCommandForConfiguredDevice()
    {
        var options = Options.Create(new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001"
        });

        var handler = new DefaultDeviceCommandHandler(options);
        var command = new DeviceCommandDto(
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Type: "lock",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "operator-request"
            });

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(options.Value.OrganizationId, result.OrganizationId);
        Assert.Equal(options.Value.BranchId, result.BranchId);
        Assert.Equal(options.Value.DeviceId, result.DeviceId);
        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal("Accepted", result.Status);
        Assert.Equal("Command accepted by Agent skeleton.", result.Message);
    }
}
```

- [ ] **Step 4: Run agent tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "DeviceConnectionRequestFactoryTests|DefaultDeviceCommandHandlerTests"
```

Expected:

```text
error CS0103: The name 'DeviceConnectionRequestFactory' does not exist
```

- [ ] **Step 5: Add connection request factory**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\DeviceConnectionRequestFactory.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public static class DeviceConnectionRequestFactory
{
    public static DeviceConnectionRequest Create(AgentOptions options, DateTimeOffset connectedAtUtc)
    {
        return new DeviceConnectionRequest(
            OrganizationId: options.OrganizationId,
            BranchId: options.BranchId,
            DeviceId: options.DeviceId,
            MachineName: options.MachineName,
            AgentVersion: options.AgentVersion,
            ShellVersion: options.ShellVersion,
            ConnectedAtUtc: connectedAtUtc);
    }
}
```

- [ ] **Step 6: Add device command handler interface**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\IDeviceCommandHandler.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public interface IDeviceCommandHandler
{
    Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken);
}
```

- [ ] **Step 7: Add default no-op command acknowledgement handler**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\DefaultDeviceCommandHandler.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DefaultDeviceCommandHandler(IOptions<AgentOptions> options) : IDeviceCommandHandler
{
    public Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var result = new DeviceCommandResultDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            CommandId: command.CommandId,
            Status: "Accepted",
            Message: "Command accepted by Agent skeleton.",
            ObservedAtUtc: DateTimeOffset.UtcNow);

        return Task.FromResult(result);
    }
}
```

- [ ] **Step 8: Add Agent SignalR realtime client**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\DeviceRealtimeClient.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DeviceRealtimeClient : IAsyncDisposable
{
    private readonly AgentOptions options;
    private readonly IDeviceCommandHandler commandHandler;
    private readonly ILogger<DeviceRealtimeClient> logger;
    private readonly HubConnection connection;

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger)
    {
        this.options = options.Value;
        this.commandHandler = commandHandler;
        this.logger = logger;
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(this.options.PlatformBaseUrl, "/hubs/devices"))
            .WithAutomaticReconnect()
            .Build();

        connection.On<DeviceCommandDto>(DeviceRealtimeEvents.DeviceCommand, HandleCommandAsync);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await connection.StartAsync(cancellationToken);

        var request = DeviceConnectionRequestFactory.Create(options, DateTimeOffset.UtcNow);
        await connection.InvokeAsync(DeviceRealtimeMethods.RegisterDeviceAsync, request, cancellationToken);

        logger.LogInformation("Realtime device channel connected for {DeviceId}.", options.DeviceId);
    }

    private async Task HandleCommandAsync(DeviceCommandDto command)
    {
        var result = await commandHandler.HandleAsync(command, CancellationToken.None);
        await connection.InvokeAsync(DeviceRealtimeMethods.ReportCommandResultAsync, result);

        logger.LogInformation(
            "Command {CommandId} acknowledged as {Status}.",
            command.CommandId,
            result.Status);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}
```

- [ ] **Step 9: Register realtime client and command handler**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\Program.cs`:

```csharp
using AFK4.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient("platform");
builder.Services.AddSingleton<IDeviceCommandHandler, DefaultDeviceCommandHandler>();
builder.Services.AddSingleton<DeviceRealtimeClient>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

- [ ] **Step 10: Start realtime client from Worker**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Agent.Service\Worker.cs`:

```csharp
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    DeviceRealtimeClient realtimeClient) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
        await realtimeClient.StartAsync(stoppingToken);

        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        while (!stoppingToken.IsCancellationRequested)
        {
            var request = HeartbeatPayloadFactory.Create(agentOptions, isLocked: true, DateTimeOffset.UtcNow);
            var response = await client.PostAsJsonAsync(
                $"/api/devices/{agentOptions.DeviceId}/heartbeat",
                request,
                stoppingToken);

            response.EnsureSuccessStatusCode();
            var heartbeat = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>(cancellationToken: stoppingToken);
            var intervalSeconds = heartbeat?.HeartbeatIntervalSeconds ?? 10;

            logger.LogInformation("Heartbeat sent for {DeviceId}. Next heartbeat in {IntervalSeconds}s.", agentOptions.DeviceId, intervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await realtimeClient.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
```

- [ ] **Step 11: Run agent tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "DeviceConnectionRequestFactoryTests|DefaultDeviceCommandHandlerTests"
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 12: Commit Agent realtime client**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Agent.Service tests/AFK4.Agent.Service.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add agent realtime device client"
```

Expected:

```text
[feature/realtime-device-channel ...] feat: add agent realtime device client
```

## Task 4: Operator App Realtime Floor Map State

**Files:**

- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\AFK4.Operator.App.csproj`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\FloorMap\FloorMapSeatViewModel.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\FloorMap\MainWindowViewModel.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\FloorMap\DeviceStatusStore.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\Realtime\OperatorRealtimeClient.cs`
- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\MainWindow.xaml.cs`
- Create: `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Operator.App.Tests\DeviceStatusStoreTests.cs`

- [ ] **Step 1: Add SignalR client package**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' add src/AFK4.Operator.App/AFK4.Operator.App.csproj package Microsoft.AspNetCore.SignalR.Client --version 10.0.7
```

Expected:

```text
PackageReference for package 'Microsoft.AspNetCore.SignalR.Client' added
```

- [ ] **Step 2: Write failing device status store test**

Create `D:\afk4.net\.worktrees\realtime-device-channel\tests\AFK4.Operator.App.Tests\DeviceStatusStoreTests.cs`:

```csharp
using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.Tests;

public sealed class DeviceStatusStoreTests
{
    [Fact]
    public void Apply_UpdatesSeatStateByMachineName()
    {
        var viewModel = new MainWindowViewModel();
        var status = new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-002",
            IsOnline: true,
            IsLocked: false,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        var updated = viewModel.ApplyDeviceStatus(status);

        Assert.True(updated);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-002" && seat.State == "Free" && seat.IsOnline);
    }

    [Fact]
    public void Apply_MarksOfflineDeviceAsOffline()
    {
        var viewModel = new MainWindowViewModel();
        var status = new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            IsOnline: false,
            IsLocked: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        var updated = viewModel.ApplyDeviceStatus(status);

        Assert.True(updated);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-001" && seat.State == "Offline" && !seat.IsOnline);
    }
}
```

- [ ] **Step 3: Run Operator tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter DeviceStatusStoreTests
```

Expected:

```text
error CS1061: 'MainWindowViewModel' does not contain a definition for 'ApplyDeviceStatus'
```

- [ ] **Step 4: Make floor map seat state mutable and observable**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\FloorMap\FloorMapSeatViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapSeatViewModel : INotifyPropertyChanged
{
    private string state;
    private bool isOnline = true;

    public FloorMapSeatViewModel(string name, string zone, string state)
    {
        Name = name;
        Zone = zone;
        this.state = state;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string Zone { get; }

    public string State
    {
        get => state;
        private set => SetField(ref state, value);
    }

    public bool IsOnline
    {
        get => isOnline;
        private set => SetField(ref isOnline, value);
    }

    public void ApplyDeviceState(bool isOnline, bool isLocked)
    {
        IsOnline = isOnline;
        State = isOnline
            ? isLocked ? "Locked" : "Free"
            : "Offline";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
```

- [ ] **Step 5: Add floor-map device status store**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\FloorMap\DeviceStatusStore.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.FloorMap;

public sealed class DeviceStatusStore(IList<FloorMapSeatViewModel> seats)
{
    public bool Apply(DeviceStatusChangedDto status)
    {
        var seat = seats.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, status.MachineName, StringComparison.OrdinalIgnoreCase));

        if (seat is null)
        {
            return false;
        }

        seat.ApplyDeviceState(status.IsOnline, status.IsLocked);
        return true;
    }
}
```

- [ ] **Step 6: Add ApplyDeviceStatus to MainWindowViewModel**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\FloorMap\MainWindowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.FloorMap;

public sealed class MainWindowViewModel
{
    private readonly DeviceStatusStore deviceStatusStore;

    public MainWindowViewModel()
    {
        Seats =
        [
            new FloorMapSeatViewModel("PC-001", "Main Hall", "Free"),
            new FloorMapSeatViewModel("PC-002", "Main Hall", "Locked")
        ];

        deviceStatusStore = new DeviceStatusStore(Seats);
    }

    public string Title => "AFK4 Operator";

    public ObservableCollection<FloorMapSeatViewModel> Seats { get; }

    public bool ApplyDeviceStatus(DeviceStatusChangedDto status)
    {
        return deviceStatusStore.Apply(status);
    }
}
```

- [ ] **Step 7: Add Operator realtime client**

Create `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\Realtime\OperatorRealtimeClient.cs`:

```csharp
using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;

namespace AFK4.Operator.App.Realtime;

public sealed class OperatorRealtimeClient : IAsyncDisposable
{
    private readonly MainWindowViewModel viewModel;
    private readonly HubConnection connection;

    public OperatorRealtimeClient(MainWindowViewModel viewModel, Uri hubUrl)
    {
        this.viewModel = viewModel;
        connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        connection.On<DeviceStatusChangedDto>(DeviceRealtimeEvents.DeviceStatusChanged, ApplyDeviceStatus);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    private void ApplyDeviceStatus(DeviceStatusChangedDto status)
    {
        viewModel.ApplyDeviceStatus(status);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}
```

- [ ] **Step 8: Wire Operator realtime client in MainWindow**

Replace `D:\afk4.net\.worktrees\realtime-device-channel\src\AFK4.Operator.App\MainWindow.xaml.cs`:

```csharp
using System.Windows;
using AFK4.Operator.App.FloorMap;
using AFK4.Operator.App.Realtime;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();
    private OperatorRealtimeClient? realtimeClient;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        realtimeClient = new OperatorRealtimeClient(viewModel, new Uri("http://localhost:5074/hubs/devices"));
        await realtimeClient.StartAsync(CancellationToken.None);
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        if (realtimeClient is not null)
        {
            await realtimeClient.DisposeAsync();
        }
    }
}
```

- [ ] **Step 9: Run Operator tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter DeviceStatusStoreTests
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 10: Commit Operator realtime state**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator realtime floor map state"
```

Expected:

```text
[feature/realtime-device-channel ...] feat: add operator realtime floor map state
```

## Task 5: Full Verification And Progress Documentation

**Files:**

- Modify: `D:\afk4.net\.worktrees\realtime-device-channel\docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Run full restore, build, and tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' restore AFK4.sln
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
Build succeeded.
Failed: 0
```

- [ ] **Step 2: Start backend for live smoke test**

Run in PowerShell session A:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Expected:

```text
Now listening on: http://localhost:5074
Application started. Press Ctrl+C to shut down.
```

- [ ] **Step 3: Verify command endpoint**

Run in PowerShell session B:

```powershell
$deviceId = 'd76eff15-9cf9-4c30-a6d4-c05fd215793f'
$body = @{
    type = 'lock'
    payload = @{
        reason = 'operator-smoke-test'
    }
} | ConvertTo-Json
Invoke-RestMethod "http://localhost:5074/api/devices/$deviceId/commands" -Method Post -ContentType 'application/json' -Body $body
```

Expected:

```text
commandId    : ...
type         : lock
createdAtUtc : ...
payload      : @{reason=operator-smoke-test}
```

- [ ] **Step 4: Verify Agent connects and acknowledges command**

Run in PowerShell session C:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Agent.Service/AFK4.Agent.Service.csproj
```

Expected Agent log lines:

```text
Realtime device channel connected for d76eff15-9cf9-4c30-a6d4-c05fd215793f.
Heartbeat sent for d76eff15-9cf9-4c30-a6d4-c05fd215793f.
Command ... acknowledged as Accepted.
```

If the command was sent before the Agent connected, send the command endpoint request from Step 3 again after the Agent log shows the realtime connection.

- [ ] **Step 5: Verify Operator App receives status updates**

Run in PowerShell session D:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Operator.App/AFK4.Operator.App.csproj
```

Then keep the Agent running until at least one heartbeat is posted.

Expected:

```text
The Operator App starts.
The PC-001 or PC-002 floor-map seat state can change from a SignalR deviceStatusChanged event.
```

The automated proof for this behavior is `DeviceStatusStoreTests`; the WPF smoke check confirms the runtime subscription does not fail during startup.

- [ ] **Step 6: Update progress log**

Modify `D:\afk4.net\.worktrees\realtime-device-channel\docs\progress\2026-05-12-vertical-slice-progress.md`.

Replace the current `## Recommended Next Work` section with:

```markdown
## Recommended Next Work

1. Complete and review `docs/superpowers/plans/2026-05-12-afk4-realtime-device-channel.md`.
2. After realtime device channel verification, create a focused plan for device enrollment, device credentials, and command status persistence.
3. Do not jump into billing, POS, updates, identity, or Windows enforcement before the realtime device channel and device identity foundations are resolved unless explicitly reprioritized.
```

Add this subsection under `## Known Deviations And Adaptations`:

```markdown
### Realtime Device Channel Follow-Up

The next focused plan is `docs/superpowers/plans/2026-05-12-afk4-realtime-device-channel.md`.
It keeps the HTTP heartbeat for compatibility while adding Agent SignalR
registration, backend command dispatch, Agent command acknowledgements, and
Operator App realtime floor-map status updates.
```

- [ ] **Step 7: Commit verification documentation**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: update progress for realtime device channel"
```

Expected:

```text
[feature/realtime-device-channel ...] docs: update progress for realtime device channel
```

## Plan Self-Review

Spec coverage:

- Architecture requirement for outgoing Agent SignalR/WebSocket communication is covered by Task 3.
- Backend SignalR device command channel is covered by Task 2.
- Operator realtime floor-map state updates are covered by Task 4.
- Shared contracts and stable event names are covered by Task 1.
- Current HTTP heartbeat compatibility and known deviation tracking are covered by Task 5.

Deferred from this plan:

- Device authentication and enrollment credentials.
- PostgreSQL persistence for device state, command log, and command result status.
- Real lock/unlock enforcement in the Agent.
- Operator command UI.
- RBAC and audit for command dispatch.
- Reconnect reconciliation and signed leases.

Placeholder scan:

- The plan contains no incomplete-marker placeholders or undefined later-fill implementation steps.
- Each code-changing step includes exact file paths and concrete code.
- Each task includes a targeted verification command and commit command.

Type consistency:

- Shared constants are named `DeviceRealtimeEvents` and `DeviceRealtimeMethods` across backend, Agent, and Operator code.
- The hub methods are `RegisterDeviceAsync` and `ReportCommandResultAsync`, matching `DeviceRealtimeMethods`.
- The command event is `deviceCommand`, matching `DeviceRealtimeEvents.DeviceCommand`.
- The status event remains `deviceStatusChanged`, matching the existing vertical slice event and the new shared constant.
