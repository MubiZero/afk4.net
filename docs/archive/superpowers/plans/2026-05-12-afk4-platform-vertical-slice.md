# AFK4 Platform Vertical Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first testable vertical slice of AFK4: .NET solution scaffold, modular backend foundation, shared contracts, WPF Operator App shell, Windows Agent Service skeleton, and realtime device heartbeat flow.

**Architecture:** Start with a .NET 10 modular monolith backend and separate Windows clients. The first slice keeps business logic minimal but establishes project boundaries, contracts, tests, and a cloud-to-agent-to-operator status loop. PostgreSQL, ledger, POS, updater, and advanced Windows control get separate follow-up plans after this foundation compiles and runs.

**Tech Stack:** .NET 10 LTS, ASP.NET Core Minimal APIs, SignalR, WPF + MVVM, Worker Service, xUnit, PostgreSQL-ready project layout.

---

## Scope

This plan implements only the first platform foundation. It does not implement billing, POS, full session lifecycle, auto-updates, Windows kiosk policy enforcement, or database persistence. Those modules need their own plans after this vertical slice is green.

## Prerequisites

Use .NET 10 LTS because it is the current LTS line for the project timeline. The repo pins SDK `10.0.203` with feature-band roll-forward. If a newer .NET 10 SDK is installed, it is acceptable through `rollForward`.

Required local tools:

- Git for Windows
- .NET 10 SDK
- PowerShell

The current repository root is `D:\afk4.net`.

## File Structure

Create this structure:

```text
D:\afk4.net\
  .editorconfig
  .gitignore
  Directory.Build.props
  global.json
  README.md
  AFK4.sln
  docs\superpowers\specs\2026-05-12-afk4-platform-architecture-design.md
  docs\superpowers\plans\2026-05-12-afk4-platform-vertical-slice.md
  src\
    AFK4.BuildingBlocks\
    AFK4.Shared.Contracts\
    AFK4.Platform.Api\
    AFK4.Agent.Service\
    AFK4.Operator.App\
    AFK4.Player.Shell\
  tests\
    AFK4.BuildingBlocks.Tests\
    AFK4.Shared.Contracts.Tests\
    AFK4.Platform.Api.Tests\
    AFK4.Agent.Service.Tests\
    AFK4.Operator.App.Tests\
```

Responsibility boundaries:

- `AFK4.BuildingBlocks`: domain primitives and shared low-level types with no application-specific dependencies.
- `AFK4.Shared.Contracts`: DTOs shared by backend, agent, operator app, and shell.
- `AFK4.Platform.Api`: ASP.NET Core modular monolith host for the first slice.
- `AFK4.Agent.Service`: Windows Worker Service skeleton that sends heartbeats.
- `AFK4.Operator.App`: WPF operator shell with initial floor map view.
- `AFK4.Player.Shell`: WPF player shell skeleton for future lock and launcher work.
- `tests/*`: unit and integration tests matching the production projects.

## Task 1: Toolchain And Repository Baseline

**Files:**

- Create: `D:\afk4.net\global.json`
- Create: `D:\afk4.net\Directory.Build.props`
- Create: `D:\afk4.net\.editorconfig`
- Create: `D:\afk4.net\.gitignore`
- Create: `D:\afk4.net\README.md`

- [ ] **Step 1: Verify Git identity**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' config user.name
& 'C:\Program Files\Git\cmd\git.exe' config user.email
```

Expected:

```text
MubiZero
mukhamedov044@gmail.com
```

- [ ] **Step 2: Verify .NET 10 SDK**

Run:

```powershell
dotnet --info
```

If the current PowerShell process has not refreshed `PATH`, this may return:

```text
dotnet : The term 'dotnet' is not recognized
```

In that case, verify the installed SDK by full path:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
```

Expected on this machine:

```text
10.0.203 [C:\Program Files\dotnet\sdk]
```

If the full path does not exist, install .NET 10 SDK:

```powershell
winget install Microsoft.DotNet.SDK.10 --source winget
```

Open a new PowerShell session after installation so `dotnet` is available through `PATH`.

Final expected result:

```text
10.0.203 [C:\Program Files\dotnet\sdk]
```

- [ ] **Step 3: Add SDK pin**

Create `D:\afk4.net\global.json`:

