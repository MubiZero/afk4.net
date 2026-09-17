using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed record GuestSeatUpdateVerdict(bool CanInstall, string Message);

public interface IGuestSeatUpdateGuard
{
    GuestSeatUpdateVerdict Evaluate(string component);
}

/// <summary>
/// Свободно ли место, чтобы обновлять эту машину.
///
/// Обновление агента и оболочки не тихое: установщик подменяет файлы и перезапускает службу, а
/// вместе с ней и киоск. Прилететь это может в любую минуту — проверка обновлений идёт по
/// расписанию, — и до сих пор единственную защиту имело приложение клуба: у кассира спрашивали,
/// можно ли закрыться. За игровым ПК так не спрашивают никого: гость просто терял игру посреди
/// оплаченного часа.
///
/// Ждать здесь ничего не стоит: место освободится, и следующая же проверка поставит обновление.
/// </summary>
public sealed class GuestSeatUpdateGuard(IAgentRuntimeStateStore runtimeStateStore) : IGuestSeatUpdateGuard
{
    public GuestSeatUpdateVerdict Evaluate(string component)
    {
        if (!InterruptsPlayer(component))
        {
            return new GuestSeatUpdateVerdict(true, "Update does not interrupt a player.");
        }

        var state = runtimeStateStore.Current;
        if (state.IsLocked)
        {
            return new GuestSeatUpdateVerdict(true, "Seat is free.");
        }

        // Место отдано гостю — даже если подписанная аренда уже кончилась, а машина держится на
        // льготном окне из-за обрыва связи. Пока экран не заперт, за ним кто-то сидит.
        var seat = state.ActiveSessionId is { } sessionId
            ? $"session {sessionId:D}"
            : "an unlocked seat";

        return new GuestSeatUpdateVerdict(
            false,
            $"Deferred: {seat} is in progress on this workstation. The update installs once the seat is free.");
    }

    private static bool InterruptsPlayer(string component)
    {
        return component is UpdateComponentNames.AgentService or UpdateComponentNames.PlayerShell;
    }
}
