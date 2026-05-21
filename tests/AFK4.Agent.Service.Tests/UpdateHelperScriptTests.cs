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
    public void ClientPackageBuildScript_ExplicitlyAcceptsWix7EulaForCiBuilds()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("-acceptEula", script, StringComparison.Ordinal);
        Assert.Contains("wix7", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientPackageBuildScript_BuildsGamingPcMsiAsX64Package()
    {
        var scriptPath = Path.Combine(GetRepositoryRoot(), "scripts", "build-client-packages.ps1");
        var script = File.ReadAllText(scriptPath);
        var gamingPcBuild = script[
            script.IndexOf("installers/gaming-pc/Package.wxs", StringComparison.Ordinal)..];

        Assert.Contains("-arch x64", gamingPcBuild, StringComparison.Ordinal);
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
    [InlineData("installers/gaming-pc/Package.wxs")]
    public void WixPackages_DoNotUseUnsupportedFilesExcludeAttribute(string packagePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), packagePath));
        var package = File.ReadAllText(absolutePath);

        Assert.DoesNotContain(" Exclude=", package, StringComparison.Ordinal);
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
