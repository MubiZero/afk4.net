# Customer Shell WebView2 Pivot — Foundation (Units A+B+C) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the gaming-PC customer shell's pure-WPF UI with a thin native WebView2 host that renders a React active-session screen driven by the existing Agent.Service named pipe, with kiosk hardening, a native fallback panel, and a crash watchdog.

**Architecture:** Mirror the proven `AFK4.Operator.App` host pattern (WebView2 + virtual-host-mapped local bundle + `postMessage` bridge). The native host (`AFK4.Player.Shell`) owns the full-screen lock window, watchdog, fallback, kiosk hardening, and the narrow JS↔native bridge over the existing `NamedPipePlayerShellStateClient` / `LauncherCommandClient`. A new React app (`AFK4.Player.Shell.Web`) renders the session screen. The web never decides — it reflects pipe state and dispatches a few whitelisted actions.

**Tech Stack:** .NET 10 (net10.0-windows, WPF), Microsoft.Web.WebView2 1.0.3967.48, xUnit; Vite + React 19 + TypeScript, `bun test` + happy-dom + Testing Library; shared `@afk4/*` workspace packages.

**Scope note:** This is Plan 1 of 2. It covers Units A (native host + fallback/watchdog), B (bridge contract), C (web scaffold + active-session screen). Units D–G (login, self-service extend/top-up, content screens, packaging + WPF retirement) are a follow-up plan written after this foundation validates the bridge and asset shapes. The auth-token transport mechanism is decided in Plan 2 (it first appears in Unit D); this plan needs no token.

**Environment quirks (read before starting):**
- `bun` is at `/home/fedya/.bun/bin/bun` (not on PATH). Web tests run on Linux.
- WPF projects (`<UseWPF>true</UseWPF>`, `net10.0-windows`) **fail to build on Linux** with NETSDK1100. All `.NET` build/test steps in this plan run on the **Windows bridge**, not in the Linux dev session. Web steps run on Linux.
- The native code is deliberately split so logic lives in plain, testable classes (`*Policy`, `*Resolver`, the bridge handler) exercised by xUnit on the Windows bridge; the `WebView2`-touching window class stays thin and is verified manually on Windows.

---

## File Structure

### Unit A — native host + fallback/watchdog (`AFK4.Player.Shell`)

- Modify: `src/AFK4.Player.Shell/AFK4.Player.Shell.csproj` — add WebView2 package + WebAssets content.
- Create: `src/AFK4.Player.Shell/Web/PlayerWebAssetResolver.cs` — resolves the web bundle / dev-server URL + virtual host name.
- Create: `src/AFK4.Player.Shell/Web/WebViewWatchdogPolicy.cs` — pure policy: failure signal → action.
- Create: `src/AFK4.Player.Shell/Web/PlayerShellLockPolicy.cs` — pure fail-locked decision.
- Create: `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml` + `.xaml.cs` — the thin WebView2 host window (manual-verified on Windows).
- Modify: `src/AFK4.Player.Shell/App.xaml` + `App.xaml.cs` — launch the new window.
- Test: `tests/AFK4.Player.Shell.Tests/Web/WebViewWatchdogPolicyTests.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerShellLockPolicyTests.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerWebAssetResolverTests.cs`

### Unit B — bridge contract

- Create: `src/AFK4.Player.Shell/Web/PlayerShellWebHostBridge.cs` — native bridge handler (route + validate + serialize).
- Modify: `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs` — wire `WebMessageReceived` → bridge; push pipe state → `PostWebMessageAsJson`.
- Create: `src/AFK4.Player.Shell.Web/src/shellBridge.ts` — TS envelope transport over `window.chrome.webview`.
- Create: `src/AFK4.Player.Shell.Web/src/shellClient.ts` — typed wrappers (loadState/launch/requestOperator/pause/subscribe).
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerShellWebHostBridgeTests.cs`
- Test: `src/AFK4.Player.Shell.Web/src/shellBridge.test.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellClient.test.ts`

### Unit C — web scaffold + active-session screen (`AFK4.Player.Shell.Web`)

- Create: `src/AFK4.Player.Shell.Web/package.json`, `vite.config.ts`, `tsconfig.json`, `bunfig.toml`, `index.html`, `src/main.tsx`, `src/styles.css`, `src/test/setup.ts`.
- Modify: `package.json` (repo root) — add the new app to `workspaces`.
- Create: `src/AFK4.Player.Shell.Web/src/shellContracts.ts` — TS mirror of the Shell DTOs.
- Create: `src/AFK4.Player.Shell.Web/src/useShellBridge.ts` — hook exposing reactive session state + actions.
- Create: `src/AFK4.Player.Shell.Web/src/App.tsx` — top-level state router (locked / active / offline).
- Create: `src/AFK4.Player.Shell.Web/src/screens/ActiveSessionScreen.tsx`, `src/screens/LockedScreen.tsx`.
- Test: `src/AFK4.Player.Shell.Web/src/useShellBridge.test.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellContracts.test.ts`
- Test: `src/AFK4.Player.Shell.Web/src/screens/ActiveSessionScreen.test.tsx`

---

## Unit A — Native thin host + fallback/watchdog

### Task A1: Add WebView2 to the Player.Shell project

**Files:**
- Modify: `src/AFK4.Player.Shell/AFK4.Player.Shell.csproj`

- [ ] **Step 1: Add the WebView2 package and WebAssets content include**

Open `src/AFK4.Player.Shell/AFK4.Player.Shell.csproj` and add the package reference (same version as Operator.App) and a WebAssets content include inside the existing `<Project>`:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.3967.48" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="WebAssets\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

- [ ] **Step 2: Restore on the Windows bridge**

Run (Windows bridge): `dotnet restore src/AFK4.Player.Shell/AFK4.Player.Shell.csproj`
Expected: restore succeeds, WebView2 1.0.3967.48 resolved.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
git commit -m "build(player-shell): add WebView2 package and WebAssets content"
```

### Task A2: WebView watchdog policy (pure, testable)

**Files:**
- Create: `src/AFK4.Player.Shell/Web/WebViewWatchdogPolicy.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/WebViewWatchdogPolicyTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Player.Shell.Tests/Web/WebViewWatchdogPolicyTests.cs`:

```csharp
using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class WebViewWatchdogPolicyTests
{
    [Fact]
    public void Healthy_NoFailure_KeepsWebVisible()
    {
        var action = WebViewWatchdogPolicy.Decide(
            new WebViewHealthSignal(ProcessFailed: false, Unresponsive: false));

        Assert.False(action.ShowFallback);
        Assert.False(action.RestartWebView);
    }

    [Fact]
    public void RenderProcessFailed_ShowsFallbackAndRestarts()
    {
        var action = WebViewWatchdogPolicy.Decide(
            new WebViewHealthSignal(ProcessFailed: true, Unresponsive: false));

        Assert.True(action.ShowFallback);
        Assert.True(action.RestartWebView);
    }

    [Fact]
    public void Unresponsive_ShowsFallbackButDoesNotRestartYet()
    {
        // An unresponsive page may recover; cover the desktop but give it a chance
        // before killing the process.
        var action = WebViewWatchdogPolicy.Decide(
            new WebViewHealthSignal(ProcessFailed: false, Unresponsive: true));

        Assert.True(action.ShowFallback);
        Assert.False(action.RestartWebView);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter WebViewWatchdogPolicyTests`
