using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

public sealed class MsiexecPlayerShellProvisionerTests
{
    private const string MsiPath = @"C:\Program Files\AFK4\Setup Wizard\payload\AFK4.Player.Shell.msi";

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

    private static MsiexecPlayerShellProvisioner Create(IProcessRunner runner) =>
        new(new SetupWizardPayloadResolver(_ => true, _ => MsiPath), runner);

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

    [Fact]
    public void Provision_RunsMsiexecInstallQuietForTheBundledMsi()
    {
        var runner = new FakeProcessRunner(0, string.Empty);
        var provisioner = Create(runner);

        provisioner.Provision();

        Assert.Equal("msiexec.exe", runner.CapturedFileName);
        Assert.Equal(new[] { "/i", MsiPath, "/qn" }, runner.CapturedArguments);
    }

    [Fact]
    public void Provision_WhenBundledMsiMissing_FailsWithoutRunningMsiexec()
    {
        var runner = new FakeProcessRunner(0, string.Empty);
        var provisioner = new MsiexecPlayerShellProvisioner(
            new SetupWizardPayloadResolver(_ => false, _ => MsiPath),
            runner);

        var result = provisioner.Provision();

        Assert.Equal(ShellProvisionStatus.Failed, result.Status);
        Assert.Null(runner.CapturedFileName);
    }
}
