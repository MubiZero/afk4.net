using System.Management.Automation.Language;

namespace AFK4.Agent.Service.Tests;

public sealed class UpdateHelperScriptTests
{
    [Theory]
    [InlineData("scripts/install-afk4-update-msi.ps1", "PackagePath")]
    [InlineData("scripts/rollback-afk4-update-msi.ps1", "PackagePath")]
    [InlineData("scripts/restart-afk4-agent-service.ps1", "ServiceName")]
    public void Script_ParsesWithoutPowerShellErrors(string scriptPath, string requiredParameter)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), scriptPath));

        var ast = Parser.ParseFile(absolutePath, out _, out var errors);

        Assert.Empty(errors);
        Assert.Contains(
            ast.ParamBlock!.Parameters,
            parameter => string.Equals(
                parameter.Name.VariablePath.UserPath,
                requiredParameter,
                StringComparison.Ordinal));
    }

    [Fact]
    public void InstallUpdateMsiScript_InstallsWebView2PrerequisiteBeforeOperatorAppMsi()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "install-afk4-update-msi.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("Test-WebView2RuntimeInstalled", script, StringComparison.Ordinal);
        Assert.Contains("Install-WebView2Runtime", script, StringComparison.Ordinal);
        Assert.Contains("$Component -eq 'operator-app'", script, StringComparison.Ordinal);
        Assert.Contains(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", script, StringComparison.Ordinal);
        Assert.Contains("MicrosoftEdgeWebView2Setup.exe", script, StringComparison.Ordinal);
        Assert.Contains("/silent", script, StringComparison.Ordinal);
        Assert.Contains("/install", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallUpdateMsiScript_StartsAgentServiceAfterAgentServiceSelfUpdate()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "install-afk4-update-msi.ps1");
        var script = File.ReadAllText(scriptPath);

        var successBranchIndex = script.IndexOf("$process.ExitCode -eq 0", StringComparison.Ordinal);
        var startCallIndex = script.IndexOf("Start-AgentServiceAfterSelfUpdate -Name $AgentServiceName", StringComparison.Ordinal);
        var successExitIndex = script.IndexOf("exit 0", successBranchIndex, StringComparison.Ordinal);

        Assert.Contains("[string] $AgentServiceName = 'AFK4.Agent.Service'", script, StringComparison.Ordinal);
        Assert.Contains("function Start-AgentServiceAfterSelfUpdate", script, StringComparison.Ordinal);
        Assert.Contains("$Component -ne 'agent-service'", script, StringComparison.Ordinal);
        Assert.Contains("Start-Service -Name $Name -ErrorAction Stop", script, StringComparison.Ordinal);
        Assert.True(startCallIndex > successBranchIndex, "The helper should start AFK4.Agent.Service only after msiexec succeeds.");
        Assert.True(startCallIndex < successExitIndex, "The helper should attempt service startup before returning success.");
    }

    [Fact]
    public void ClientPackageBuildScript_ExplicitlyAcceptsWix7EulaForCiBuilds()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("-acceptEula", script, StringComparison.Ordinal);
        Assert.Contains("wix7", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsLegacyGamingPcMsiOnlyWhenRequested()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);
        var legacyMsiConditionIndex = script.IndexOf("if ($includeLegacyGamingPcPackage)", StringComparison.Ordinal);
        var legacyMsiBuildIndex = script.IndexOf("installers/gaming-pc/Package.wxs", StringComparison.Ordinal);
        var gamingPcBuild = script[
            legacyMsiBuildIndex..];

        Assert.Contains("IncludeLegacyGamingPcPackage", script, StringComparison.Ordinal);
        Assert.Contains("BuildLegacyStagingBootstrapper", script, StringComparison.Ordinal);
        Assert.True(legacyMsiBuildIndex > legacyMsiConditionIndex, "The coordinated gaming-PC MSI must not be built by default.");
        Assert.Contains("-arch x64", gamingPcBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsSingleAgentMsiWithSetupWizard()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);
        var agentBuild = script[
            script.IndexOf("installers/agent/Package.wxs", StringComparison.Ordinal)..];

        Assert.Contains("src/AFK4.SetupWizard/AFK4.SetupWizard.csproj", script, StringComparison.Ordinal);
        Assert.Contains("setup-wizard-$Version-$Channel", script, StringComparison.Ordinal);
        Assert.Contains("afk4-agent-$Version-$Channel.msi", script, StringComparison.Ordinal);
        Assert.Contains("-arch x64", agentBuild, StringComparison.Ordinal);
        Assert.Contains("-d \"SetupWizardPublishDir=$setupWizardPublishDir\"", agentBuild, StringComparison.Ordinal);
        Assert.Contains("Write-Host $agentMsiPath", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsStandalonePlayerShellMsi()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);
        var playerShellBuild = script[
            script.IndexOf("installers/player-shell/Package.wxs", StringComparison.Ordinal)..];

        Assert.Contains("afk4-player-shell-$Version-$Channel.msi", script, StringComparison.Ordinal);
        Assert.Contains("-arch x64", playerShellBuild, StringComparison.Ordinal);
        Assert.Contains("-d \"PlayerShellPublishDir=$(Join-Path $publishRoot \"player-shell-$Version-$Channel\")\"", playerShellBuild, StringComparison.Ordinal);
        Assert.Contains("Write-Host $playerShellMsiPath", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsStandaloneX64OperatorAppMsi()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);
        var operatorAppBuild = script[
            script.IndexOf("installers/operator-app/Package.wxs", StringComparison.Ordinal)..];

        Assert.Contains("@{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj'; SelfContained = $true }", script, StringComparison.Ordinal);
        Assert.Contains("-arch x64", operatorAppBuild, StringComparison.Ordinal);
        Assert.Contains("-d \"OperatorAppPublishDir=$(Join-Path $publishRoot \"operator-app-$Version-$Channel\")\"", operatorAppBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsAndPublishesOperatorFrontendAssets()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("NpmPath", script, StringComparison.Ordinal);
        Assert.Contains("SkipOperatorWebRestore", script, StringComparison.Ordinal);
        Assert.Contains("src/AFK4.Operator.App.Web", script, StringComparison.Ordinal);
        Assert.Contains("& $NpmPath ci", script, StringComparison.Ordinal);
        Assert.Contains("& $NpmPath run build", script, StringComparison.Ordinal);
        Assert.Contains("Operator App frontend build did not produce", script, StringComparison.Ordinal);
        Assert.Contains("$operatorWebAssetsPublishDir = Join-Path $operatorAppPublishDir 'WebAssets'", script, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -Destination $operatorWebAssetsPublishDir -Recurse -Force", script, StringComparison.Ordinal);
        Assert.Contains("WindowsInstaller.Installer", script, StringComparison.Ordinal);
        Assert.Contains("Assert-OperatorMsiContainsFrontendAssets -MsiPath $operatorMsiPath", script, StringComparison.Ordinal);
        Assert.Contains("Operator App MSI does not contain", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("installers/operator-app/Package.wxs")]
    [InlineData("installers/player-shell/Package.wxs")]
    [InlineData("installers/gaming-pc/Package.wxs")]
    [InlineData("installers/agent/Package.wxs")]
    public void WixPackages_DoNotUseUnsupportedFilesExcludeAttribute(string packagePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), packagePath));
        var package = File.ReadAllText(absolutePath);

        Assert.DoesNotContain(" Exclude=", package, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleAgentWixPackage_InstallsSetupWizardAndFirstRunLaunch()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "agent", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("Name=\"AFK4 Agent\"", package, StringComparison.Ordinal);
        Assert.Contains("SetupWizardFolder", package, StringComparison.Ordinal);
        Assert.Contains("$(var.SetupWizardPublishDir)", package, StringComparison.Ordinal);
        Assert.Contains("AFK4.SetupWizard.exe", package, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", package, StringComparison.Ordinal);
        Assert.Contains("AFK4SetupWizardFirstRunPending", package, StringComparison.Ordinal);
        Assert.Contains("ProgramMenuFolder", package, StringComparison.Ordinal);
        Assert.Contains("LaunchSetupWizard", package, StringComparison.Ordinal);
        Assert.Contains("asyncNoWait", package, StringComparison.Ordinal);
        Assert.Contains("Id=\"SetupWizardRegistration\"", package, StringComparison.Ordinal);
        Assert.Contains("Condition=\"NOT WIX_UPGRADE_DETECTED\"", package, StringComparison.Ordinal);
        Assert.Contains(
            "Condition=\"NOT Installed AND NOT WIX_UPGRADE_DETECTED AND UILevel &gt;= 3\"",
            package,
            StringComparison.Ordinal);
        Assert.Contains("Start=\"auto\"", package, StringComparison.Ordinal);
        Assert.DoesNotContain("Start=\"demand\"", package, StringComparison.Ordinal);
        Assert.DoesNotContain("Start=\"install\"", package, StringComparison.Ordinal);
        Assert.DoesNotContain("PlayerShell", package, StringComparison.Ordinal);
        Assert.DoesNotContain("Player Shell", package, StringComparison.Ordinal);
    }

    [Fact]
    public void SetupWizardCompletionAction_ClearsFirstRunRegistryStateAfterEnrollment()
    {
        var action = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "AFK4.SetupWizard", "AgentServiceCompletionAction.cs"));
        var registration = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "AFK4.SetupWizard", "SetupWizardFirstRunRegistration.cs"));

        var startCheckIndex = action.IndexOf("if (startResult is not 0 and not ServiceAlreadyRunning)", StringComparison.Ordinal);
        var clearIndex = action.IndexOf("SetupWizardFirstRunRegistration.Clear();", StringComparison.Ordinal);

        Assert.True(clearIndex > startCheckIndex, "The first-run marker should be cleared only after Agent service startup succeeds.");
        Assert.Contains(@"Software\AFK4\SetupWizard", registration, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", registration, StringComparison.Ordinal);
        Assert.Contains("FirstRunPending", registration, StringComparison.Ordinal);
        Assert.Contains("AFK4 Setup Wizard", registration, StringComparison.Ordinal);
        Assert.Contains("DeleteValue(valueName, throwOnMissingValue: false)", registration, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerShellWixPackage_InstallsShellAndWritesAgentShellConfiguration()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "player-shell", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("Name=\"AFK4 Player Shell\"", package, StringComparison.Ordinal);
        Assert.Contains("$(var.PlayerShellPublishDir)", package, StringComparison.Ordinal);
        Assert.Contains("AFK4.Player.Shell.exe", package, StringComparison.Ordinal);
        Assert.Contains("Agent__ShellVersion", package, StringComparison.Ordinal);
        Assert.Contains("Agent__PlayerShellExecutablePath", package, StringComparison.Ordinal);
        Assert.Contains("Agent__PlayerShellAutoStartEnabled", package, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorAppWixPackage_RequiresWebView2Runtime()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "operator-app", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("WEBVIEW2_RUNTIME_HKLM_PV", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKCU_PV", package, StringComparison.Ordinal);
        Assert.Contains(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", package, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", package, StringComparison.Ordinal);
        Assert.Contains("<Launch", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKLM_PV &lt;&gt; &quot;0.0.0.0&quot;", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKCU_PV &lt;&gt; &quot;0.0.0.0&quot;", package, StringComparison.Ordinal);
        Assert.Contains("Microsoft Edge WebView2 Runtime is required.", package, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorAppWixPackage_WritesAgentOperatorAppVersion()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "operator-app", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("Agent__OperatorAppVersion", package, StringComparison.Ordinal);
        Assert.Contains("Value=\"$(var.PackageVersion)\"", package, StringComparison.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