```json
{
  "sdk": {
    "version": "10.0.203",
    "rollForward": "latestFeature"
  }
}
```

- [ ] **Step 4: Add shared MSBuild defaults**

Create `D:\afk4.net\Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>14.0</LangVersion>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>
</Project>
```

- [ ] **Step 5: Add editor configuration**

Create `D:\afk4.net\.editorconfig`:

```editorconfig
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,csproj,xaml}]
indent_style = space
indent_size = 4

[*.md]
trim_trailing_whitespace = false
```

- [ ] **Step 6: Add .NET gitignore**

Create `D:\afk4.net\.gitignore`:

```gitignore
.vs/
.idea/
.vscode/
bin/
obj/
TestResults/
*.user
*.suo
*.nupkg
*.snupkg
artifacts/
.superpowers/
```

- [ ] **Step 7: Add README**

Create `D:\afk4.net\README.md`:

```markdown
# AFK4

AFK4 is a cloud-first SaaS platform for managing Windows-based computer clubs.

Architecture baseline:

- ASP.NET Core modular monolith backend
- PostgreSQL source-of-truth database in production plans
- WPF Operator App
- Windows Agent Service
- WPF Player Shell
- SignalR realtime channel

Start with the architecture spec:

- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
```

- [ ] **Step 8: Commit baseline**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add global.json Directory.Build.props .editorconfig .gitignore README.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "chore: add repository baseline"
```

Expected:

```text
[main ...] chore: add repository baseline
```

## Task 2: Solution And Project Scaffold

**Files:**

- Create: `D:\afk4.net\AFK4.sln`
- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\AFK4.BuildingBlocks.csproj`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\AFK4.Shared.Contracts.csproj`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\AFK4.Platform.Api.csproj`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\AFK4.Agent.Service.csproj`
- Create: `D:\afk4.net\src\AFK4.Operator.App\AFK4.Operator.App.csproj`
- Create: `D:\afk4.net\src\AFK4.Player.Shell\AFK4.Player.Shell.csproj`
- Create: `D:\afk4.net\tests\AFK4.BuildingBlocks.Tests\AFK4.BuildingBlocks.Tests.csproj`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj`
- Create: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj`
- Create: `D:\afk4.net\tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n AFK4
dotnet new classlib -n AFK4.BuildingBlocks -o src/AFK4.BuildingBlocks -f net10.0
dotnet new classlib -n AFK4.Shared.Contracts -o src/AFK4.Shared.Contracts -f net10.0
dotnet new webapi -n AFK4.Platform.Api -o src/AFK4.Platform.Api -f net10.0
dotnet new worker -n AFK4.Agent.Service -o src/AFK4.Agent.Service -f net10.0
dotnet new wpf -n AFK4.Operator.App -o src/AFK4.Operator.App -f net10.0-windows
dotnet new wpf -n AFK4.Player.Shell -o src/AFK4.Player.Shell -f net10.0-windows
dotnet new xunit -n AFK4.BuildingBlocks.Tests -o tests/AFK4.BuildingBlocks.Tests -f net10.0
dotnet new xunit -n AFK4.Shared.Contracts.Tests -o tests/AFK4.Shared.Contracts.Tests -f net10.0
dotnet new xunit -n AFK4.Platform.Api.Tests -o tests/AFK4.Platform.Api.Tests -f net10.0
dotnet new xunit -n AFK4.Agent.Service.Tests -o tests/AFK4.Agent.Service.Tests -f net10.0
dotnet new xunit -n AFK4.Operator.App.Tests -o tests/AFK4.Operator.App.Tests -f net10.0-windows
```

Expected:

```text
The template ... was created successfully.
```

- [ ] **Step 2: Add projects to solution**

Run:

```powershell
dotnet sln AFK4.sln add src/AFK4.BuildingBlocks/AFK4.BuildingBlocks.csproj
dotnet sln AFK4.sln add src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj
dotnet sln AFK4.sln add src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
dotnet sln AFK4.sln add src/AFK4.Agent.Service/AFK4.Agent.Service.csproj
dotnet sln AFK4.sln add src/AFK4.Operator.App/AFK4.Operator.App.csproj
dotnet sln AFK4.sln add src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
dotnet sln AFK4.sln add tests/AFK4.BuildingBlocks.Tests/AFK4.BuildingBlocks.Tests.csproj
dotnet sln AFK4.sln add tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj
dotnet sln AFK4.sln add tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
dotnet sln AFK4.sln add tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj
dotnet sln AFK4.sln add tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj
```

Expected:

```text
Project ... added to the solution.
```

- [ ] **Step 3: Add project references**

Run:

```powershell
dotnet add src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj reference src/AFK4.BuildingBlocks/AFK4.BuildingBlocks.csproj
dotnet add src/AFK4.Platform.Api/AFK4.Platform.Api.csproj reference src/AFK4.BuildingBlocks/AFK4.BuildingBlocks.csproj
dotnet add src/AFK4.Platform.Api/AFK4.Platform.Api.csproj reference src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj
dotnet add src/AFK4.Agent.Service/AFK4.Agent.Service.csproj reference src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj
dotnet add src/AFK4.Operator.App/AFK4.Operator.App.csproj reference src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj
dotnet add src/AFK4.Player.Shell/AFK4.Player.Shell.csproj reference src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj
dotnet add tests/AFK4.BuildingBlocks.Tests/AFK4.BuildingBlocks.Tests.csproj reference src/AFK4.BuildingBlocks/AFK4.BuildingBlocks.csproj
dotnet add tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj reference src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj
dotnet add tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj reference src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
dotnet add tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj reference src/AFK4.Agent.Service/AFK4.Agent.Service.csproj
dotnet add tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj reference src/AFK4.Operator.App/AFK4.Operator.App.csproj
```

Expected:

```text
Reference ... added to the project.
```

- [ ] **Step 4: Add API integration test package**

Run:

```powershell
dotnet add tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

