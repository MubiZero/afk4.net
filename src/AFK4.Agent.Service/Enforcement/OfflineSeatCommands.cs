using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Офлайн-аренда от Панели по сети клуба (docs/roadmap/offline-front-desk.md): открыть ПК гостю,
/// продлить его сессию или запереть, когда интернета нет. Пока у агента есть связь с платформой,
/// распоряжается она — офлайн-аренда тогда отклоняется.
/// </summary>
public sealed class OfflineSeatCommands(
    ISessionEnforcementCoordinator coordinator,
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IOfflineGraceState graceState,
    TimeProvider timeProvider,
    IShellStateSignal? shellStateSignal = null)
{
    /// <summary>
    /// Сколько агент должен молчать с платформой, чтобы считать себя без связи. Две минуты — с запасом
    /// больше трёх пропущенных сердцебиений: короткий сбой сети не повод отдавать ПК Панели.
    /// </summary>
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<OfflineSeatResponse> ApplyAsync(SessionLeaseDto lease, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var refusal = Refusal(lease);
            if (refusal is not null)
            {
                return Response(accepted: false, refusal);
            }

            var current = leaseStore.Current;
            var sameSession = current?.SessionId == lease.SessionId && runtimeStateStore.Current.SessionRuns;
            var result = lease.State == OfflineLeaseSigning.EndedState
                ? await coordinator.LockAsync(lease.SessionId, cancellationToken)
                : sameSession
                    ? await coordinator.RefreshLeaseAsync(lease, cancellationToken)
                    : await coordinator.UnlockAsync(lease, cancellationToken);
            shellStateSignal?.Notify();

            var accepted = string.Equals(result.Status, "Accepted", StringComparison.Ordinal);
            return Response(accepted, accepted ? null : result.Message);
        }
        finally
        {
            gate.Release();
        }
    }

    private string? Refusal(SessionLeaseDto lease)
    {
        if (!OfflineLeaseSigning.TryGetKeyId(lease.SignatureAlgorithm, out _))
        {
            return "Only offline leases are accepted over the club network.";
        }

        var lastContact = graceState.LastSuccessfulContactUtc;
        if (lastContact is null)
        {
            return "This PC has never reached the platform; it does not trust offline leases yet.";
        }

        if (timeProvider.GetUtcNow() - lastContact.Value < OfflineAfter)
        {
            return "This PC is online; the platform decides.";
        }

        var current = leaseStore.Current;
        var state = runtimeStateStore.Current;
        if (current is not null && state.SessionRuns && current.SessionId != lease.SessionId)
        {
            return "Another session is running on this PC.";
        }

        // Номер растёт с каждой арендой одной сессии: повтор старой — или перехваченной в сети —
        // не продлит и не откроет ПК заново.
        if (current is not null && current.SessionId == lease.SessionId && lease.Sequence <= current.Sequence)
        {
            return "This lease is not newer than the one the PC holds.";
        }

        // Запереть можно только ту сессию, что идёт: «конец» чужой сессии ничего не значит.
        if (lease.State == OfflineLeaseSigning.EndedState && (current is null || current.SessionId != lease.SessionId))
        {
            return "No such session is running on this PC.";
        }

        return null;
    }

    private OfflineSeatResponse Response(bool accepted, string? reason)
    {
        var state = runtimeStateStore.Current;
        return new OfflineSeatResponse(accepted, reason, state.IsLocked, state.ActiveSessionId, state.LeaseExpiresAtUtc);
    }
}
