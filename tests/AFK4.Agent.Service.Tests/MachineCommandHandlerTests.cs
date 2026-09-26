using System.Net;
using System.Net.NetworkInformation;
using AFK4.Agent.Service.Commands;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Network;
using AFK4.Agent.Service.Protection;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Команды администратора самой машине (спека оболочки, §5.8). Сервер уже не пускает питание и
/// обслуживание на занятый ПК; агент проверяет ещё раз, потому что видит сессию своими глазами.
/// </summary>
public sealed class MachineCommandHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    [Theory]
    [InlineData(DeviceCommandTypeNames.Reboot, MachinePowerAction.Restart, DeviceCommandOutcomeNames.RebootScheduled)]
    [InlineData(DeviceCommandTypeNames.Shutdown, MachinePowerAction.Shutdown, DeviceCommandOutcomeNames.ShutdownScheduled)]
    public async Task Power_OnAFreePc_IsScheduledWithTimeToReport(string type, MachinePowerAction action, string outcome)
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(type);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(outcome, result.Outcome);
        var scheduled = Assert.Single(fixture.Power.Scheduled);
        Assert.Equal(action, scheduled.Action);
        // Ответ серверу уходит до того, как Windows начнёт: иначе команда осталась бы без результата.
        Assert.Equal(TimeSpan.FromSeconds(10), scheduled.Delay);
    }

    [Theory]
    [InlineData(DeviceCommandTypeNames.Reboot)]
    [InlineData(DeviceCommandTypeNames.Shutdown)]
    [InlineData(DeviceCommandTypeNames.MaintenanceOn)]
    public async Task PowerAndMaintenance_DoNotTouchAPcSomeoneIsPlayingOn(string type)
    {
        var fixture = new Fixture();
        fixture.RuntimeState.Save(new AgentRuntimeState(PlayerShellStateNames.Active, false, Guid.NewGuid(), Now.AddHours(1), Now));

        var result = await fixture.HandleAsync(type);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(DeviceCommandOutcomeNames.SessionInProgress, result.Outcome);
        Assert.Empty(fixture.Power.Scheduled);
        Assert.Equal(PlayerShellStateNames.Active, fixture.RuntimeState.Current.State);
    }

    [Fact]
    public async Task WakeNeighbour_SendsTheMagicPacketIntoItsOwnNetwork()
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(DeviceCommandTypeNames.WakeNeighbor, new()
        {
            ["mac"] = "AA-BB-CC-DD-EE-02",
            ["broadcast"] = "192.168.1.255",
            ["targetDeviceId"] = Guid.NewGuid().ToString("D")
        });

        Assert.Equal(DeviceCommandOutcomeNames.WakePacketSent, result.Outcome);
        var sent = Assert.Single(fixture.Wake.Sent);
        Assert.Equal(PhysicalAddress.Parse("AA-BB-CC-DD-EE-02"), sent.Mac);
        Assert.Equal(IPAddress.Parse("192.168.1.255"), sent.Broadcast);
    }

    [Fact]
    public async Task WakeNeighbour_FromAnotherNetwork_IsRefused()
    {
        // Пакет не проходит маршрутизатор: отправка из чужой подсети только изобразила бы пробуждение.
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(DeviceCommandTypeNames.WakeNeighbor, new()
        {
            ["mac"] = "AA-BB-CC-DD-EE-02",
            ["broadcast"] = "10.0.0.255"
        });

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(DeviceCommandOutcomeNames.WakeTargetInvalid, result.Outcome);
        Assert.Empty(fixture.Wake.Sent);
    }

    [Fact]
    public async Task WakeNeighbour_WithAnUnreadableMac_IsRefused()
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(DeviceCommandTypeNames.WakeNeighbor, new() { ["mac"] = "nope", ["broadcast"] = "192.168.1.255" });

        Assert.Equal(DeviceCommandOutcomeNames.WakeTargetInvalid, result.Outcome);
        Assert.Empty(fixture.Wake.Sent);
    }

    /// <summary>Спека оболочки, §6.5: обслуживание открывает ПК технику, а не запирает его.</summary>
    [Fact]
    public async Task Maintenance_OpensThePcForTheTechnician_AndReturningLocksItAgain()
    {
        var fixture = new Fixture();

        var on = await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOn);

        Assert.Equal(DeviceCommandOutcomeNames.MaintenanceStarted, on.Outcome);
        Assert.Equal(PlayerShellStateNames.Maintenance, fixture.RuntimeState.Current.State);
        Assert.Equal(1, fixture.Lock.Unlocks);
        Assert.Equal(1, fixture.Desktop.Opened);
        Assert.True(fixture.RuntimeState.Current.MaintenanceDesktopOpened);

        var off = await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOff);

        Assert.Equal(DeviceCommandOutcomeNames.MaintenanceEnded, off.Outcome);
        Assert.Equal(PlayerShellStateNames.Locked, fixture.RuntimeState.Current.State);
        Assert.Equal(1, fixture.Desktop.Closed);
        Assert.Equal(1, fixture.Lock.Locks);
    }

    /// <summary>Спека, §6.5: профиль защиты снимается на обслуживание и встаёт по возвращении в зал.</summary>
    [Fact]
    public async Task Maintenance_LiftsTheProtectionProfile_AndReturningPutsItBack()
    {
        var protection = new CountingProtection();
        var fixture = new Fixture(protection);

        await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOn);
        Assert.Equal(1, protection.Releases);

        await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOff);
        Assert.Equal(1, protection.Applies);
    }

    [Fact]
    public async Task ThePolicyRefreshCommand_RereadsTheProfile()
    {
        var protection = new CountingProtection();
        var fixture = new Fixture(protection);

        var result = await fixture.HandleAsync(DeviceCommandTypeNames.PolicyRefresh);

        Assert.Equal(DeviceCommandOutcomeNames.ProtectionApplied, result.Outcome);
        Assert.Equal(1, protection.Refreshes);
    }

    /// <summary>До перевода в киоск проводник — обычная оболочка учётки: его не закрываем.</summary>
    [Fact]
    public async Task ReturningToTheFloor_LeavesAnExplorerTheAgentDidNotStart()
    {
        var fixture = new Fixture();
        fixture.Desktop.AlreadyOpen = true;

        await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOn);
        await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOff);

        Assert.Equal(0, fixture.Desktop.Closed);
        Assert.Equal(PlayerShellStateNames.Locked, fixture.RuntimeState.Current.State);
    }

    [Fact]
    public async Task ADesktopThatWouldNotOpen_DoesNotStopMaintenance()
    {
        var fixture = new Fixture();
        fixture.Desktop.Fails = true;

        var on = await fixture.HandleAsync(DeviceCommandTypeNames.MaintenanceOn);

        Assert.Equal(DeviceCommandOutcomeNames.MaintenanceStarted, on.Outcome);
        Assert.Equal(PlayerShellStateNames.Maintenance, fixture.RuntimeState.Current.State);
        Assert.False(fixture.RuntimeState.Current.MaintenanceDesktopOpened);
    }

    [Fact]
    public async Task TheHeartbeat_CatchesUpOnAMissedMaintenanceCommand_BothWays()
    {
        var fixture = new Fixture();

        await fixture.Maintenance.ReconcileAsync(maintenance: true, CancellationToken.None);
        Assert.Equal(PlayerShellStateNames.Maintenance, fixture.RuntimeState.Current.State);

        await fixture.Maintenance.ReconcileAsync(maintenance: false, CancellationToken.None);
        Assert.Equal(PlayerShellStateNames.Locked, fixture.RuntimeState.Current.State);
    }

    [Fact]
    public async Task TheHeartbeat_DoesNotEndSomeonesGame_ForMaintenance()
    {
        var fixture = new Fixture();
        fixture.RuntimeState.Save(new AgentRuntimeState(PlayerShellStateNames.Active, false, Guid.NewGuid(), Now.AddHours(1), Now));

        await fixture.Maintenance.ReconcileAsync(maintenance: true, CancellationToken.None);

        Assert.Equal(PlayerShellStateNames.Active, fixture.RuntimeState.Current.State);
        Assert.Equal(0, fixture.Desktop.Opened);
    }

    [Fact]
    public async Task AMessage_GoesToTheConnectedPlayerScreen()
    {
        var fixture = new Fixture();
        var reader = fixture.Host.Attach();

        var result = await fixture.HandleAsync(DeviceCommandTypeNames.Message, new() { ["text"] = "Через пять минут закрываемся" });

        Assert.Equal(DeviceCommandOutcomeNames.DeliveredToShell, result.Outcome);
        Assert.True(reader.TryRead(out var frame));
        Assert.Equal(ShellPipeMessageTypeNames.Command, frame.Type);
        Assert.Equal(DeviceCommandTypeNames.Message, frame.Command!.Type);
        Assert.Equal("Через пять минут закрываемся", frame.Command.Text);
    }

    [Theory]
    [InlineData(DeviceCommandTypeNames.SignOut)]
    [InlineData(DeviceCommandTypeNames.Message)]
    public async Task WithoutAPlayerScreen_TheAgentSaysThereIsNobodyToTell(string type)
    {
        var fixture = new Fixture();

        var result = await fixture.HandleAsync(type, new() { ["text"] = "Привет" });

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(DeviceCommandOutcomeNames.ShellNotConnected, result.Outcome);
    }

    [Fact]
    public async Task AScreenThatLeft_GetsNothingMore()
    {
        var fixture = new Fixture();
        var reader = fixture.Host.Attach();
        fixture.Host.Detach(reader);

        var result = await fixture.HandleAsync(DeviceCommandTypeNames.SignOut);

        Assert.Equal(DeviceCommandOutcomeNames.ShellNotConnected, result.Outcome);
    }

    [Fact]
    public async Task PolicyRefresh_IsAccepted_AndSaysThereWasNothingToDo()
    {
        var result = await new Fixture().HandleAsync(DeviceCommandTypeNames.PolicyRefresh);

        Assert.Equal("Accepted", result.Status);
        Assert.Equal(DeviceCommandOutcomeNames.NothingToRefresh, result.Outcome);
    }

    [Fact]
    public async Task TheCommandHandler_PassesMachineCommandsOn_InsteadOfSayingItCannot()
    {
        var fixture = new Fixture();
        var handler = new DefaultDeviceCommandHandler(
            Microsoft.Extensions.Options.Options.Create(new AgentOptions()),
            fixture.Coordinator,
            new ShellWarningStore(),
            NullLogger<DefaultDeviceCommandHandler>.Instance,
            machineCommands: fixture.Handler);

        var result = await handler.HandleAsync(
            new DeviceCommandDto(Guid.NewGuid(), DeviceCommandTypeNames.PolicyRefresh, Now, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(DeviceCommandOutcomeNames.NothingToRefresh, result.Outcome);
    }

    private sealed class Fixture
    {
        public Fixture(IProtectionEnforcer? protection = null)
        {
            Maintenance = new MaintenanceMode(RuntimeState, Lock, Desktop, TimeProvider.System, NullLogger<MaintenanceMode>.Instance, protection);
            Handler = new MachineCommandHandler(
                RuntimeState,
                Maintenance,
                Power,
                Wake,
                new FixedNetworkIdentity(new NetworkIdentity("AA-BB-CC-DD-EE-01", "192.168.1.0/24", "192.168.1.255")),
                Host,
                NullLogger<MachineCommandHandler>.Instance,
                protection);
        }

        public PlayerShellStateBuilderTests.MemoryRuntimeStateStore RuntimeState { get; } = new(AgentRuntimeState.Locked(Now));

        public LockingCoordinator Coordinator => coordinator ??= new LockingCoordinator(RuntimeState);

        public RecordingPower Power { get; } = new();

        public RecordingLock Lock { get; } = new();

        public RecordingDesktop Desktop { get; } = new();

        public RecordingWake Wake { get; } = new();

        public ShellHostChannel Host { get; } = new();

        public MaintenanceMode Maintenance { get; }

        public MachineCommandHandler Handler { get; }

        private LockingCoordinator? coordinator;

        public Task<SessionEnforcementResult> HandleAsync(string type, Dictionary<string, string>? payload = null) =>
            Handler.HandleAsync(new DeviceCommandDto(Guid.NewGuid(), type, Now, payload ?? []), CancellationToken.None);
    }

    private sealed class LockingCoordinator(IAgentRuntimeStateStore runtimeState) : ISessionEnforcementCoordinator
    {
        public int Locks { get; private set; }

        public Task<SessionEnforcementResult> LockAsync(Guid? sessionId, CancellationToken cancellationToken)
        {
            Locks++;
            runtimeState.MarkLocked(Now);
            return Task.FromResult(SessionEnforcementResult.Accepted("Workstation locked.", DeviceCommandOutcomeNames.WorkstationLocked));
        }

        public Task<SessionEnforcementResult> UnlockAsync(SessionLeaseDto lease, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SessionEnforcementResult> RefreshLeaseAsync(SessionLeaseDto lease, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CountingProtection : IProtectionEnforcer
    {
        public int Applies { get; private set; }

        public int Releases { get; private set; }

        public int Refreshes { get; private set; }

        public IReadOnlyList<BlockedWindowRuleDto> BlockedWindows => [];

        public IReadOnlyList<string> ClearAfterSession => [];

        public ProtectionProfileDto Profile => new(0, false, false, false, false, [], [], [], ClearAfterSession);

        public Task ApplyAsync(CancellationToken cancellationToken)
        {
            Applies++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            Releases++;
            return Task.CompletedTask;
        }

        public Task SyncAsync(int serverVersion, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SessionEnforcementResult> RefreshAsync(CancellationToken cancellationToken)
        {
            Refreshes++;
            return Task.FromResult(SessionEnforcementResult.Accepted("Protection profile v3.", DeviceCommandOutcomeNames.ProtectionApplied));
        }
    }

    internal sealed class RecordingLock : IWorkstationLockController
    {
        public int Locks { get; private set; }

        public int Unlocks { get; private set; }

        public Task<WorkstationLockOutcome> LockAsync(CancellationToken cancellationToken)
        {
            Locks++;
            return Task.FromResult(new WorkstationLockOutcome(["task manager disabled"]));
        }

        public Task<WorkstationLockOutcome> UnlockAsync(CancellationToken cancellationToken)
        {
            Unlocks++;
            return Task.FromResult(new WorkstationLockOutcome(["task manager restored"]));
        }
    }

    internal sealed class RecordingDesktop : IMaintenanceDesktop
    {
        public bool AlreadyOpen { get; set; }

        public bool Fails { get; set; }

        public int Opened { get; private set; }

        public int Closed { get; private set; }

        public bool Open()
        {
            if (Fails)
            {
                throw new InvalidOperationException("No interactive user session is available.");
            }

            if (AlreadyOpen)
            {
                return false;
            }

            Opened++;
            return true;
        }

        public void Close() => Closed++;
    }

    private sealed class RecordingPower : IMachinePowerController
    {
        public void Cancel()
        {
        }

        public List<(MachinePowerAction Action, TimeSpan Delay, string Reason)> Scheduled { get; } = [];

        public void Schedule(MachinePowerAction action, TimeSpan delay, string reason) => Scheduled.Add((action, delay, reason));
    }

    private sealed class RecordingWake : IWakeOnLanSender
    {
        public List<(PhysicalAddress Mac, IPAddress Broadcast)> Sent { get; } = [];

        public Task SendAsync(PhysicalAddress mac, IPAddress broadcast, CancellationToken cancellationToken)
        {
            Sent.Add((mac, broadcast));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedNetworkIdentity(NetworkIdentity? current) : INetworkIdentityProvider
    {
        public NetworkIdentity? Current { get; } = current;
    }
}
