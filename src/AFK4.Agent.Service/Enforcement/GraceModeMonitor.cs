using AFK4.Agent.Service.Cleanup;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Enforcement;

public interface IGraceModeMonitor
{
    Task EnforceAsync(CancellationToken cancellationToken);
}

public sealed class GraceModeMonitor(
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IWorkstationLockController workstationLockController,
    IOfflineLeaseExtender offlineLeaseExtender,
    TimeProvider timeProvider,
    ILogger<GraceModeMonitor> logger,
    ISessionCleanup? sessionCleanup = null) : IGraceModeMonitor
{
    public async Task EnforceAsync(CancellationToken cancellationToken)
    {
        var lease = leaseStore.Current;
        var now = timeProvider.GetUtcNow();
        if (lease is null)
        {
            await LockAfterRestartIfLeaseLapsedAsync(now, cancellationToken);
            return;
        }

        if (lease.ExpiresAtUtc > now)
        {
            return;
        }

        // The signed lease has lapsed, but if the network only just dropped the customer paid for this
        // time — keep the PC unlocked through the grace window (measured from last contact, not from the
        // last lease refresh). The lease is retained so a reconnect can refresh or reconcile it.
        if (offlineLeaseExtender.ShouldExtend(lease, now))
        {
            EnterGraceMode(lease.SessionId, lease.ExpiresAtUtc, now);
            logger.LogInformation(
                "Session lease {SessionId} lapsed at {ExpiresAtUtc} but is within the offline grace window. Keeping the workstation unlocked.",
                lease.SessionId,
                lease.ExpiresAtUtc);
            return;
        }

        var ended = runtimeStateStore.Current;
        leaseStore.Clear(lease.SessionId);
        runtimeStateStore.MarkLocked(now);
        var outcome = await workstationLockController.LockAsync(cancellationToken);

        logger.LogInformation(
            "Session lease {SessionId} expired at {ExpiresAtUtc}. Workstation locked: {Enforced}.",
            lease.SessionId,
            lease.ExpiresAtUtc,
            outcome.Describe());
        await CleanUpAfterAsync(ended, cancellationToken);
    }

    // Сессия кончилась без сервера — по аренде или после перезапуска службы: следы следующему
    // игроку остаются ровно так же, как после команды «Запереть».
    private async Task CleanUpAfterAsync(AgentRuntimeState ended, CancellationToken cancellationToken)
    {
        if (sessionCleanup is not null && ended.SessionRuns)
        {
            await sessionCleanup.RunAsync(ended.SessionStartedAtUtc, cancellationToken);
        }
    }

    /// <summary>
    /// Служба перезапустилась, пока за ПК сидел гость.
    ///
    /// Просроченную аренду хранилище при загрузке удаляет — она больше не даёт права играть. Но в
    /// состоянии осталось, что машина была отдана гостю, и без этой проверки агент после
    /// перезапуска не запирал её уже никогда: аренды нет, значит и запирать «нечего». ПК оставался
    /// бесплатным, пока не вернётся связь, а при затяжном обрыве это часы.
    ///
    /// Внутри льготного окна (его время теперь тоже переживает перезапуск) машина остаётся
    /// открытой: гость заплатил, а сеть пропала не по его вине.
    /// </summary>
    /// <summary>
    /// Сказать оболочке, что связь потеряна, а сессия продолжается по подписанной аренде. Пишем
    /// только на переходе: монитор проходит каждое сердцебиение, а состояние живёт файлом.
    /// </summary>
    private void EnterGraceMode(Guid sessionId, DateTimeOffset leaseExpiresAtUtc, DateTimeOffset now)
    {
        var state = runtimeStateStore.Current;
        if (string.Equals(state.State, PlayerShellStateNames.Grace, StringComparison.Ordinal) &&
            state.ActiveSessionId == sessionId)
        {
            return;
        }

        runtimeStateStore.Save(AgentRuntimeState.Grace(sessionId, leaseExpiresAtUtc, now, state.SessionStartedAtUtc));
    }

    private async Task LockAfterRestartIfLeaseLapsedAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var state = runtimeStateStore.Current;
        if (state.IsLocked || state.ActiveSessionId is null || state.LeaseExpiresAtUtc is not { } expiresAtUtc)
        {
            return;
        }

        if (expiresAtUtc > now)
        {
            return;
        }

        if (offlineLeaseExtender.WithinGraceWindow(now, expiresAtUtc))
        {
            EnterGraceMode(state.ActiveSessionId.Value, expiresAtUtc, now);
            return;
        }

        runtimeStateStore.MarkLocked(now);
        var outcome = await workstationLockController.LockAsync(cancellationToken);

        logger.LogWarning(
            "Session {SessionId} lease lapsed at {ExpiresAtUtc} and did not survive an Agent restart. Workstation locked: {Enforced}.",
            state.ActiveSessionId,
            expiresAtUtc,
            outcome.Describe());
        await CleanUpAfterAsync(state, cancellationToken);
    }
}
