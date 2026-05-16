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

    [Theory]
    [InlineData("installers/operator-app/Package.wxs")]
    [InlineData("installers/gaming-pc/Package.wxs")]
    public void WixPackages_DoNotUseUnsupportedFilesExcludeAttribute(string packagePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), packagePath));
        var package = File.ReadAllText(absolutePath);

        Assert.DoesNotContain(" Exclude=", package, StringComparison.Ordinal);
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