Expected:

```text
PackageReference for package 'Microsoft.AspNetCore.Mvc.Testing' added
```

- [ ] **Step 5: Build scaffold**

Run:

```powershell
dotnet build AFK4.sln
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 6: Commit scaffold**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add AFK4.sln src tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "chore: scaffold dotnet solution"
```

Expected:

```text
[main ...] chore: scaffold dotnet solution
```

## Task 3: Building Blocks Domain Primitives

**Files:**

- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\OrganizationId.cs`
- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\BranchId.cs`
- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\ZoneId.cs`
- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\SeatId.cs`
- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\DeviceId.cs`
- Create: `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\SessionId.cs`
- Create: `D:\afk4.net\tests\AFK4.BuildingBlocks.Tests\Ids\StrongIdTests.cs`

- [ ] **Step 1: Write failing ID tests**

Create `D:\afk4.net\tests\AFK4.BuildingBlocks.Tests\Ids\StrongIdTests.cs`:

```csharp
using AFK4.BuildingBlocks.Ids;

namespace AFK4.BuildingBlocks.Tests.Ids;

public sealed class StrongIdTests
{
    [Fact]
    public void OrganizationId_New_CreatesNonEmptyValue()
    {
        var id = OrganizationId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal(id.Value.ToString("D"), id.ToString());
    }