Expected: FAIL — `WebViewWatchdogPolicy`/`WebViewHealthSignal` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell/Web/WebViewWatchdogPolicy.cs`:

```csharp
namespace AFK4.Player.Shell.Web;

public readonly record struct WebViewHealthSignal(bool ProcessFailed, bool Unresponsive);

public readonly record struct WebViewWatchdogAction(bool ShowFallback, bool RestartWebView);

public static class WebViewWatchdogPolicy
{
    public static WebViewWatchdogAction Decide(WebViewHealthSignal signal)
    {
        if (signal.ProcessFailed)
        {
            return new WebViewWatchdogAction(ShowFallback: true, RestartWebView: true);
        }

        if (signal.Unresponsive)
        {
            return new WebViewWatchdogAction(ShowFallback: true, RestartWebView: false);
        }

        return new WebViewWatchdogAction(ShowFallback: false, RestartWebView: false);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter WebViewWatchdogPolicyTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell/Web/WebViewWatchdogPolicy.cs tests/AFK4.Player.Shell.Tests/Web/WebViewWatchdogPolicyTests.cs
git commit -m "feat(player-shell): webview watchdog policy"
```

### Task A3: Fail-locked policy (pure, testable)

**Files:**
- Create: `src/AFK4.Player.Shell/Web/PlayerShellLockPolicy.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerShellLockPolicyTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Player.Shell.Tests/Web/PlayerShellLockPolicyTests.cs`:

```csharp
using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerShellLockPolicyTests
{
    private static PlayerShellStateDto State(string state, int? remaining = 1200) =>
        new(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: state,
            SessionId: Guid.NewGuid(),
            LeaseExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(20),
            RemainingSeconds: remaining,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "ok",
            LauncherApps: []);

    [Fact]
    public void NoState_IsLocked()
    {
        // Until the pipe delivers authoritative state, assume locked.
        Assert.True(PlayerShellLockPolicy.IsLocked(state: null));
    }

    [Fact]
    public void LockedState_IsLocked()
    {
        Assert.True(PlayerShellLockPolicy.IsLocked(State(PlayerShellStateNames.Locked)));
    }

    [Fact]
    public void ActiveState_IsNotLocked()
    {
        Assert.False(PlayerShellLockPolicy.IsLocked(State(PlayerShellStateNames.Active)));
    }

    [Fact]
    public void OfflineWithLease_IsNotLocked()
    {
        // Offline but lease still valid: keep playing.
        Assert.False(PlayerShellLockPolicy.IsLocked(State(PlayerShellStateNames.Active) with { IsOnline = false }));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter PlayerShellLockPolicyTests`
Expected: FAIL — `PlayerShellLockPolicy` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell/Web/PlayerShellLockPolicy.cs`:

```csharp
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

public static class PlayerShellLockPolicy
{
    // Fail-locked / default-deny: no authoritative state ⇒ locked.
    public static bool IsLocked(PlayerShellStateDto? state)
    {
        if (state is null)
        {
            return true;
        }

        return state.State is PlayerShellStateNames.Locked
            or PlayerShellStateNames.Offline
            or PlayerShellStateNames.Error;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter PlayerShellLockPolicyTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell/Web/PlayerShellLockPolicy.cs tests/AFK4.Player.Shell.Tests/Web/PlayerShellLockPolicyTests.cs
git commit -m "feat(player-shell): fail-locked lock policy"
```

### Task A4: Web asset resolver (virtual host + dist/dev-server resolution)

**Files:**
- Create: `src/AFK4.Player.Shell/Web/PlayerWebAssetResolver.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerWebAssetResolverTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Player.Shell.Tests/Web/PlayerWebAssetResolverTests.cs`:

```csharp
using System.IO;
using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerWebAssetResolverTests
{
    [Fact]
    public void VirtualHost_IsPlayerLocalDomain()
    {
        Assert.Equal("player.afk4.local", PlayerWebAssetResolver.LocalVirtualHost);
    }

    [Fact]
    public void DevServerUrl_WhenLoopbackHttp_IsAccepted()
    {
        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: "http://127.0.0.1:5175",
            distIndexHtmlPath: null);

        Assert.Equal(PlayerWebLaunchKind.DevServer, target.Kind);
        Assert.Equal("http://127.0.0.1:5175", target.Source);
    }

    [Fact]
    public void DevServerUrl_WhenNotLoopback_IsRejected()
    {
        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: "http://example.com",
            distIndexHtmlPath: "/repo/src/AFK4.Player.Shell.Web/dist/index.html");

        Assert.Equal(PlayerWebLaunchKind.LocalFolder, target.Kind);
    }

    [Fact]
    public void NoDevServer_UsesDistFolderViaVirtualHost()
    {
        // Build the path with the OS-native separator so the expected folder
        // matches Path.GetDirectoryName on both Windows (\) and Linux (/).
        var indexPath = Path.Combine("repo", "src", "AFK4.Player.Shell.Web", "dist", "index.html");

        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: null,
            distIndexHtmlPath: indexPath);

        Assert.Equal(PlayerWebLaunchKind.LocalFolder, target.Kind);
        Assert.Equal(Path.GetDirectoryName(indexPath), target.LocalFolderPath);
        Assert.Equal("https://player.afk4.local/index.html", target.Source);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter PlayerWebAssetResolverTests`
Expected: FAIL — types do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell/Web/PlayerWebAssetResolver.cs`:

```csharp
using System.IO;

namespace AFK4.Player.Shell.Web;

public enum PlayerWebLaunchKind
{
    DevServer,
    LocalFolder
}

public sealed record PlayerWebLaunchTarget(
    PlayerWebLaunchKind Kind,
    string Source,
    string? LocalFolderPath);

public static class PlayerWebAssetResolver
{
    public const string LocalVirtualHost = "player.afk4.local";

    public static PlayerWebLaunchTarget Resolve(string? devServerUrl, string? distIndexHtmlPath)
    {
        if (IsLoopbackHttp(devServerUrl))
        {
            return new PlayerWebLaunchTarget(PlayerWebLaunchKind.DevServer, devServerUrl!, LocalFolderPath: null);
        }

        var folder = Path.GetDirectoryName(distIndexHtmlPath)!;
        return new PlayerWebLaunchTarget(
            PlayerWebLaunchKind.LocalFolder,
            $"https://{LocalVirtualHost}/index.html",
            folder);
    }

    private static bool IsLoopbackHttp(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var schemeOk = uri.Scheme is "http" or "https";
        return schemeOk && uri.IsLoopback;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter PlayerWebAssetResolverTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell/Web/PlayerWebAssetResolver.cs tests/AFK4.Player.Shell.Tests/Web/PlayerWebAssetResolverTests.cs
git commit -m "feat(player-shell): web asset resolver with virtual host"
```

### Task A5: WebView host window (thin glue, kiosk hardening, native fallback) — manual-verified

**Files:**
- Create: `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml`
- Create: `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs`
- Modify: `src/AFK4.Player.Shell/App.xaml.cs`

> This task is WebView2 glue: it cannot be unit-tested headless. Keep it thin (it delegates all decisions to the Task A2/A3/A4 policies and, in Unit B, to the bridge). Verify manually on Windows per the checklist at the end of the task.

- [ ] **Step 1: Create the window XAML**

Create `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml`:

```xml
<Window x:Class="AFK4.Player.Shell.Web.WebViewPlayerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
        Title="AFK4.NET Player Shell"
        WindowState="Maximized"
        WindowStyle="None"
        ResizeMode="NoResize"
        Topmost="True"
        Background="#0B1220">
    <Grid>
        <wv2:WebView2 x:Name="Browser" />

        <!-- Native fallback: covers the desktop when the web layer is down. -->
        <Grid x:Name="FallbackPanel"
              Background="#0B1220"
              Visibility="Collapsed">
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
                <TextBlock x:Name="FallbackTimer"
                           Foreground="#E5E7EB"
                           FontSize="48"
                           FontWeight="SemiBold"
                           TextAlignment="Center" />
                <TextBlock x:Name="FallbackMessage"
                           Margin="0,18,0,0"
                           Foreground="#9CA3AF"
                           FontSize="18"
                           TextAlignment="Center" />
            </StackPanel>
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 2: Create the window code-behind (thin)**

Create `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Windows;
using AFK4.Player.Shell.Configuration;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Realtime;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Web.WebView2.Core;

namespace AFK4.Player.Shell.Web;

public partial class WebViewPlayerWindow : Window
{
    private readonly PlayerShellOptions options;
    private readonly IPlayerShellStateClient stateClient;
    private readonly CancellationTokenSource lifetime = new();
    private PlayerShellStateDto? latestState;

    public WebViewPlayerWindow()
        : this(
            new PlayerShellOptions
            {
                PipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_PIPE_NAME") ?? "afk4-player-shell",
                CommandPipeName = Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_COMMAND_PIPE_NAME") ?? "afk4-player-shell-commands"
            })
    {
    }

    internal WebViewPlayerWindow(PlayerShellOptions options)
    {
        this.options = options;
        stateClient = new NamedPipePlayerShellStateClient(options);
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Browser.EnsureCoreWebView2Async();
        HardenForKiosk(Browser.CoreWebView2);

        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: Environment.GetEnvironmentVariable("AFK4_PLAYER_WEB_DEV_SERVER_URL"),
            distIndexHtmlPath: ResolveDistIndexHtml());

        if (target.Kind == PlayerWebLaunchKind.LocalFolder)
        {
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                PlayerWebAssetResolver.LocalVirtualHost,
                target.LocalFolderPath!,
                CoreWebView2HostResourceAccessKind.Allow);
        }

        Browser.CoreWebView2.ProcessFailed += OnProcessFailed;
        Browser.Source = new Uri(target.Source);
    }

    private static void HardenForKiosk(CoreWebView2 core)
    {
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        var action = WebViewWatchdogPolicy.Decide(new WebViewHealthSignal(ProcessFailed: true, Unresponsive: false));
        ApplyWatchdog(action);
    }

    private void ApplyWatchdog(WebViewWatchdogAction action)
    {
        FallbackPanel.Visibility = action.ShowFallback ? Visibility.Visible : Visibility.Collapsed;
        FallbackTimer.Text = RemainingTimeFormatterText();
        FallbackMessage.Text = "Восстанавливаем соединение…";

        if (action.RestartWebView)
        {
            Browser.Reload();
            FallbackPanel.Visibility = Visibility.Collapsed;
        }
    }

    private string RemainingTimeFormatterText() =>
        Shell.RemainingTimeFormatter.Format(latestState?.RemainingSeconds);

    private static string? ResolveDistIndexHtml()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "WebAssets", "index.html");
        return File.Exists(candidate) ? candidate : candidate;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        lifetime.Cancel();
        lifetime.Dispose();
    }
}
```

> Note: `latestState`, `lifetime`, the state-listen loop, and `WebMessageReceived` wiring are completed in Unit B (Task B2). This task only proves the host renders a page and the fallback panel shows on a forced crash.

- [ ] **Step 3: Switch app startup to the new window**

In `src/AFK4.Player.Shell/App.xaml.cs`, replace the production `MainWindow` creation with the WebView window. Change the non-preview branch of `OnStartup`:

```csharp
        var window = new AFK4.Player.Shell.Web.WebViewPlayerWindow();
        window.Show();
        base.OnStartup(e);
```

Leave the `#if DEBUG --preview` branch untouched for now (it still uses the old `MainWindow`; the old WPF UI is removed in Plan 2 / Unit G).

- [ ] **Step 4: Build on the Windows bridge**

Run (Windows bridge): `dotnet build src/AFK4.Player.Shell/AFK4.Player.Shell.csproj`
Expected: build succeeds.

- [ ] **Step 5: Manual verification on Windows (no automated test possible)**

Place a minimal `WebAssets/index.html` (temporary) under the build output, run `AFK4.Player.Shell.exe`, and confirm:
1. A full-screen borderless window shows the page.
2. Dev tools (F12) and right-click context menu are disabled.
3. Killing the WebView2 renderer (Task Manager → end `msedgewebview2` child) shows the native fallback panel rather than the desktop.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs src/AFK4.Player.Shell/App.xaml.cs
git commit -m "feat(player-shell): thin WebView2 host window with kiosk hardening and native fallback"
```

---

## Unit B — Bridge contract

### Task B1: Native bridge handler (route + validate + serialize)

**Files:**
- Create: `src/AFK4.Player.Shell/Web/PlayerShellWebHostBridge.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerShellWebHostBridgeTests.cs`

The bridge handles a small whitelist of request types and serializes a response envelope. It depends only on `ILauncherCommandClient` and a getter for the latest state — no WebView2 — so it is fully unit-testable.

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Player.Shell.Tests/Web/PlayerShellWebHostBridgeTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Player.Shell.Launcher;
using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerShellWebHostBridgeTests
{
    private sealed class StubLauncher : ILauncherCommandClient
    {
        public string? LaunchedAppId { get; private set; }

        public Task<PlayerShellCommandResultDto> LaunchAsync(string appId, CancellationToken cancellationToken)
        {
            LaunchedAppId = appId;
            return Task.FromResult(new PlayerShellCommandResultDto(Guid.NewGuid(), "accepted", "launched"));
        }
    }

    private static PlayerShellWebHostBridge CreateBridge(StubLauncher launcher) =>
        new(launcher, getLatestState: () => null);

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task LaunchRequest_RoutesAppIdToLauncher()
    {
        var launcher = new StubLauncher();
        var bridge = CreateBridge(launcher);

        var request = """{"requestId":"r1","type":"launcher:launch","payload":{"appId":"cs2"}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Equal("cs2", launcher.LaunchedAppId);
        var response = Parse(responseJson!);
        Assert.Equal("r1", response.GetProperty("requestId").GetString());
        Assert.True(response.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task UnknownType_IsRejected()
    {
        var bridge = CreateBridge(new StubLauncher());

        var request = """{"requestId":"r2","type":"os:shutdown","payload":{}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        var response = Parse(responseJson!);
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("unknown_request", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LaunchRequest_MissingAppId_IsRejected()
    {
        var launcher = new StubLauncher();
        var bridge = CreateBridge(launcher);

        var request = """{"requestId":"r3","type":"launcher:launch","payload":{}}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        Assert.Null(launcher.LaunchedAppId);
        var response = Parse(responseJson!);
        Assert.False(response.GetProperty("ok").GetBoolean());
        Assert.Equal("invalid_payload", response.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoadStateRequest_ReturnsCurrentState()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: PlayerShellStateNames.Active,
            SessionId: Guid.NewGuid(),
            LeaseExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(10),
            RemainingSeconds: 600,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "ok",
            LauncherApps: []);
        var bridge = new PlayerShellWebHostBridge(new StubLauncher(), getLatestState: () => state);

        var request = """{"requestId":"r4","type":"shell:loadState"}""";
        var responseJson = await bridge.HandleAsync(request, CancellationToken.None);

        var response = Parse(responseJson!);
        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal("active", response.GetProperty("payload").GetProperty("state").GetString());
    }

    [Fact]
    public void StateChangedEnvelope_SerializesAsPushMessage()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: false,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "locked",
            LauncherApps: []);

        var json = PlayerShellWebHostBridge.CreateStatePush(state);

        var envelope = Parse(json);
        Assert.Equal("shell:stateChanged", envelope.GetProperty("type").GetString());
        Assert.Equal("locked", envelope.GetProperty("payload").GetProperty("state").GetString());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter PlayerShellWebHostBridgeTests`
Expected: FAIL — `PlayerShellWebHostBridge` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell/Web/PlayerShellWebHostBridge.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Player.Shell.Launcher;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Web;

public sealed class PlayerShellWebHostBridge(
    ILauncherCommandClient launcher,
    Func<PlayerShellStateDto?> getLatestState)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> AllowedTypes =
    [
        "shell:loadState",
        "launcher:launch",
        "shell:requestOperator",
        "shell:pause"
    ];

    public async Task<string?> HandleAsync(string requestJson, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(requestJson);
        var root = doc.RootElement;
        var requestId = root.TryGetProperty("requestId", out var id) ? id.GetString() ?? "" : "";
        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

        if (!AllowedTypes.Contains(type))
        {
            return Error(requestId, "unknown_request", $"Unsupported request type '{type}'.");
        }

        var payload = root.TryGetProperty("payload", out var p) ? p : default;

        return type switch
        {
            "shell:loadState" => Ok(requestId, getLatestState()),
            "launcher:launch" => await HandleLaunchAsync(requestId, payload, cancellationToken),
            "shell:requestOperator" => Ok(requestId, new { requested = true }),
            "shell:pause" => Ok(requestId, new { paused = true }),
            _ => Error(requestId, "unknown_request", "Unsupported request type.")
        };
    }

    public static string CreateStatePush(PlayerShellStateDto state) =>
        JsonSerializer.Serialize(new { type = "shell:stateChanged", payload = state }, JsonOptions);

    private async Task<string> HandleLaunchAsync(string requestId, JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("appId", out var appIdEl)
            || appIdEl.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(appIdEl.GetString()))
        {
            return Error(requestId, "invalid_payload", "launcher:launch requires a non-empty appId.");
        }

        var result = await launcher.LaunchAsync(appIdEl.GetString()!, ct);
        return Ok(requestId, result);
    }

    private static string Ok(string requestId, object? payload) =>
        JsonSerializer.Serialize(
            new { type = "host:response", requestId, ok = true, payload },
            JsonOptions);

    private static string Error(string requestId, string code, string message) =>
        JsonSerializer.Serialize(
            new { type = "host:response", requestId, ok = false, error = new { code, message } },
            JsonOptions);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run (Windows bridge): `dotnet test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter PlayerShellWebHostBridgeTests`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell/Web/PlayerShellWebHostBridge.cs tests/AFK4.Player.Shell.Tests/Web/PlayerShellWebHostBridgeTests.cs
git commit -m "feat(player-shell): native web host bridge with request whitelist"
```

### Task B2: Wire the bridge + pipe-state push into the host window

**Files:**
- Modify: `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs`

> WebView2 glue again — verified manually on Windows. It connects the Task B1 bridge and the existing `NamedPipePlayerShellStateClient` to `WebMessageReceived` / `PostWebMessageAsJson`.

- [ ] **Step 1: Add bridge wiring and the state-listen loop**

In `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs`, add a `PlayerShellWebHostBridge` field, build it in the constructor, subscribe to `WebMessageReceived` after `EnsureCoreWebView2Async`, and start the pipe loop. Replace the class body's relevant parts so it reads:

```csharp
    private readonly PlayerShellWebHostBridge bridge;

    internal WebViewPlayerWindow(PlayerShellOptions options)
    {
        this.options = options;
        stateClient = new NamedPipePlayerShellStateClient(options);
        bridge = new PlayerShellWebHostBridge(new LauncherCommandClient(options), getLatestState: () => latestState);
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }
```

Append to the end of `OnLoaded` (after `Browser.Source = ...`):

```csharp
        Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _ = ListenForStateAsync(lifetime.Token);
```

Add the two methods:

```csharp
    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var responseJson = await bridge.HandleAsync(e.WebMessageAsJson, lifetime.Token);
        if (responseJson is not null && Browser.CoreWebView2 is not null)
        {
            Browser.CoreWebView2.PostWebMessageAsJson(responseJson);
        }
    }

    private async Task ListenForStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var state in stateClient.ReadStatesAsync(cancellationToken))
            {
                latestState = state;
                await Dispatcher.InvokeAsync(() =>
                {
                    Browser.CoreWebView2?.PostWebMessageAsJson(PlayerShellWebHostBridge.CreateStatePush(state));
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
```

- [ ] **Step 2: Build on the Windows bridge**

Run (Windows bridge): `dotnet build src/AFK4.Player.Shell/AFK4.Player.Shell.csproj`
Expected: build succeeds.

- [ ] **Step 3: Manual verification on Windows**

With the Agent.Service (or a pipe stub) pushing a state, confirm in the WebView2 dev build (temporarily re-enable dev tools) that a `shell:stateChanged` message arrives and a `launcher:launch` round-trips. Re-disable dev tools before committing.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs
git commit -m "feat(player-shell): wire bridge and pipe-state push into host window"
```

### Task B3: TypeScript bridge transport (`shellBridge.ts`)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/shellBridge.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellBridge.test.ts`

> This runs on Linux via `bun test`. It depends on the web scaffold from Unit C for its toolchain, but the file itself is plain TS. Implement the scaffold (Task C1) first if starting fresh; the steps below assume `bun test` works in `src/AFK4.Player.Shell.Web`.

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Player.Shell.Web/src/shellBridge.test.ts`:

```ts
import { afterEach, describe, expect, it } from 'bun:test';
import { onShellStateChanged, postShellRequest } from './shellBridge';

type Listener = (event: { data: unknown }) => void;

function installWebview(onPost: (message: any) => void) {
  const listeners: Listener[] = [];
  (window as any).chrome = {
    webview: {
      postMessage: (message: any) => onPost(message),
      addEventListener: (_type: 'message', listener: Listener) => listeners.push(listener),
      removeEventListener: (_type: 'message', listener: Listener) => {
        const i = listeners.indexOf(listener);
        if (i >= 0) listeners.splice(i, 1);
      }
    }
  };
  return {
    emit: (data: unknown) => listeners.forEach((l) => l({ data }))
  };
}

afterEach(() => {
  delete (window as any).chrome;
});

describe('postShellRequest', () => {
  it('resolves with the payload of the matching host:response', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = postShellRequest<{ paused: boolean }>('shell:pause');
    harness.emit({ type: 'host:response', requestId: sent.requestId, ok: true, payload: { paused: true } });

    await expect(promise).resolves.toEqual({ paused: true });
    expect(sent.type).toBe('shell:pause');
  });

  it('rejects when the host responds with ok=false', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = postShellRequest('launcher:launch', { appId: '' });
    harness.emit({
      type: 'host:response',
      requestId: sent.requestId,
      ok: false,
      error: { code: 'invalid_payload', message: 'bad' }
    });

    await expect(promise).rejects.toThrow('invalid_payload');
  });
});

describe('onShellStateChanged', () => {
  it('invokes the listener on shell:stateChanged pushes', () => {
    const harness = installWebview(() => {});
    const seen: unknown[] = [];

    const unsubscribe = onShellStateChanged((state) => seen.push(state));
    harness.emit({ type: 'shell:stateChanged', payload: { state: 'Active' } });
    unsubscribe();
    harness.emit({ type: 'shell:stateChanged', payload: { state: 'Locked' } });

    expect(seen).toEqual([{ state: 'Active' }]);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellBridge.test.ts`
Expected: FAIL — `./shellBridge` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell.Web/src/shellBridge.ts`:

```ts
declare global {
  interface Window {
    chrome?: {
      webview?: {
        postMessage(message: unknown): void;
        addEventListener?(type: 'message', listener: (event: { data: unknown }) => void): void;
        removeEventListener?(type: 'message', listener: (event: { data: unknown }) => void): void;
      };
    };
  }
}

interface HostResponse<T> {
  type: 'host:response';
  requestId: string;
  ok: boolean;
  payload?: T;
  error?: { code: string; message: string };
}

let requestCounter = 0;

function nextRequestId(): string {
  requestCounter += 1;
  return `req-${requestCounter}`;
}

export function postShellRequest<T>(type: string, payload?: unknown, timeoutMs = 15_000): Promise<T> {
  const webview = window.chrome?.webview;
  if (!webview?.postMessage || !webview.addEventListener) {
    return Promise.reject(new Error('shell bridge unavailable'));
  }

  const requestId = nextRequestId();

  return new Promise<T>((resolve, reject) => {
    const listener = (event: { data: unknown }) => {
      const data = event.data as HostResponse<T>;
      if (!data || data.type !== 'host:response' || data.requestId !== requestId) {
        return;
      }
      cleanup();
      if (data.ok) {
        resolve(data.payload as T);
      } else {
        reject(new Error(data.error?.code ?? 'host_error'));
      }
    };

    const timer = setTimeout(() => {
      cleanup();
      reject(new Error('shell request timed out'));
    }, timeoutMs);

    function cleanup() {
      clearTimeout(timer);
      webview!.removeEventListener?.('message', listener);
    }

    webview.addEventListener?.('message', listener);
    webview.postMessage({ requestId, type, payload });
  });
}

export function onShellStateChanged(handler: (state: unknown) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview?.addEventListener) {
    return () => {};
  }

  const listener = (event: { data: unknown }) => {
    const data = event.data as { type?: string; payload?: unknown };
    if (data?.type === 'shell:stateChanged') {
      handler(data.payload);
    }
  };

  webview.addEventListener('message', listener);
  return () => webview.removeEventListener?.('message', listener);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellBridge.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/shellBridge.ts src/AFK4.Player.Shell.Web/src/shellBridge.test.ts
git commit -m "feat(player-shell-web): typescript host bridge transport"
```

### Task B4: Typed shell client (`shellClient.ts`)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/shellClient.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellClient.test.ts`
- Depends on: `src/AFK4.Player.Shell.Web/src/shellContracts.ts` (Task C2)

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Player.Shell.Web/src/shellClient.test.ts`:

```ts
import { afterEach, describe, expect, it } from 'bun:test';
import { launchApp, loadShellState, pauseSession, requestOperator } from './shellClient';

function installWebview(onPost: (message: any) => void) {
  const listeners: Array<(event: { data: unknown }) => void> = [];
  (window as any).chrome = {
    webview: {
      postMessage: (message: any) => onPost(message),
      addEventListener: (_t: 'message', l: (event: { data: unknown }) => void) => listeners.push(l),
      removeEventListener: () => {}
    }
  };
  return { reply: (data: unknown) => listeners.forEach((l) => l({ data })) };
}

afterEach(() => {
  delete (window as any).chrome;
});

describe('shellClient', () => {
  it('launchApp sends launcher:launch with appId', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = launchApp('cs2');
    harness.reply({ type: 'host:response', requestId: sent.requestId, ok: true, payload: { status: 'accepted' } });

    await promise;
    expect(sent.type).toBe('launcher:launch');
    expect(sent.payload).toEqual({ appId: 'cs2' });
  });

  it('loadShellState sends shell:loadState', async () => {
    let sent: any;
    const harness = installWebview((message) => {
      sent = message;
    });

    const promise = loadShellState();
    harness.reply({ type: 'host:response', requestId: sent.requestId, ok: true, payload: null });

    await expect(promise).resolves.toBeNull();
    expect(sent.type).toBe('shell:loadState');
  });

  it('requestOperator and pauseSession send their types', async () => {
    const posts: any[] = [];
    const harness = installWebview((message) => posts.push(message));

    const op = requestOperator();
    harness.reply({ type: 'host:response', requestId: posts[0].requestId, ok: true, payload: { requested: true } });
    await op;

    const pause = pauseSession();
    harness.reply({ type: 'host:response', requestId: posts[1].requestId, ok: true, payload: { paused: true } });
    await pause;

    expect(posts.map((p) => p.type)).toEqual(['shell:requestOperator', 'shell:pause']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellClient.test.ts`
Expected: FAIL — `./shellClient` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell.Web/src/shellClient.ts`:

```ts
import { postShellRequest } from './shellBridge';
import type { PlayerShellState } from './shellContracts';

export function loadShellState(): Promise<PlayerShellState | null> {
  return postShellRequest<PlayerShellState | null>('shell:loadState').then((s) => s ?? null);
}

export function launchApp(appId: string): Promise<{ status: string }> {
  return postShellRequest<{ status: string }>('launcher:launch', { appId });
}

export function requestOperator(): Promise<{ requested: boolean }> {
  return postShellRequest<{ requested: boolean }>('shell:requestOperator');
}

export function pauseSession(): Promise<{ paused: boolean }> {
  return postShellRequest<{ paused: boolean }>('shell:pause');
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellClient.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/shellClient.ts src/AFK4.Player.Shell.Web/src/shellClient.test.ts
git commit -m "feat(player-shell-web): typed shell client wrappers"
```

---

## Unit C — Web scaffold + active-session screen

### Task C1: Scaffold the `AFK4.Player.Shell.Web` app

**Files:**
- Create: `src/AFK4.Player.Shell.Web/package.json`, `vite.config.ts`, `tsconfig.json`, `bunfig.toml`, `index.html`, `src/main.tsx`, `src/styles.css`, `src/test/setup.ts`
- Modify: `package.json` (repo root)

- [ ] **Step 1: Create the package manifest**

Create `src/AFK4.Player.Shell.Web/package.json`:

```json
{
  "name": "afk4-player-shell-web",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite --host 127.0.0.1 --port 5175",
    "build": "tsc -b && vite build",
    "test": "bun test",
    "preview": "vite preview --host 127.0.0.1 --port 4175"
  },
  "dependencies": {
    "@afk4/formatting": "workspace:*",
    "@afk4/i18n": "workspace:*",
    "@afk4/money": "workspace:*",
    "lucide-react": "^1.16.0",
    "react": "^19.2.6",
    "react-dom": "^19.2.6"
  },
  "devDependencies": {
    "@happy-dom/global-registrator": "^20.9.0",
    "@testing-library/jest-dom": "^6.9.1",
    "@testing-library/react": "^16.3.2",
    "@types/bun": "^1.3.14",
    "@types/react": "^19.2.15",
    "@types/react-dom": "^19.2.3",
    "@vitejs/plugin-react": "^6.0.2",
    "typescript": "^6.0.3",
    "vite": "^8.0.13"
  }
}
```

- [ ] **Step 2: Create vite/tsconfig/bun/test config**

Create `src/AFK4.Player.Shell.Web/vite.config.ts`:

```ts
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  base: './',
  plugins: [react()]
});
```

Create `src/AFK4.Player.Shell.Web/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "lib": ["DOM", "DOM.Iterable", "ES2022"],
    "jsx": "react-jsx",
    "strict": true,
    "module": "ESNext",
    "moduleResolution": "Bundler",
    "skipLibCheck": true,
    "noEmit": true
  },
  "include": ["src"]
}
```

Create `src/AFK4.Player.Shell.Web/bunfig.toml`:

```toml
[test]
preload = ["./src/test/setup.ts"]
```

Create `src/AFK4.Player.Shell.Web/src/test/setup.ts`:

```ts
import { GlobalRegistrator } from '@happy-dom/global-registrator';
import * as matchers from '@testing-library/jest-dom/matchers';

GlobalRegistrator.register({ url: 'https://player.afk4.local/' });
expect.extend(matchers);
```

- [ ] **Step 3: Create the HTML entry, React entry, and styles**

Create `src/AFK4.Player.Shell.Web/index.html`:

```html
<!doctype html>
<html lang="ru">
  <head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>AFK4.NET Player</title>
  </head>
  <body>
    <div id="root"></div>
    <script type="module" src="/src/main.tsx"></script>
  </body>
</html>
```

Create `src/AFK4.Player.Shell.Web/src/styles.css`:

```css
:root {
  color-scheme: dark;
}

body {
  margin: 0;
  background: #0b1220;
  color: #e5e7eb;
  font-family: system-ui, sans-serif;
}
```

Create `src/AFK4.Player.Shell.Web/src/main.tsx`:

```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';
import './styles.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <App />
    </I18nProvider>
  </StrictMode>
);
```

- [ ] **Step 4: Register the workspace**

In the repo-root `package.json`, add the new app to the `workspaces` array (keep the others):

```json
  "workspaces": [
    "packages/*",
    "src/AFK4.Platform.Web",
    "src/AFK4.Operator.App.Web",
    "src/AFK4.Customer.Web",
    "src/AFK4.SetupWizard.Web",
    "src/AFK4.Player.Shell.Web"
  ],
```

- [ ] **Step 5: Install and verify the toolchain**

Run: `/home/fedya/.bun/bin/bun install`
Then create a throwaway `src/AFK4.Player.Shell.Web/src/smoke.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';

describe('smoke', () => {
  it('runs', () => {
    expect(1 + 1).toBe(2);
  });
});
```

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/smoke.test.ts`
Expected: PASS. Then delete `src/smoke.test.ts`.

> Note: `App.tsx` is created in Task C3; until then the dev server won't build, but `bun test` works. Do the commit after C3 if you prefer a buildable tree, or commit the scaffold now and let C2/C3 follow.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Player.Shell.Web/package.json src/AFK4.Player.Shell.Web/vite.config.ts src/AFK4.Player.Shell.Web/tsconfig.json src/AFK4.Player.Shell.Web/bunfig.toml src/AFK4.Player.Shell.Web/index.html src/AFK4.Player.Shell.Web/src/main.tsx src/AFK4.Player.Shell.Web/src/styles.css src/AFK4.Player.Shell.Web/src/test/setup.ts package.json bun.lock
git commit -m "build(player-shell-web): scaffold vite + react app"
```

### Task C2: Shell DTO mirror + parity test

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/shellContracts.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellContracts.test.ts`

Since there is no C#→TS codegen, the DTO is hand-mirrored. The test pins the state-name constants so drift from `PlayerShellStateNames.cs` is caught in review.

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Player.Shell.Web/src/shellContracts.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { PlayerShellStateNames } from './shellContracts';

describe('shellContracts', () => {
  it('mirrors the C# PlayerShellStateNames constants exactly', () => {
    expect(PlayerShellStateNames).toEqual({
      Locked: 'locked',
      Active: 'active',
      Grace: 'grace',
      Ending: 'ending',
      Maintenance: 'maintenance',
      Offline: 'offline',
      Error: 'error'
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellContracts.test.ts`
Expected: FAIL — `./shellContracts` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell.Web/src/shellContracts.ts`:

```ts
// Hand-mirrored from AFK4.Shared.Contracts/Shell. No codegen exists; keep in sync.

// Values are lowercase to match the C# source of truth
// (AFK4.Shared.Contracts/Shell/PlayerShellStateNames.cs), which is what
// arrives over the wire — NOT the PascalCase member names.
export const PlayerShellStateNames = {
  Locked: 'locked',
  Active: 'active',
  Grace: 'grace',
  Ending: 'ending',
  Maintenance: 'maintenance',
  Offline: 'offline',
  Error: 'error'
} as const;

export type PlayerShellStateName = (typeof PlayerShellStateNames)[keyof typeof PlayerShellStateNames];

export interface LauncherApp {
  appId: string;
  displayName: string;
  category: string;
  iconUri: string | null;
  isAvailable: boolean;
}

export interface PlayerShellState {
  organizationId: string;
  branchId: string;
  deviceId: string;
  state: PlayerShellStateName;
  sessionId: string | null;
  leaseExpiresAtUtc: string | null;
  remainingSeconds: number | null;
  isOnline: boolean;
  isGraceMode: boolean;
  warningThresholdSeconds: number;
  message: string;
  launcherApps: LauncherApp[];
  locale: string;
  warningKind: string;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellContracts.test.ts`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/shellContracts.ts src/AFK4.Player.Shell.Web/src/shellContracts.test.ts
git commit -m "feat(player-shell-web): shell DTO mirror with parity test"
```

### Task C3: `useShellBridge` hook + App router

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/useShellBridge.ts`
- Create: `src/AFK4.Player.Shell.Web/src/App.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/useShellBridge.test.ts`
- Depends on: Tasks B3 (`shellBridge.ts`), B4 (`shellClient.ts`), C2 (`shellContracts.ts`)

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Player.Shell.Web/src/useShellBridge.test.ts`:

```ts
import { afterEach, describe, expect, it } from 'bun:test';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useShellBridge } from './useShellBridge';

function installWebview(onPost: (message: any) => void) {
  const listeners: Array<(event: { data: unknown }) => void> = [];
  (window as any).chrome = {
    webview: {
      postMessage: (message: any) => onPost(message),
      addEventListener: (_t: 'message', l: (event: { data: unknown }) => void) => listeners.push(l),
      removeEventListener: (_t: 'message', l: (event: { data: unknown }) => void) => {
        const i = listeners.indexOf(l);
        if (i >= 0) listeners.splice(i, 1);
      }
    }
  };
  return { push: (data: unknown) => act(() => listeners.forEach((l) => l({ data }))) };
}

afterEach(() => {
  delete (window as any).chrome;
});

describe('useShellBridge', () => {
  it('starts with null state then updates on shell:stateChanged pushes', async () => {
    const harness = installWebview(() => {});
    const { result } = renderHook(() => useShellBridge());

    expect(result.current.state).toBeNull();

    harness.push({ type: 'shell:stateChanged', payload: { state: 'Active', remainingSeconds: 600, launcherApps: [] } });

    await waitFor(() => expect(result.current.state?.state).toBe('Active'));
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/useShellBridge.test.ts`
Expected: FAIL — `./useShellBridge` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/AFK4.Player.Shell.Web/src/useShellBridge.ts`:

```ts
import { useEffect, useState } from 'react';
import { onShellStateChanged } from './shellBridge';
import { launchApp, loadShellState, pauseSession, requestOperator } from './shellClient';
import type { PlayerShellState } from './shellContracts';

export interface ShellBridge {
  state: PlayerShellState | null;
  launch: (appId: string) => Promise<{ status: string }>;
  requestOperator: () => Promise<{ requested: boolean }>;
  pause: () => Promise<{ paused: boolean }>;
}

export function useShellBridge(): ShellBridge {
  const [state, setState] = useState<PlayerShellState | null>(null);

  useEffect(() => {
    let active = true;
    loadShellState()
      .then((initial) => {
        if (active && initial) {
          setState(initial);
        }
      })
      .catch(() => {});

    const unsubscribe = onShellStateChanged((next) => setState(next as PlayerShellState));
    return () => {
      active = false;
      unsubscribe();
    };
  }, []);

  return { state, launch: launchApp, requestOperator, pause: pauseSession };
}
```

Create `src/AFK4.Player.Shell.Web/src/App.tsx`:

```tsx
import { ActiveSessionScreen } from './screens/ActiveSessionScreen';
import { LockedScreen } from './screens/LockedScreen';
import { PlayerShellStateNames } from './shellContracts';
import { useShellBridge } from './useShellBridge';

export function App() {
  const { state, launch, requestOperator } = useShellBridge();

  const locked =
    state === null ||
    state.state === PlayerShellStateNames.Locked ||
    state.state === PlayerShellStateNames.Offline ||
    state.state === PlayerShellStateNames.Error;

  if (locked) {
    return <LockedScreen state={state} onRequestOperator={requestOperator} />;
  }

  return <ActiveSessionScreen state={state} onLaunch={launch} onRequestOperator={requestOperator} />;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/useShellBridge.test.ts`
Expected: FAIL to compile until `ActiveSessionScreen`/`LockedScreen` exist. Create minimal placeholders to unblock — but they are fully implemented in Task C4. Implement C4 next, then re-run. (If running strictly in order, expect this hook test to pass once C4's screens exist.)

- [ ] **Step 5: Commit (after C4 compiles)**

```bash
git add src/AFK4.Player.Shell.Web/src/useShellBridge.ts src/AFK4.Player.Shell.Web/src/App.tsx src/AFK4.Player.Shell.Web/src/useShellBridge.test.ts
git commit -m "feat(player-shell-web): useShellBridge hook and state router"
```

### Task C4: Active-session and locked screens

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/ActiveSessionScreen.tsx`
- Create: `src/AFK4.Player.Shell.Web/src/screens/LockedScreen.tsx`
- Create: `src/AFK4.Player.Shell.Web/src/formatRemaining.ts`
- Test: `src/AFK4.Player.Shell.Web/src/screens/ActiveSessionScreen.test.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/formatRemaining.test.ts`

- [ ] **Step 1: Write the failing test for remaining-time formatting**

Create `src/AFK4.Player.Shell.Web/src/formatRemaining.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { formatRemaining } from './formatRemaining';

describe('formatRemaining', () => {
  it('formats seconds as H:MM:SS', () => {
    expect(formatRemaining(3661)).toBe('1:01:01');
  });

  it('formats sub-hour as M:SS', () => {
    expect(formatRemaining(125)).toBe('2:05');
  });

  it('renders a dash when null', () => {
    expect(formatRemaining(null)).toBe('—');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/formatRemaining.test.ts`
Expected: FAIL — `./formatRemaining` does not exist.

- [ ] **Step 3: Implement the formatter**

Create `src/AFK4.Player.Shell.Web/src/formatRemaining.ts`:

```ts
export function formatRemaining(seconds: number | null): string {
  if (seconds === null || seconds < 0) {
    return '—';
  }

  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const ss = String(s).padStart(2, '0');

  if (h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${ss}`;
  }
  return `${m}:${ss}`;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/formatRemaining.test.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Write the failing screen test**

Create `src/AFK4.Player.Shell.Web/src/screens/ActiveSessionScreen.test.tsx`:

```tsx
import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen } from '@testing-library/react';
import { ActiveSessionScreen } from './ActiveSessionScreen';
import type { PlayerShellState } from '../shellContracts';

const baseState: PlayerShellState = {
  organizationId: 'o',
  branchId: 'b',
  deviceId: 'd',
  state: 'active',
  sessionId: 's',
  leaseExpiresAtUtc: null,
  remainingSeconds: 3661,
  isOnline: true,
  isGraceMode: false,
  warningThresholdSeconds: 300,
  message: 'ok',
  launcherApps: [
    { appId: 'cs2', displayName: 'Counter-Strike 2', category: 'game', iconUri: null, isAvailable: true },
    { appId: 'valorant', displayName: 'Valorant', category: 'game', iconUri: null, isAvailable: false }
  ],
  locale: 'ru',
  warningKind: 'none'
};

describe('ActiveSessionScreen', () => {
  it('shows the formatted remaining time', () => {
    render(<ActiveSessionScreen state={baseState} onLaunch={mock(async () => ({ status: 'accepted' }))} onRequestOperator={mock(async () => ({ requested: true }))} />);
    expect(screen.getByText('1:01:01')).toBeInTheDocument();
  });

  it('launches an available app on click', () => {
    const onLaunch = mock(async () => ({ status: 'accepted' }));
    render(<ActiveSessionScreen state={baseState} onLaunch={onLaunch} onRequestOperator={mock(async () => ({ requested: true }))} />);

    fireEvent.click(screen.getByRole('button', { name: /Counter-Strike 2/ }));
    expect(onLaunch).toHaveBeenCalledWith('cs2');
  });

  it('disables unavailable apps', () => {
    render(<ActiveSessionScreen state={baseState} onLaunch={mock(async () => ({ status: 'accepted' }))} onRequestOperator={mock(async () => ({ requested: true }))} />);
    expect(screen.getByRole('button', { name: /Valorant/ })).toBeDisabled();
  });
});
```

- [ ] **Step 6: Run to verify it fails**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/screens/ActiveSessionScreen.test.tsx`
Expected: FAIL — screen does not exist.

- [ ] **Step 7: Implement the screens**

Create `src/AFK4.Player.Shell.Web/src/screens/ActiveSessionScreen.tsx`:

```tsx
import { formatRemaining } from '../formatRemaining';
import type { PlayerShellState } from '../shellContracts';

interface Props {
  state: PlayerShellState;
  onLaunch: (appId: string) => Promise<{ status: string }>;
  onRequestOperator: () => Promise<{ requested: boolean }>;
}

export function ActiveSessionScreen({ state, onLaunch, onRequestOperator }: Props) {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', padding: '20px 28px', borderBottom: '1px solid #1f3a5f' }}>
        <strong style={{ fontSize: 24 }}>
          AFK4<span style={{ color: '#2dd4a7' }}>.NET</span>
        </strong>
        <span style={{ fontSize: 28, fontWeight: 600 }}>{formatRemaining(state.remainingSeconds)}</span>
      </header>

      <main style={{ flex: 1, padding: 42 }}>
        {state.warningKind !== 'none' && (
          <p style={{ color: '#fde68a' }}>{state.message}</p>
        )}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
          {state.launcherApps.map((app) => (
            <button
              key={app.appId}
              type="button"
              disabled={!app.isAvailable}
              onClick={() => onLaunch(app.appId)}
              style={{
                minHeight: 120,
                background: '#10233a',
                border: '1px solid #2b5b84',
                color: '#fff',
                borderRadius: 10,
                opacity: app.isAvailable ? 1 : 0.45
              }}
            >
              <span style={{ fontSize: 18, fontWeight: 600 }}>{app.displayName}</span>
            </button>
          ))}
        </div>
      </main>

      <footer style={{ padding: '14px 24px', borderTop: '1px solid #1f3a5f' }}>
        <button type="button" onClick={() => onRequestOperator()} style={{ background: 'none', border: '1px solid #2b5b84', color: '#9ca3af', borderRadius: 8, padding: '8px 14px' }}>
          Позвать оператора
        </button>
      </footer>
    </div>
  );
}
```

Create `src/AFK4.Player.Shell.Web/src/screens/LockedScreen.tsx`:

```tsx
import type { PlayerShellState } from '../shellContracts';

interface Props {
  state: PlayerShellState | null;
  onRequestOperator: () => Promise<{ requested: boolean }>;
}

export function LockedScreen({ state, onRequestOperator }: Props) {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 20 }}>
      <strong style={{ fontSize: 34 }}>
        AFK4<span style={{ color: '#2dd4a7' }}>.NET</span>
      </strong>
      <p style={{ color: '#9ca3af', fontSize: 20 }}>{state?.message ?? 'Экран заблокирован'}</p>
      <button type="button" onClick={() => onRequestOperator()} style={{ background: 'none', border: '1px solid #2b5b84', color: '#9ca3af', borderRadius: 8, padding: '10px 18px' }}>
        Позвать оператора
      </button>
    </div>
  );
}
```

- [ ] **Step 8: Run the screen + hook tests to verify they pass**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test`
Expected: PASS — all web tests green (shellBridge, shellClient, shellContracts, useShellBridge, formatRemaining, ActiveSessionScreen).

- [ ] **Step 9: Build the web app**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun run build`
Expected: `tsc -b && vite build` succeeds, `dist/` produced.

- [ ] **Step 10: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/screens src/AFK4.Player.Shell.Web/src/formatRemaining.ts src/AFK4.Player.Shell.Web/src/formatRemaining.test.ts
git commit -m "feat(player-shell-web): active-session and locked screens"
```

### Task C5: Wire the web build into the host bundle

**Files:**
- Modify: `src/AFK4.Player.Shell/AFK4.Player.Shell.csproj`

- [ ] **Step 1: Copy the web `dist` into `WebAssets` at build**

Add an MSBuild target to `src/AFK4.Player.Shell/AFK4.Player.Shell.csproj` that copies the built web `dist` into the output `WebAssets` folder (mirrors how Operator.App ships its web assets). Insert before `</Project>`:

```xml
  <Target Name="CopyPlayerWebAssets" BeforeTargets="Build">
    <ItemGroup>
      <PlayerWebDist Include="$(MSBuildProjectDirectory)\..\AFK4.Player.Shell.Web\dist\**\*" />
    </ItemGroup>
    <Copy SourceFiles="@(PlayerWebDist)"
          DestinationFiles="@(PlayerWebDist->'$(OutDir)WebAssets\%(RecursiveDir)%(Filename)%(Extension)')"
          SkipUnchangedFiles="true"
          Condition="'@(PlayerWebDist)' != ''" />
  </Target>
```

- [ ] **Step 2: Build the web app, then the host, on the Windows bridge**

Run (Windows bridge):
```
cd src/AFK4.Player.Shell.Web && bun run build
cd ../.. && dotnet build src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
```
Expected: host build copies `WebAssets\index.html` into the output.

- [ ] **Step 3: Manual end-to-end verification on Windows**

Run `AFK4.Player.Shell.exe` with a pipe source (Agent.Service or stub). Confirm:
1. The React active-session screen renders full-screen via `https://player.afk4.local/index.html`.
2. The timer reflects `RemainingSeconds` from the pipe.
3. Clicking an available launcher app triggers a launch round-trip.
4. A `Locked` pipe state switches to the locked screen.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
git commit -m "build(player-shell): bundle web dist into host WebAssets"
```

---

## Self-Review

**Spec coverage (against the design doc):**
- Thin native host + WebView2 + virtual-host mapping → Tasks A1, A4, A5. ✓
- Kiosk hardening (no devtools/context menu) → Task A5 `HardenForKiosk`. ✓
- Native fallback + watchdog → Tasks A2 (policy) + A5 (`ProcessFailed`→fallback). ✓
- Fail-locked principle → Task A3 + App router (Task C3). ✓
- Narrow, validated bridge (whitelist + input validation) → Task B1 (`AllowedTypes`, `invalid_payload`). ✓
- Bridge over existing pipe/launcher, state push → Tasks B2, B1 `CreateStatePush`. ✓
- React app reusing the stack + `@afk4/*` → Tasks C1–C4. ✓
- Active-session screen (timer, launcher, call-operator) → Task C4. ✓
- DTO parity without codegen → Task C2 parity test. ✓
- Bundling web into host → Task C5. ✓
- Token transport, REST/`useShellApi`, top-up/extend/shop/login → **intentionally deferred to Plan 2** (Units D–G). Noted in the header. ✓
- Offline degrade of server actions → Plan 2 (no REST in this plan; pipe/lease path already renders offline). ✓
- ACL on the named pipe → Plan 2/Agent.Service side (the pipe is owned by Agent.Service; the client side is unchanged here). Flagged for the D/E plan.

**Placeholder scan:** No "TBD"/"add error handling"/"similar to" — every code step has full content. The only forward-references are explicit cross-task ordering notes (C3 depends on C4's screens), with concrete code in the referenced task. ✓

**Type consistency:** Envelope shape is identical across C# (`{type,requestId,ok,payload,error:{code,message}}`) and TS (`HostResponse<T>`); push shape `{type:'shell:stateChanged',payload}` matches `CreateStatePush`; `PlayerShellStateNames` constants match `shellContracts.ts` and the C# constants; `launcher:launch` payload `{appId}` matches `HandleLaunchAsync`. ✓

**Build/test reality:** All `.NET` steps are marked "Windows bridge" (WPF can't build on Linux); all web steps use `/home/fedya/.bun/bin/bun`. ✓
