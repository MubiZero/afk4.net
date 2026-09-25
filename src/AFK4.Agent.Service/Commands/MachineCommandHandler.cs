using System.Net;
using System.Net.Sockets;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Network;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Commands;

/// <summary>Команды администратора самой машине (спека оболочки, §5.8): питание, пробуждение соседа, обслуживание, экран.</summary>
public interface IMachineCommandHandler
{
    bool Handles(string commandType);

    Task<SessionEnforcementResult> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken);
}

public sealed class MachineCommandHandler(
    IAgentRuntimeStateStore runtimeStateStore,
    IMaintenanceMode maintenanceMode,
    IMachinePowerController powerController,
    IWakeOnLanSender wakeOnLanSender,
    INetworkIdentityProvider networkIdentity,
    IShellHostChannel hostChannel,
    ILogger<MachineCommandHandler> logger) : IMachineCommandHandler
{
    /// <summary>Сколько Windows ждёт перед перезагрузкой: ответ серверу уходит за это время.</summary>
    public static readonly TimeSpan PowerDelay = TimeSpan.FromSeconds(10);

    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        DeviceCommandTypeNames.Reboot,
        DeviceCommandTypeNames.Shutdown,
        DeviceCommandTypeNames.WakeNeighbor,
        DeviceCommandTypeNames.MaintenanceOn,
        DeviceCommandTypeNames.MaintenanceOff,
        DeviceCommandTypeNames.SignOut,
        DeviceCommandTypeNames.Message,
        DeviceCommandTypeNames.PolicyRefresh
    };

    public bool Handles(string commandType) => Types.Contains(commandType);

    public async Task<SessionEnforcementResult> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        switch (command.Type.ToLowerInvariant())
        {
            case DeviceCommandTypeNames.Reboot:
                return SchedulePower(MachinePowerAction.Restart);
            case DeviceCommandTypeNames.Shutdown:
                return SchedulePower(MachinePowerAction.Shutdown);
            case DeviceCommandTypeNames.WakeNeighbor:
                return await WakeNeighbourAsync(command, cancellationToken);
            case DeviceCommandTypeNames.MaintenanceOn:
                return await maintenanceMode.EnterAsync(cancellationToken);
            case DeviceCommandTypeNames.MaintenanceOff:
                return await maintenanceMode.LeaveAsync(cancellationToken);
            case DeviceCommandTypeNames.SignOut:
                return PostToHost(new ShellPipeCommandDto(command.CommandId, DeviceCommandTypeNames.SignOut));
            case DeviceCommandTypeNames.Message:
                return PostToHost(new ShellPipeCommandDto(
                    command.CommandId,
                    DeviceCommandTypeNames.Message,
                    command.Payload.GetValueOrDefault("text")));
            default:
                // Профилей защиты ещё нет (P5): принять и честно сказать, что делать было нечего.
                return SessionEnforcementResult.Accepted(
                    "This PC has no protection profile yet; nothing to refresh.",
                    DeviceCommandOutcomeNames.NothingToRefresh);
        }
    }

    private SessionEnforcementResult SchedulePower(MachinePowerAction action)
    {
        // Сервер не шлёт питание на занятый ПК. Если агент видит сессию, они разошлись — и выключать
        // чужую игру по устаревшему решению нельзя.
        if (runtimeStateStore.Current.SessionRuns)
        {
            return SessionEnforcementResult.Rejected("A session is running on this PC.", DeviceCommandOutcomeNames.SessionInProgress);
        }

        var restart = action == MachinePowerAction.Restart;
        powerController.Schedule(
            action,
            PowerDelay,
            restart ? "AFK4: the club is restarting this PC." : "AFK4: the club is shutting this PC down.");
        logger.LogInformation("Windows {Action} scheduled in {Delay}.", action, PowerDelay);

        return SessionEnforcementResult.Accepted(
            $"Windows {(restart ? "restarts" : "shuts down")} in {PowerDelay.TotalSeconds:0} seconds.",
            restart ? DeviceCommandOutcomeNames.RebootScheduled : DeviceCommandOutcomeNames.ShutdownScheduled);
    }

    private async Task<SessionEnforcementResult> WakeNeighbourAsync(DeviceCommandDto command, CancellationToken cancellationToken)
    {
        if (!MagicPacket.TryParseMac(command.Payload.GetValueOrDefault("mac"), out var mac)
            || !IPAddress.TryParse(command.Payload.GetValueOrDefault("broadcast"), out var broadcast)
            || broadcast.AddressFamily != AddressFamily.InterNetwork)
        {
            return SessionEnforcementResult.Rejected("Wake target MAC or broadcast address is not valid.", DeviceCommandOutcomeNames.WakeTargetInvalid);
        }

        // Волшебный пакет не проходит маршрутизатор: если этот ПК уже в другой подсети, сервер выбрал
        // соседа по устаревшим данным, и отправка только изобразила бы пробуждение.
        var own = networkIdentity.Current;
        if (own is not null && !string.Equals(own.BroadcastAddress, broadcast.ToString(), StringComparison.Ordinal))
        {
            return SessionEnforcementResult.Rejected(
                $"This PC is in {own.Subnet}, not in the target's network.",
                DeviceCommandOutcomeNames.WakeTargetInvalid);
        }

        await wakeOnLanSender.SendAsync(mac, broadcast, cancellationToken);
        return SessionEnforcementResult.Accepted(
            $"Wake packet sent to {NetworkMath.FormatMac(mac)} via {broadcast}.",
            DeviceCommandOutcomeNames.WakePacketSent);
    }

    private SessionEnforcementResult PostToHost(ShellPipeCommandDto command)
    {
        return hostChannel.TryPost(new ShellPipeMessage(ShellPipeMessageTypeNames.Command, Command: command))
            ? SessionEnforcementResult.Accepted("Passed to the player screen.", DeviceCommandOutcomeNames.DeliveredToShell)
            : SessionEnforcementResult.Rejected("The player screen is not connected.", DeviceCommandOutcomeNames.ShellNotConnected);
    }
}