    [Fact]
    public void DeviceId_From_PreservesValue()
    {
        var value = Guid.Parse("9f3adbd3-957e-4dc8-8d34-a6bfa56b9275");

        var id = DeviceId.From(value);

        Assert.Equal(value, id.Value);
        Assert.Equal("9f3adbd3-957e-4dc8-8d34-a6bfa56b9275", id.ToString());
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/AFK4.BuildingBlocks.Tests/AFK4.BuildingBlocks.Tests.csproj --filter StrongIdTests
```

Expected:

```text
error CS0246: The type or namespace name 'AFK4' could not be found
```

- [ ] **Step 3: Implement strongly typed IDs**

Create `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\OrganizationId.cs`:

```csharp
namespace AFK4.BuildingBlocks.Ids;

public readonly record struct OrganizationId(Guid Value)
{
    public static OrganizationId New() => new(Guid.NewGuid());

    public static OrganizationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
```

Create `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\BranchId.cs`:

```csharp
namespace AFK4.BuildingBlocks.Ids;

public readonly record struct BranchId(Guid Value)
{
    public static BranchId New() => new(Guid.NewGuid());

    public static BranchId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
```

Create `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\ZoneId.cs`:

```csharp
namespace AFK4.BuildingBlocks.Ids;

public readonly record struct ZoneId(Guid Value)
{
    public static ZoneId New() => new(Guid.NewGuid());

    public static ZoneId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
```

Create `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\SeatId.cs`:

```csharp
namespace AFK4.BuildingBlocks.Ids;

public readonly record struct SeatId(Guid Value)
{
    public static SeatId New() => new(Guid.NewGuid());

    public static SeatId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
```

Create `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\DeviceId.cs`:

```csharp
namespace AFK4.BuildingBlocks.Ids;

public readonly record struct DeviceId(Guid Value)
{
    public static DeviceId New() => new(Guid.NewGuid());

    public static DeviceId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
```

Create `D:\afk4.net\src\AFK4.BuildingBlocks\Ids\SessionId.cs`:

```csharp
namespace AFK4.BuildingBlocks.Ids;

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());

    public static SessionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
```

- [ ] **Step 4: Run tests and verify pass**

Run:

```powershell
dotnet test tests/AFK4.BuildingBlocks.Tests/AFK4.BuildingBlocks.Tests.csproj --filter StrongIdTests
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 5: Commit domain primitives**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.BuildingBlocks tests/AFK4.BuildingBlocks.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add domain id primitives"
```

Expected:

```text
[main ...] feat: add domain id primitives
```

## Task 4: Shared Contracts

**Files:**

- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceHeartbeatRequest.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceHeartbeatResponse.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceCommandDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceStatusChangedDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\FloorMap\FloorMapDto.cs`
- Create: `D:\afk4.net\src\AFK4.Shared.Contracts\FloorMap\SeatStatusDto.cs`
- Create: `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\ContractSerializationTests.cs`

- [ ] **Step 1: Write failing serialization tests**

Create `D:\afk4.net\tests\AFK4.Shared.Contracts.Tests\ContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ContractSerializationTests
{
    [Fact]
    public void DeviceHeartbeatRequest_RoundTripsThroughJson()
    {
        var request = new DeviceHeartbeatRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            IsLocked: true);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<DeviceHeartbeatRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.True(copy.IsLocked);
    }

    [Fact]
    public void FloorMapDto_ContainsSeatStatuses()
    {
        var seat = new SeatStatusDto(
            SeatId: Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414"),
            SeatName: "PC-001",
            ZoneName: "Main Hall",
            State: "Free",
            ActiveSessionId: null,
            RemainingSeconds: null);

        var map = new FloorMapDto(
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            BranchName: "Demo Branch",
            Seats: [seat]);

        Assert.Single(map.Seats);
        Assert.Equal("Free", map.Seats[0].State);
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests
```

Expected:

```text
error CS0234: The type or namespace name 'Contracts' does not exist
```

- [ ] **Step 3: Implement device contracts**

Create `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceHeartbeatRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset ObservedAtUtc,
    bool IsLocked);
```

Create `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceCommandDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceCommandDto(
    Guid CommandId,
    string Type,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Payload);
```

Create `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceHeartbeatResponse.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatResponse(
    DateTimeOffset ServerTimeUtc,
    int HeartbeatIntervalSeconds,
    IReadOnlyList<DeviceCommandDto> Commands);
```

Create `D:\afk4.net\src\AFK4.Shared.Contracts\Devices\DeviceStatusChangedDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceStatusChangedDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    bool IsOnline,
    bool IsLocked,
    DateTimeOffset ObservedAtUtc);
```

- [ ] **Step 4: Implement floor map contracts**

Create `D:\afk4.net\src\AFK4.Shared.Contracts\FloorMap\SeatStatusDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.FloorMap;

public sealed record SeatStatusDto(
    Guid SeatId,
    string SeatName,
    string ZoneName,
    string State,
    Guid? ActiveSessionId,
    int? RemainingSeconds);
```

Create `D:\afk4.net\src\AFK4.Shared.Contracts\FloorMap\FloorMapDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapDto(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<SeatStatusDto> Seats);
```

- [ ] **Step 5: Run contract tests**

Run:

```powershell
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 6: Commit contracts**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts tests/AFK4.Shared.Contracts.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add shared platform contracts"
```

Expected:

```text
[main ...] feat: add shared platform contracts
```

## Task 5: Platform API Health And Floor Map

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\IFloorMapReadService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\InMemoryFloorMapReadService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\HealthEndpointTests.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\FloorMapEndpointTests.cs`

- [ ] **Step 1: Write failing API tests**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\HealthEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }

    private sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);
}
```

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\FloorMapEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.FloorMap;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class FloorMapEndpointTests
{
    [Fact]
    public async Task FloorMap_ReturnsInitialSeatCards()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/floor-map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await response.Content.ReadFromJsonAsync<FloorMapDto>();
        Assert.NotNull(map);
        Assert.Equal("Demo Branch", map.BranchName);
        Assert.Contains(map.Seats, seat => seat.SeatName == "PC-001" && seat.State == "Free");
    }
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "HealthEndpointTests|FloorMapEndpointTests"
```

Expected:

```text
error CS0122: 'Program' is inaccessible due to its protection level
```

- [ ] **Step 3: Implement floor map service**

Create `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\IFloorMapReadService.cs`:

```csharp
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public interface IFloorMapReadService
{
    FloorMapDto GetFloorMap(Guid branchId);
}
```

Create `D:\afk4.net\src\AFK4.Platform.Api\FloorMap\InMemoryFloorMapReadService.cs`:

```csharp
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Platform.Api.FloorMap;

public sealed class InMemoryFloorMapReadService : IFloorMapReadService
{
    public FloorMapDto GetFloorMap(Guid branchId)
    {
        return new FloorMapDto(
            BranchId: branchId,
            BranchName: "Demo Branch",
            Seats:
            [
                new SeatStatusDto(
                    SeatId: Guid.Parse("e5edae8b-a833-4d92-ad8c-5864376d0414"),
                    SeatName: "PC-001",
                    ZoneName: "Main Hall",
                    State: "Free",
                    ActiveSessionId: null,
                    RemainingSeconds: null),
                new SeatStatusDto(
                    SeatId: Guid.Parse("ad63d1ef-8477-476b-a21c-06916dd5ad76"),
                    SeatName: "PC-002",
                    ZoneName: "Main Hall",
                    State: "Locked",
                    ActiveSessionId: null,
                    RemainingSeconds: null)
            ]);
    }
}
```

- [ ] **Step 4: Replace API Program**

Replace `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`:

```csharp
using AFK4.Platform.Api.FloorMap;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
```

- [ ] **Step 5: Run API tests**

Run:

```powershell
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "HealthEndpointTests|FloorMapEndpointTests"
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 6: Commit API health and floor map**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add api health and floor map endpoints"
```

Expected:

```text
[main ...] feat: add api health and floor map endpoints
```

## Task 6: Device Heartbeat API And SignalR Hub

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Platform.Api\Program.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Devices\DeviceHub.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Devices\IDeviceHeartbeatService.cs`
- Create: `D:\afk4.net\src\AFK4.Platform.Api\Devices\InMemoryDeviceHeartbeatService.cs`
- Create: `D:\afk4.net\tests\AFK4.Platform.Api.Tests\DeviceHeartbeatEndpointTests.cs`

- [ ] **Step 1: Write failing heartbeat endpoint test**

Create `D:\afk4.net\tests\AFK4.Platform.Api.Tests\DeviceHeartbeatEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceHeartbeatEndpointTests
{
    [Fact]
    public async Task DeviceHeartbeat_ReturnsServerTimeAndInterval()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var request = new DeviceHeartbeatRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: deviceId,
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            IsLocked: true);

        var response = await client.PostAsJsonAsync($"/api/devices/{deviceId}/heartbeat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body.HeartbeatIntervalSeconds);
        Assert.Empty(body.Commands);
    }
}
```

- [ ] **Step 2: Run test and verify failure**

Run:

```powershell
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceHeartbeatEndpointTests
```

Expected:

```text
Assert.Equal() Failure
Expected: OK
Actual:   NotFound
```

- [ ] **Step 3: Add device hub and heartbeat service**

Create `D:\afk4.net\src\AFK4.Platform.Api\Devices\DeviceHub.cs`:

```csharp
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHub : Hub
{
}
```

Create `D:\afk4.net\src\AFK4.Platform.Api\Devices\IDeviceHeartbeatService.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceHeartbeatService
{
    Task<DeviceHeartbeatResponse> RecordHeartbeatAsync(Guid deviceId, DeviceHeartbeatRequest request, CancellationToken cancellationToken);
}
```

Create `D:\afk4.net\src\AFK4.Platform.Api\Devices\InMemoryDeviceHeartbeatService.cs`:

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

        await hubContext.Clients.All.SendAsync("deviceStatusChanged", status, cancellationToken);

        return new DeviceHeartbeatResponse(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            HeartbeatIntervalSeconds: 10,
            Commands: []);
    }
}
```

- [ ] **Step 4: Wire heartbeat endpoint and SignalR**

Modify `D:\afk4.net\src\AFK4.Platform.Api\Program.cs` to include the device service:

```csharp
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.FloorMap;
using AFK4.Shared.Contracts.Devices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<IFloorMapReadService, InMemoryFloorMapReadService>();
builder.Services.AddSingleton<IDeviceHeartbeatService, InMemoryDeviceHeartbeatService>();

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
        return Results.BadRequest(new { error = "Route device id does not match request device id." });
    }

    var response = await heartbeatService.RecordHeartbeatAsync(deviceId, request, cancellationToken);
    return Results.Ok(response);
});

app.MapHub<DeviceHub>("/hubs/devices");

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
```

- [ ] **Step 5: Run API tests**

Run:

```powershell
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 6: Commit heartbeat API**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add device heartbeat endpoint"
```

Expected:

```text
[main ...] feat: add device heartbeat endpoint
```

## Task 7: Agent Service Heartbeat Skeleton

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Agent.Service\Program.cs`
- Modify: `D:\afk4.net\src\AFK4.Agent.Service\Worker.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\AgentOptions.cs`
- Create: `D:\afk4.net\src\AFK4.Agent.Service\HeartbeatPayloadFactory.cs`
- Create: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\HeartbeatPayloadFactoryTests.cs`

- [ ] **Step 1: Write failing heartbeat payload test**

Create `D:\afk4.net\tests\AFK4.Agent.Service.Tests\HeartbeatPayloadFactoryTests.cs`:

```csharp
using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

public sealed class HeartbeatPayloadFactoryTests
{
    [Fact]
    public void Create_BuildsHeartbeatForConfiguredDevice()
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

