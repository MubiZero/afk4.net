using System.Net;
using System.Net.Http;
using AFK4.SetupWizard.Core;
using AFK4.SetupWizard.Core.SilentInstall;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests.SilentInstall;

public sealed class SilentInstallerTests
{
    private static readonly InstallEnrollResponse Enrolled = new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "secret",
        "approved", "https://api.afk4.net", "stable", DateTimeOffset.UnixEpoch)
    {
        AssignedSeatName = "PC-07"
    };

    [Fact]
    public async Task EnrollsByCode_ThenSetsUpThePcLikeTheWizard()
    {
        var harness = new Harness(_ => Task.FromResult(Enrolled));

        var exitCode = await harness.RunAsync(new SilentInstallOptions("7KQ2-M9XD-4TPV-HB3R", "PC-07"));

        Assert.Equal(SilentInstallExitCodes.Installed, exitCode);
        var request = Assert.Single(harness.Api.Requests);
        Assert.Equal("7KQ2-M9XD-4TPV-HB3R", request.Code);
        Assert.Equal("PC-07", request.SeatName);
        Assert.Equal("WIN-HALL-07", request.MachineName);
        Assert.Equal("device-public-key", request.DevicePublicKey);
        Assert.Equal(DeviceRoleNames.GamingPc, harness.Bootstrap.Written!.Role);
        Assert.Equal(Enrolled.CredentialSecret, harness.Bootstrap.Written.CredentialSecret);
        Assert.Equal(1, harness.Shell.Calls);
        Assert.True(harness.Completion.Completed);
    }

    // Скрипт развёртывания часто стартует раньше, чем на ПК поднялась сеть.
    [Fact]
    public async Task WaitsForThePlatform_WhenTheNetworkIsNotUpYet()
    {
        var calls = 0;
        var harness = new Harness(_ => ++calls < 3
            ? throw new HttpRequestException("No such host is known.")
            : Task.FromResult(Enrolled));

        var exitCode = await harness.RunAsync(new SilentInstallOptions("7KQ2-M9XD-4TPV-HB3R", null));

        Assert.Equal(SilentInstallExitCodes.Installed, exitCode);
        Assert.Equal(3, calls);
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], harness.Delays);
    }

    [Fact]
    public async Task WaitsOut_TheDoorAskingToSlowDown()
    {
        var calls = 0;
        var harness = new Harness(_ => ++calls < 2
            ? throw new HttpRequestException("Too many requests", null, HttpStatusCode.TooManyRequests)
            : Task.FromResult(Enrolled));

        Assert.Equal(SilentInstallExitCodes.Installed, await harness.RunAsync(new SilentInstallOptions("код", null)));
        Assert.Single(harness.Delays);
    }

    [Fact]
    public async Task GivesUp_WhenThePlatformNeverAnswers()
    {
        var harness = new Harness(_ => throw new HttpRequestException("No such host is known."));

        var exitCode = await harness.RunAsync(new SilentInstallOptions("код", null));

        Assert.Equal(SilentInstallExitCodes.PlatformUnreachable, exitCode);
        Assert.Equal(SilentInstaller.RetryDelays.Count, harness.Delays.Count);
        Assert.Null(harness.Bootstrap.Written);
    }

    // Отказ с причиной не повторяется: неверный код от повтора верным не станет, а скрипт
    // зря простоял бы восемь минут на каждом ПК зала.
    [Theory]
    [InlineData("install_code_invalid")]
    [InlineData("plan_limit_reached")]
    public async Task ARefusalWithAReason_IsNotRetried(string code)
    {
        var harness = new Harness(_ => throw new SetupWizardApiException(code, "Platform API returned 400.", remainingAttempts: null));

        var exitCode = await harness.RunAsync(new SilentInstallOptions("код", null));

        Assert.Equal(SilentInstallExitCodes.CodeRefused, exitCode);
        Assert.Empty(harness.Delays);
        Assert.Single(harness.Api.Requests);
        Assert.Equal(0, harness.Shell.Calls);
    }

    // Приостановленный клуб платформа отклоняет без машинного кода — это всё равно отказ, а не сбой.
    [Fact]
    public async Task ARefusalWithoutACode_IsStillARefusal()
    {
        var harness = new Harness(_ => throw new HttpRequestException("Bad request", null, HttpStatusCode.BadRequest));

        Assert.Equal(SilentInstallExitCodes.CodeRefused, await harness.RunAsync(new SilentInstallOptions("код", null)));
        Assert.Empty(harness.Delays);
    }

    [Fact]
    public async Task ServerError_IsRetried()
    {
        var calls = 0;
        var harness = new Harness(_ => ++calls < 2
            ? throw new HttpRequestException("Bad gateway", null, HttpStatusCode.BadGateway)
            : Task.FromResult(Enrolled));

        Assert.Equal(SilentInstallExitCodes.Installed, await harness.RunAsync(new SilentInstallOptions("код", null)));
    }

    [Fact]
    public async Task ShellThatDidNotInstall_FailsTheRun_AndLeavesTheAgentAlone()
    {
        var harness = new Harness(_ => Task.FromResult(Enrolled));
        harness.Shell.Result = ShellProvisionResult.Failed(1603, "Fatal error during installation.");

        var exitCode = await harness.RunAsync(new SilentInstallOptions("код", null));

        Assert.Equal(SilentInstallExitCodes.SetupFailed, exitCode);
        Assert.False(harness.Completion.Completed);
    }

    [Fact]
    public async Task AgentThatDidNotStart_FailsTheRun()
    {
        var harness = new Harness(_ => Task.FromResult(Enrolled));
        harness.Completion.Failure = new InvalidOperationException("sc.exe exited with code 1053.");

        Assert.Equal(SilentInstallExitCodes.SetupFailed, await harness.RunAsync(new SilentInstallOptions("код", null)));
    }

    [Fact]
    public async Task ConfigurationThatDidNotWrite_FailsTheRun()
    {
        var harness = new Harness(_ => Task.FromResult(Enrolled));
        harness.Bootstrap.Failure = new UnauthorizedAccessException("Access to the path is denied.");

        Assert.Equal(SilentInstallExitCodes.SetupFailed, await harness.RunAsync(new SilentInstallOptions("код", null)));
        Assert.Equal(0, harness.Shell.Calls);
    }

    private sealed class Harness
    {
        public Harness(Func<InstallCodeEnrollRequest, Task<InstallEnrollResponse>> enroll)
        {
            Api = new FakeApi(enroll);
            var setup = new SetupWizardDeviceSetup(Bootstrap, Completion, Shell, new FakeProvisioner(), new FakeLauncher());
            Installer = new SilentInstaller(
                Api,
                new FakeKeyStore(),
                new SetupWizardMachineInfo("WIN-HALL-07"),
                setup,
                (delay, _) =>
                {
                    Delays.Add(delay);
                    return Task.CompletedTask;
                });
        }

        public FakeApi Api { get; }
        public FakeBootstrapWriter Bootstrap { get; } = new();
        public FakeCompletionAction Completion { get; } = new();
        public FakeProvisioner Shell { get; } = new();
        public List<TimeSpan> Delays { get; } = [];
        public SilentInstaller Installer { get; }

        public Task<int> RunAsync(SilentInstallOptions options) => Installer.RunAsync(options, CancellationToken.None);
    }

    private sealed class FakeApi(Func<InstallCodeEnrollRequest, Task<InstallEnrollResponse>> enroll) : IInstallCodeEnrollmentClient
    {
        public List<InstallCodeEnrollRequest> Requests { get; } = [];

        public Task<InstallEnrollResponse> EnrollByCodeAsync(InstallCodeEnrollRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return enroll(request);
        }
    }

    private sealed class FakeKeyStore : IDeviceKeyStore
    {
        public Task<string> GetOrCreatePublicKeyPemAsync(CancellationToken cancellationToken) => Task.FromResult("device-public-key");
    }

    private sealed class FakeBootstrapWriter : ISetupWizardBootstrapWriter
    {
        public SetupWizardBootstrapConfig? Written { get; private set; }
        public Exception? Failure { get; set; }

        public void Write(SetupWizardBootstrapConfig config)
        {
            if (Failure is not null) throw Failure;
            Written = config;
        }
    }

    private sealed class FakeCompletionAction : ISetupWizardCompletionAction
    {
        public bool Completed { get; private set; }
        public Exception? Failure { get; set; }

        public void Complete()
        {
            if (Failure is not null) throw Failure;
            Completed = true;
        }
    }

    private sealed class FakeProvisioner : ISetupWizardShellProvisioner
    {
        public int Calls { get; private set; }
        public ShellProvisionResult Result { get; set; } = ShellProvisionResult.Installed(0);

        public ShellProvisionResult Provision()
        {
            Calls++;
            return Result;
        }
    }

    private sealed class FakeLauncher : ISetupWizardOperatorLauncher
    {
        public void Launch()
        {
        }
    }
}
