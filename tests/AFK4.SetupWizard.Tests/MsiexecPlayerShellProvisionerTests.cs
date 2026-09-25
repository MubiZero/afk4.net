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

    private sealed class SequenceProcessRunner(params int[] exitCodes) : IProcessRunner
    {
        public int Runs { get; private set; }

        public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            var exitCode = exitCodes[Math.Min(Runs, exitCodes.Length - 1)];
            Runs++;
            return new ProcessRunResult(exitCode, "msiexec output");
        }
    }

    // При тихой установке мастер стартует, пока установщик агента ещё закрывает свою сессию:
    // первая попытка натыкается на занятый Windows Installer, и это не повод бросать ПК без оболочки.
    [Fact]
    public void Provision_WaitsOutABusyWindowsInstaller()
    {
        var runner = new SequenceProcessRunner(1618, 1618, 0);
        var waits = new List<TimeSpan>();
        var provisioner = new MsiexecPlayerShellProvisioner(
            new SetupWizardPayloadResolver(_ => true, _ => MsiPath), runner, waits.Add);

        var result = provisioner.Provision();

        Assert.Equal(ShellProvisionStatus.Installed, result.Status);
        Assert.Equal(3, runner.Runs);
        Assert.Equal(2, waits.Count);
    }

    [Fact]
    public void Provision_GivesUpOnAnInstallerThatStaysBusy()
    {
        var runner = new SequenceProcessRunner(1618);
        var waits = new List<TimeSpan>();
        var provisioner = new MsiexecPlayerShellProvisioner(
            new SetupWizardPayloadResolver(_ => true, _ => MsiPath), runner, waits.Add);

        var result = provisioner.Provision();

        Assert.Equal(ShellProvisionStatus.Failed, result.Status);
        Assert.Equal(1618, result.ExitCode);
        Assert.Equal(24, runner.Runs);
        Assert.Equal(TimeSpan.FromMinutes(2) - TimeSpan.FromSeconds(5), waits.Aggregate(TimeSpan.Zero, (sum, wait) => sum + wait));
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