        var request = HeartbeatPayloadFactory.Create(options, isLocked: true, DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        Assert.Equal(options.OrganizationId, request.OrganizationId);
        Assert.Equal(options.BranchId, request.BranchId);
        Assert.Equal(options.DeviceId, request.DeviceId);
        Assert.Equal("PC-001", request.MachineName);
        Assert.True(request.IsLocked);
    }
}
```

- [ ] **Step 2: Run test and verify failure**

Run:

```powershell
dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter HeartbeatPayloadFactoryTests
```

Expected:

```text
error CS0246: The type or namespace name 'AgentOptions' could not be found
```

- [ ] **Step 3: Implement agent options and payload factory**

Create `D:\afk4.net\src\AFK4.Agent.Service\AgentOptions.cs`:

```csharp
namespace AFK4.Agent.Service;

public sealed class AgentOptions
{
    public Uri PlatformBaseUrl { get; init; } = new("http://localhost:5000");

    public Guid OrganizationId { get; init; }

    public Guid BranchId { get; init; }

    public Guid DeviceId { get; init; }

    public string MachineName { get; init; } = Environment.MachineName;

    public string AgentVersion { get; init; } = "0.1.0";

    public string ShellVersion { get; init; } = "0.1.0";
}
```

Create `D:\afk4.net\src\AFK4.Agent.Service\HeartbeatPayloadFactory.cs`:

```csharp
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public static class HeartbeatPayloadFactory
{
    public static DeviceHeartbeatRequest Create(AgentOptions options, bool isLocked, DateTimeOffset observedAtUtc)
    {
        return new DeviceHeartbeatRequest(
            OrganizationId: options.OrganizationId,
            BranchId: options.BranchId,
            DeviceId: options.DeviceId,
            MachineName: options.MachineName,
            AgentVersion: options.AgentVersion,
            ShellVersion: options.ShellVersion,
            ObservedAtUtc: observedAtUtc,
            IsLocked: isLocked);
    }
}
```

- [ ] **Step 4: Wire Worker heartbeat loop**

Replace `D:\afk4.net\src\AFK4.Agent.Service\Program.cs`:

```csharp
using AFK4.Agent.Service;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient("platform");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

Replace `D:\afk4.net\src\AFK4.Agent.Service\Worker.cs`:

```csharp
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
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
}
```

- [ ] **Step 5: Run agent tests**

Run:

```powershell
dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter HeartbeatPayloadFactoryTests
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 6: Commit Agent heartbeat skeleton**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Agent.Service tests/AFK4.Agent.Service.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add agent heartbeat skeleton"
```

Expected:

```text
[main ...] feat: add agent heartbeat skeleton
```

## Task 8: Operator App Floor Map Shell

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml`
- Modify: `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\FloorMapSeatViewModel.cs`
- Create: `D:\afk4.net\src\AFK4.Operator.App\FloorMap\MainWindowViewModel.cs`
- Create: `D:\afk4.net\tests\AFK4.Operator.App.Tests\MainWindowViewModelTests.cs`

- [ ] **Step 1: Write failing ViewModel test**

Create `D:\afk4.net\tests\AFK4.Operator.App.Tests\MainWindowViewModelTests.cs`:

```csharp
using AFK4.Operator.App.FloorMap;

namespace AFK4.Operator.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_CreatesInitialSeatCards()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal("AFK4 Operator", viewModel.Title);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-001" && seat.State == "Free");
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-002" && seat.State == "Locked");
    }
}
```

- [ ] **Step 2: Run test and verify failure**

Run:

```powershell
dotnet test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter MainWindowViewModelTests
```

Expected:

```text
error CS0234: The type or namespace name 'FloorMap' does not exist
```

- [ ] **Step 3: Implement floor map ViewModels**

Create `D:\afk4.net\src\AFK4.Operator.App\FloorMap\FloorMapSeatViewModel.cs`:

```csharp
namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapSeatViewModel
{
    public FloorMapSeatViewModel(string name, string zone, string state)
    {
        Name = name;
        Zone = zone;
        State = state;
    }

    public string Name { get; }

    public string Zone { get; }

    public string State { get; }
}
```

Create `D:\afk4.net\src\AFK4.Operator.App\FloorMap\MainWindowViewModel.cs`:

```csharp
using System.Collections.ObjectModel;

