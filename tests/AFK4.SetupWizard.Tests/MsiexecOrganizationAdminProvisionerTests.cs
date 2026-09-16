using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

/// <summary>
/// Установка панели управляющего на рабочем месте кассира. Зеркало установки оболочки игрока, но
/// проверялась до сих пор только вторая: перепутанный файл пейлоада или потерянный код возврата
/// здесь означал бы, что человек за стойкой остался без приложения, а мастер об этом смолчал.
/// </summary>
public sealed class MsiexecOrganizationAdminProvisionerTests
{
    private const string MsiPath = @"C:\Program Files\AFK4\Setup Wizard\payload\AFK4.OrganizationAdmin.msi";

    [Theory]
    [InlineData(0, ShellProvisionStatus.Installed)]
    [InlineData(3010, ShellProvisionStatus.Installed)]
    [InlineData(1638, ShellProvisionStatus.AlreadyPresent)]
    [InlineData(1603, ShellProvisionStatus.Failed)]
    public void Provision_MapsMsiexecExitCodes(int exitCode, ShellProvisionStatus expected)
    {
        var provisioner = Create(new FakeProcessRunner(exitCode, "msiexec output"));

        var result = provisioner.Provision();

        Assert.Equal(expected, result.Status);
        Assert.Equal(exitCode, result.ExitCode);
    }

    // Ставится именно панель управляющего, а не оболочка игрока: оба MSI лежат рядом в пейлоаде.
    [Fact]
    public void Provision_InstallsTheOrganizationAdminPayloadQuietly()
    {
        var runner = new FakeProcessRunner(0, string.Empty);
        var requestedFiles = new List<string>();
        var provisioner = new MsiexecOrganizationAdminProvisioner(
            new SetupWizardPayloadResolver(
                _ => true,
                fileName =>
                {
                    requestedFiles.Add(fileName);
                    return MsiPath;
                }),
            runner);

        provisioner.Provision();

        Assert.Equal(["AFK4.OrganizationAdmin.msi"], requestedFiles);
        Assert.Equal("msiexec.exe", runner.CapturedFileName);
        Assert.Equal(new[] { "/i", MsiPath, "/qn" }, runner.CapturedArguments);
    }

    // Пейлоада нет — установщик не запускаем и не делаем вид, что поставили.
    [Fact]
    public void Provision_WhenBundledMsiMissing_FailsWithoutRunningMsiexec()
    {
        var runner = new FakeProcessRunner(0, string.Empty);
        var provisioner = new MsiexecOrganizationAdminProvisioner(
            new SetupWizardPayloadResolver(_ => false, _ => MsiPath),
            runner);

        var result = provisioner.Provision();

        Assert.Equal(ShellProvisionStatus.Failed, result.Status);
        Assert.Null(runner.CapturedFileName);
    }

    private static MsiexecOrganizationAdminProvisioner Create(IProcessRunner runner) =>
        new(new SetupWizardPayloadResolver(_ => true, _ => MsiPath), runner);

    private sealed class FakeProcessRunner(int exitCode, string output) : IProcessRunner
    {
        public string? CapturedFileName { get; private set; }

        public IReadOnlyList<string>? CapturedArguments { get; private set; }

        public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            CapturedFileName = fileName;
            CapturedArguments = arguments;
            return new ProcessRunResult(exitCode, output);
        }
    }
}
