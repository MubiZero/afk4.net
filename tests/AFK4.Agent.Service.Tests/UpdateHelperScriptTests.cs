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
    public void InstallUpdateMsiScript_InstallsWebView2PrerequisiteBeforeOrganizationAdminMsi()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "install-afk4-update-msi.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("Test-WebView2RuntimeInstalled", script, StringComparison.Ordinal);
        Assert.Contains("Install-WebView2Runtime", script, StringComparison.Ordinal);
        Assert.Contains("$Component -eq 'organization-admin'", script, StringComparison.Ordinal);
        Assert.Contains(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", script, StringComparison.Ordinal);
        Assert.Contains("MicrosoftEdgeWebView2Setup.exe", script, StringComparison.Ordinal);
        // Скачанный файл запускается с правами системы: подпись Microsoft — единственное, что
        // стоит между подменённой загрузкой и установщиком, которому мы отдаём машину.
        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("O=Microsoft Corporation", script, StringComparison.Ordinal);
        Assert.Contains("Refusing to run it.", script, StringComparison.Ordinal);
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
        var exitIndex = script.IndexOf("exit $process.ExitCode", successBranchIndex, StringComparison.Ordinal);

        Assert.Contains("[string] $AgentServiceName = 'AFK4.Agent.Service'", script, StringComparison.Ordinal);
        Assert.Contains("function Start-AgentServiceAfterSelfUpdate", script, StringComparison.Ordinal);
        Assert.Contains("$Component -ne 'agent-service'", script, StringComparison.Ordinal);
        Assert.Contains("Start-Service -Name $Name -ErrorAction Stop", script, StringComparison.Ordinal);
        Assert.True(startCallIndex > successBranchIndex, "The helper should start AFK4.Agent.Service only after msiexec succeeds.");
        Assert.True(startCallIndex < exitIndex, "The helper should attempt service startup before returning.");
    }

    // 3010 значит «установлено, нужна перезагрузка». Раньше скрипт сводил его к нулю, и платформа
    // записывала «обновлено» по всему парку, который до перезагрузки работал на старой сборке.
    [Fact]
    public void InstallUpdateMsiScript_ReportsRebootRequiredInsteadOfCollapsingItToSuccess()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "install-afk4-update-msi.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("$rebootRequiredExitCode = 3010", script, StringComparison.Ordinal);
        Assert.Contains("exit $process.ExitCode", script, StringComparison.Ordinal);
        Assert.DoesNotContain("exit 0", script, StringComparison.Ordinal);
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
        // The agent MSI is no longer the deliverable — it is embedded into the client bundle as a
        // build input (AgentMsiPath) and moved to intermediates\. The bundle .exe is what ships.
        Assert.Contains("-d \"AgentMsiPath=$agentMsiPath\"", script, StringComparison.Ordinal);
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
        // The player-shell MSI is a bundled input (carried inside the agent MSI payload), not a
        // standalone deliverable — the build moves it into the intermediates\ subfolder instead of
        // echoing it as an output (see "move bundled operator/player-shell MSIs into intermediates").
        Assert.Contains("foreach ($bundledMsi in @($organizationAdminMsiPath, $playerShellMsiPath))", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsStandaloneX64OrganizationAdminMsi()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);
        var organizationAdminBuild = script[
            script.IndexOf("installers/organization-admin/Package.wxs", StringComparison.Ordinal)..];

        Assert.Contains("@{ Name = 'organization-admin'; Path = 'src/AFK4.OrganizationAdmin.App/AFK4.OrganizationAdmin.App.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
        // All four client components must publish framework-dependent so the bundle's shared
        // runtime is the single .NET copy (see Workstream A). A stray "SelfContained = $true" in
        // the $projects list would re-bloat the MSI back toward 160 MB.
        Assert.Contains("@{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
        Assert.Contains("@{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
        Assert.Contains("@{ Name = 'setup-wizard'; Path = 'src/AFK4.SetupWizard/AFK4.SetupWizard.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
        Assert.Contains("-arch x64", organizationAdminBuild, StringComparison.Ordinal);
        Assert.Contains("-d \"OrganizationAdminPublishDir=$(Join-Path $publishRoot \"organization-admin-$Version-$Channel\")\"", organizationAdminBuild, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsAndPublishesOperatorFrontendAssets()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("BunPath", script, StringComparison.Ordinal);
        Assert.Contains("SkipOrganizationAdminWebRestore", script, StringComparison.Ordinal);
        Assert.Contains("src/AFK4.OrganizationAdmin.Web", script, StringComparison.Ordinal);
        Assert.Contains("& $BunPath install --frozen-lockfile", script, StringComparison.Ordinal);
        Assert.Contains("& $BunPath run build", script, StringComparison.Ordinal);
        Assert.Contains("Organization Admin frontend build did not produce", script, StringComparison.Ordinal);
        Assert.Contains("$organizationAdminWebAssetsPublishDir = Join-Path $organizationAdminPublishDir 'WebAssets'", script, StringComparison.Ordinal);
        Assert.Contains("Copy-Item -Destination $organizationAdminWebAssetsPublishDir -Recurse -Force", script, StringComparison.Ordinal);
        Assert.Contains("WindowsInstaller.Installer", script, StringComparison.Ordinal);
        Assert.Contains("Assert-OperatorMsiContainsFrontendAssets -MsiPath $organizationAdminMsiPath", script, StringComparison.Ordinal);
        Assert.Contains("Organization Admin MSI does not contain", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("installers/organization-admin/Package.wxs")]
    [InlineData("installers/player-shell/Package.wxs")]
    [InlineData("installers/agent/Package.wxs")]
    public void WixPackages_DoNotUseUnsupportedFilesExcludeAttribute(string packagePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), packagePath));
        var package = File.ReadAllText(absolutePath);

        Assert.DoesNotContain(" Exclude=", package, StringComparison.Ordinal);
    }

    /// <summary>
    /// Упавшего агента поднимает Windows (спека оболочки, §6.1): без него на ПК нет ни экрана игрока,
    /// ни запретов. util:ServiceConfig, а не встроенные элементы MSI — те WiX сам помечает как
    /// ненадёжные; и сборка агента обязана подключать расширение Util, иначе элемент не соберётся.
    /// </summary>
    [Fact]
    public void AgentWixPackage_TellsWindowsToRestartAFailedService()
    {
        var root = GetRepositoryRoot();
        var document = System.Xml.Linq.XDocument.Load(Path.Combine(root, "installers", "agent", "Package.wxs"));
        System.Xml.Linq.XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        System.Xml.Linq.XNamespace util = "http://wixtoolset.org/schemas/v4/wxs/util";
        var service = document.Descendants(wix + "ServiceInstall").Single(element => (string?)element.Attribute("Name") == "AFK4.Agent.Service");

        var recovery = service.Element(util + "ServiceConfig");
        Assert.NotNull(recovery);
        Assert.Equal("restart", (string?)recovery.Attribute("FirstFailureActionType"));
        Assert.Equal("restart", (string?)recovery.Attribute("SecondFailureActionType"));
        Assert.Equal("restart", (string?)recovery.Attribute("ThirdFailureActionType"));
        Assert.Equal("1", (string?)recovery.Attribute("ResetPeriodInDays"));
        Assert.Null(service.Element(wix + "ServiceConfigFailureActions"));

        var script = File.ReadAllText(Path.Combine(root, "scripts", "build-client-packages.ps1"));
        var agentBuild = script[script.IndexOf("installers/agent/Package.wxs", StringComparison.Ordinal)..];
        agentBuild = agentBuild[..agentBuild.IndexOf("-o $agentMsiPath", StringComparison.Ordinal)];
        Assert.Contains("-ext WixToolset.Util.wixext", agentBuild, StringComparison.Ordinal);
    }

    /// <summary>
    /// Тихая установка по коду: код доезжает от командной строки установщика до мастера и нигде по
    /// дороге не пишется в журнал — ни Burn, ни MSI. Окно мастера при коде не открывается.
    /// </summary>
    [Fact]
    public void Installers_CarryTheInstallCodeToTheWizard_WithoutLoggingIt()
    {
        var root = GetRepositoryRoot();
        System.Xml.Linq.XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";
        System.Xml.Linq.XNamespace bal = "http://wixtoolset.org/schemas/v4/wxs/bal";

        var package = System.Xml.Linq.XDocument.Load(Path.Combine(root, "installers", "agent", "Package.wxs"));
        var codeProperty = package.Descendants(wix + "Property").Single(element => (string?)element.Attribute("Id") == "AFK4_INSTALL_CODE");
        Assert.Equal("yes", (string?)codeProperty.Attribute("Hidden"));
        Assert.Equal("yes", (string?)codeProperty.Attribute("Secure"));
        Assert.Equal("yes", (string?)package.Descendants(wix + "Property").Single(element => (string?)element.Attribute("Id") == "AFK4_SEAT").Attribute("Secure"));

        var silent = package.Descendants(wix + "CustomAction").Single(element => (string?)element.Attribute("Id") == "LaunchSetupWizardSilently");
        Assert.Equal("SetupWizardExe", (string?)silent.Attribute("FileRef"));
        Assert.Equal("--install-code \"[AFK4_INSTALL_CODE]\" --seat \"[AFK4_SEAT]\"", (string?)silent.Attribute("ExeCommand"));
        Assert.Equal("yes", (string?)silent.Attribute("HideTarget"));
        Assert.Equal("asyncNoWait", (string?)silent.Attribute("Return"));

        var sequence = package.Descendants(wix + "Custom").ToDictionary(element => (string)element.Attribute("Action")!, element => (string)element.Attribute("Condition")!);
        Assert.StartsWith("AFK4_INSTALL_CODE", sequence["LaunchSetupWizardSilently"], StringComparison.Ordinal);
        Assert.Contains("NOT AFK4_INSTALL_CODE", sequence["LaunchSetupWizard"], StringComparison.Ordinal);

        var bundle = System.Xml.Linq.XDocument.Load(Path.Combine(root, "installers", "bundle", "Bundle.wxs"));
        var variables = bundle.Descendants(wix + "Variable").ToDictionary(element => (string)element.Attribute("Name")!);
        Assert.Equal("yes", (string?)variables["AFK4_INSTALL_CODE"].Attribute(bal + "Overridable"));
        Assert.Equal("yes", (string?)variables["AFK4_INSTALL_CODE"].Attribute("Hidden"));
        Assert.Equal("yes", (string?)variables["AFK4_SEAT"].Attribute(bal + "Overridable"));
        var forwarded = bundle.Descendants(wix + "MsiProperty").ToDictionary(element => (string)element.Attribute("Name")!, element => (string?)element.Attribute("Value"));
        Assert.Equal("[AFK4_INSTALL_CODE]", forwarded["AFK4_INSTALL_CODE"]);
        Assert.Equal("[AFK4_SEAT]", forwarded["AFK4_SEAT"]);

        // Аргументы, которые MSI передаёт мастеру, мастер и разбирает.
        var options = File.ReadAllText(Path.Combine(root, "src", "AFK4.SetupWizard.Core", "SilentInstall", "SilentInstallOptions.cs"));
        Assert.Contains("CodeArgument = \"--install-code\"", options, StringComparison.Ordinal);
        Assert.Contains("SeatArgument = \"--seat\"", options, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleAgentWixPackage_InstallsSetupWizardAndFirstRunLaunch()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "agent", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("Name=\"AFK4.NET Agent\"", package, StringComparison.Ordinal);
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
            "Condition=\"NOT Installed AND NOT WIX_UPGRADE_DETECTED AND NOT AFK4_INSTALL_CODE AND (UILevel &gt;= 3 OR LAUNCHWIZARD = &quot;1&quot;)\"",
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

        var startCheckIndex = action.IndexOf("if (startResult != 0)", StringComparison.Ordinal);
        var clearIndex = action.IndexOf("SetupWizardFirstRunRegistration.Clear();", StringComparison.Ordinal);

        // Обе строки должны найтись: прежняя искала проверку, которой в коде давно нет, получала -1
        // и проходила всегда — порядок никто не проверял.
        Assert.True(startCheckIndex >= 0, "The Agent start check was not found; update this test with the code.");
        Assert.True(clearIndex > startCheckIndex, "The first-run marker should be cleared only after Agent service startup succeeds.");
        Assert.Contains(@"Software\AFK4\SetupWizard", registration, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", registration, StringComparison.Ordinal);
        Assert.Contains("FirstRunPending", registration, StringComparison.Ordinal);
        Assert.Contains("AFK4.NET Setup Wizard", registration, StringComparison.Ordinal);
        Assert.Contains("DeleteValue(valueName, throwOnMissingValue: false)", registration, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerShellWixPackage_InstallsShellAndWritesAgentShellConfiguration()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "player-shell", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("Name=\"AFK4.NET Player Shell\"", package, StringComparison.Ordinal);
        Assert.Contains("$(var.PlayerShellPublishDir)", package, StringComparison.Ordinal);
        Assert.Contains("AFK4.Player.Shell.exe", package, StringComparison.Ordinal);
        Assert.Contains("Agent__ShellVersion", package, StringComparison.Ordinal);
        Assert.Contains("Agent__PlayerShellExecutablePath", package, StringComparison.Ordinal);
        Assert.Contains("Agent__PlayerShellAutoStartEnabled", package, StringComparison.Ordinal);
    }

    [Fact]
    public void OrganizationAdminWixPackage_RequiresWebView2Runtime()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "organization-admin", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("WEBVIEW2_RUNTIME_HKLM_PV", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKCU_PV", package, StringComparison.Ordinal);
        Assert.Contains(@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", package, StringComparison.Ordinal);
        Assert.Contains(@"Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", package, StringComparison.Ordinal);
        Assert.Contains("<Launch", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKLM_PV &lt;&gt; &quot;0.0.0.0&quot;", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKCU_PV &lt;&gt; &quot;0.0.0.0&quot;", package, StringComparison.Ordinal);
        Assert.Contains("WEBVIEW2_RUNTIME_HKLM64_PV &lt;&gt; &quot;0.0.0.0&quot;", package, StringComparison.Ordinal);
        // Отказ называет обычный путь (установщик клуба ставит рантайм сам) и даёт ссылку тому,
        // кто всё-таки ставит MSI в одиночку. Прежний текст говорил «поставьте рантайм» и молчал
        // о том, где его взять и что бандл делает это без него.
        Assert.Contains("afk4-client-&lt;version&gt;-&lt;channel&gt;.exe", package, StringComparison.Ordinal);
        Assert.Contains("https://go.microsoft.com/fwlink/p/?LinkId=2124703", package, StringComparison.Ordinal);
    }

    // Обе клиентские MSI спрашивают WebView2 одинаково: раньше оболочка игрока проверяла один
    // ключ из трёх и не смотрела на «0.0.0.0», то есть на машине с per-user рантаймом отказывала
    // там, где админка ставилась.
    [Fact]
    public void PlayerShellWixPackage_ChecksWebView2TheSameWayAsOrganizationAdmin()
    {
        var shell = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "installers", "player-shell", "Package.wxs"));

        foreach (var marker in new[]
                 {
                     "WEBVIEW2_RUNTIME_HKLM_PV",
                     "WEBVIEW2_RUNTIME_HKLM64_PV",
                     "WEBVIEW2_RUNTIME_HKCU_PV",
                     "WEBVIEW2_RUNTIME_HKCU_PV &lt;&gt; &quot;0.0.0.0&quot;",
                     "afk4-client-&lt;version&gt;-&lt;channel&gt;.exe"
                 })
        {
            Assert.Contains(marker, shell, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void OrganizationAdminWixPackage_WritesAgentOrganizationAdminVersion()
    {
        var packagePath = Path.Combine(GetRepositoryRoot(), "installers", "organization-admin", "Package.wxs");
        var package = File.ReadAllText(packagePath);

        Assert.Contains("Agent__OrganizationAdminVersion", package, StringComparison.Ordinal);
        Assert.Contains("Value=\"$(var.PackageVersion)\"", package, StringComparison.Ordinal);
        Assert.Contains("Agent__OrganizationAdminExecutablePath", package, StringComparison.Ordinal);
        Assert.Contains("[OrganizationAdminFolder]AFK4.OrganizationAdmin.App.exe", package, StringComparison.Ordinal);
        Assert.DoesNotContain("UPDATE_COORDINATION_SECRET", package, StringComparison.Ordinal);
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