namespace AFK4.Operator.App.FloorMap;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        Seats =
        [
            new FloorMapSeatViewModel("PC-001", "Main Hall", "Free"),
            new FloorMapSeatViewModel("PC-002", "Main Hall", "Locked")
        ];
    }

    public string Title => "AFK4 Operator";

    public ObservableCollection<FloorMapSeatViewModel> Seats { get; }
}
```

- [ ] **Step 4: Bind MainWindow to ViewModel**

Replace `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml`:

```xml
<Window x:Class="AFK4.Operator.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{Binding Title}"
        Height="720"
        Width="1180"
        MinHeight="640"
        MinWidth="960"
        Background="#F3F5F7">
    <DockPanel>
        <Border DockPanel.Dock="Top" Background="#111827" Padding="18,12">
            <TextBlock Text="{Binding Title}" Foreground="White" FontSize="20" FontWeight="SemiBold" />
        </Border>

        <Grid Margin="18">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="320" />
            </Grid.ColumnDefinitions>

            <ItemsControl ItemsSource="{Binding Seats}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Width="160" Height="112" Margin="0,0,12,12" Background="White" BorderBrush="#D8DEE9" BorderThickness="1" CornerRadius="6" Padding="12">
                            <StackPanel>
                                <TextBlock Text="{Binding Name}" FontSize="18" FontWeight="SemiBold" Foreground="#111827" />
                                <TextBlock Text="{Binding Zone}" Margin="0,6,0,0" Foreground="#4B5563" />
                                <TextBlock Text="{Binding State}" Margin="0,12,0,0" FontWeight="SemiBold" Foreground="#047857" />
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <Border Grid.Column="1" Background="White" BorderBrush="#D8DEE9" BorderThickness="1" CornerRadius="6" Padding="16">
                <StackPanel>
                    <TextBlock Text="Selected Seat" FontSize="18" FontWeight="SemiBold" Foreground="#111827" />
                    <TextBlock Text="Select a PC to show session, player, billing, and quick actions." Margin="0,10,0,0" TextWrapping="Wrap" Foreground="#4B5563" />
                </StackPanel>
            </Border>
        </Grid>
    </DockPanel>
