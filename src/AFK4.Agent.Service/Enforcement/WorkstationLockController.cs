namespace AFK4.Agent.Service.Enforcement;

/// <summary>
/// Что агент может запереть на машине из службы, и что честно говорит о результате.
///
/// Раньше здесь были две строки в журнал и <c>Task.CompletedTask</c>: сервер получал «заблокировано»
/// на машине, где не запиралось ничего. Теперь агент ставит машинные политики, которые может
/// поставить только служба, и докладывает, что именно получилось.
///
/// Этого мало для полного киоска, и притворяться обратным нельзя: перехват Win, Alt+Tab и
/// удержание окна поверх остальных должны жить в интерактивном процессе — служба сидит в нулевой
/// сессии и хуков на пользовательский рабочий стол ставить не может. Эта половина — за оболочкой
/// игрока, вместе с её переписыванием.
/// </summary>
public sealed class WorkstationLockController(
    IMachinePolicyStore policyStore,
    ILogger<WorkstationLockController> logger) : IWorkstationLockController
{
    /// <summary>Диспетчер задач: и Ctrl+Shift+Esc, и пункт на экране Ctrl+Alt+Del.</summary>
    private const string DisableTaskManagerPolicy = "DisableTaskMgr";

    public Task<WorkstationLockOutcome> LockAsync(CancellationToken cancellationToken)
    {
        if (!policyStore.IsSupported)
        {
            logger.LogWarning(
                "Workstation lock requested, but machine policies are unavailable on this platform: nothing was enforced.");
            return Task.FromResult(WorkstationLockOutcome.Nothing);
        }

        var enforced = new List<string>();
        if (policyStore.Set(DisableTaskManagerPolicy, 1))
        {
            enforced.Add("task manager disabled");
        }

        var outcome = new WorkstationLockOutcome(enforced);
        logger.LogInformation("Workstation locked by Agent enforcement coordinator: {Enforced}.", outcome.Describe());
        return Task.FromResult(outcome);
    }

    public Task<WorkstationLockOutcome> UnlockAsync(CancellationToken cancellationToken)
    {
        if (!policyStore.IsSupported)
        {
            return Task.FromResult(WorkstationLockOutcome.Nothing);
        }

        var released = new List<string>();
        if (policyStore.Remove(DisableTaskManagerPolicy))
        {
            released.Add("task manager restored");
        }

        var outcome = new WorkstationLockOutcome(released);
        logger.LogInformation("Workstation unlocked by Agent enforcement coordinator: {Released}.", outcome.Describe());
        return Task.FromResult(outcome);
    }
}