</Window>
```

Replace `D:\afk4.net\src\AFK4.Operator.App\MainWindow.xaml.cs`:

```csharp
using System.Windows;
using AFK4.Operator.App.FloorMap;

namespace AFK4.Operator.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
```

- [ ] **Step 5: Run Operator tests**

Run:

```powershell
dotnet test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter MainWindowViewModelTests
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 6: Commit Operator App shell**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add operator floor map shell"
```

Expected:

```text
[main ...] feat: add operator floor map shell
```

## Task 9: Player Shell Skeleton

**Files:**

- Modify: `D:\afk4.net\src\AFK4.Player.Shell\MainWindow.xaml`
- Modify: `D:\afk4.net\src\AFK4.Player.Shell\MainWindow.xaml.cs`

- [ ] **Step 1: Replace Player Shell window**

Replace `D:\afk4.net\src\AFK4.Player.Shell\MainWindow.xaml`:

```xml
<Window x:Class="AFK4.Player.Shell.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="AFK4 Player Shell"
        WindowState="Maximized"
        WindowStyle="None"
        ResizeMode="NoResize"
        Background="#0B1220">
    <Grid>
        <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Width="520">
            <TextBlock Text="AFK4" Foreground="White" FontSize="48" FontWeight="Bold" HorizontalAlignment="Center" />
            <TextBlock Text="This PC is locked" Foreground="#D1D5DB" FontSize="24" Margin="0,24,0,0" HorizontalAlignment="Center" />
            <TextBlock Text="Start a session from the operator desk." Foreground="#9CA3AF" FontSize="16" Margin="0,12,0,0" HorizontalAlignment="Center" />
        </StackPanel>
    </Grid>
</Window>
```

Replace `D:\afk4.net\src\AFK4.Player.Shell\MainWindow.xaml.cs`:

```csharp
using System.Windows;

namespace AFK4.Player.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 2: Build Player Shell**

Run:

```powershell
dotnet build src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 3: Commit Player Shell skeleton**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Player.Shell
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add player shell skeleton"
```

Expected:

```text
[main ...] feat: add player shell skeleton
```

## Task 10: Full Solution Verification

**Files:**

- Modify: none unless verification exposes a concrete compile or test failure.

- [ ] **Step 1: Run full build**

Run:

```powershell
dotnet build AFK4.sln
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 2: Run full test suite**

Run:

```powershell
dotnet test AFK4.sln
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 3: Start backend**

Run:

```powershell
dotnet run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Expected:

```text
Now listening on: http://localhost:5074
Application started. Press Ctrl+C to shut down.
```

- [ ] **Step 4: Verify health endpoint from a second PowerShell session**

Run:

```powershell
Invoke-RestMethod http://localhost:5074/api/health
```

Expected:

```text
status serverTimeUtc
------ -------------
ok     ...
```

- [ ] **Step 5: Verify floor map endpoint**

Run:

```powershell
Invoke-RestMethod http://localhost:5074/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/floor-map
```

Expected:

```text
branchId   : acfc0212-967f-4d84-94be-9003387b09c2
branchName : Demo Branch
seats      : {@{seatId=...; seatName=PC-001; zoneName=Main Hall; state=Free; ...}, ...}
```

- [ ] **Step 6: Commit verification fixes if any were required**

If files changed during verification, run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' status --short
& 'C:\Program Files\Git\cmd\git.exe' add src tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "fix: complete vertical slice verification"
```

Expected when changes existed:

```text
[main ...] fix: complete vertical slice verification
```

Expected when no changes existed:

```text
no output from git status --short
```

## Plan Self-Review

Spec coverage:

- Cloud-first modular monolith foundation is covered by Tasks 2, 5, and 6.
- Shared contracts are covered by Task 4.
- Native WPF Operator App foundation is covered by Task 8.
- Windows Agent Service foundation is covered by Task 7.
- Player Shell foundation is covered by Task 9.
- Realtime device status foundation is covered by Task 6.
- Testing and verification are covered in every implementation task and Task 10.

Deferred from this first plan:

- PostgreSQL EF Core persistence
- identity and permission enforcement
- session lifecycle commands
- ledger and billing
- POS and inventory
- update package signing and rollout
- Windows kiosk policy enforcement
- installer packaging

These are deferred because they are separate subsystems and need focused plans after the first vertical slice builds successfully.
