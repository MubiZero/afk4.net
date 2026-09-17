using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class SafeUpdateInstaller(
    IUpdateInstallStateStore stateStore,
    IUpdateInstallExecutor executor,
    IUpdateRollbackExecutor rollbackExecutor,
    IAgentRestartScheduler restartScheduler,
    IOptions<AgentOptions> options,
    TimeProvider timeProvider,
    IOrganizationAdminProcessLauncher? organizationAdminProcessLauncher = null) : IUpdateInstaller
{
    public async Task<UpdateInstallResult> InstallAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.BeginInstallAsync(
            instruction,
            artifact,
            timeProvider.GetUtcNow(),
            cancellationToken);

        state = state.WithInstalledVersion(GetInstalledVersion(instruction.Component));
        await stateStore.SaveAsync(state, cancellationToken);

        var installResult = await executor.ExecuteAsync(instruction, artifact, state, cancellationToken);
        if (installResult.RestartPending)
        {
            return await RecordRestartPendingAsync(instruction, state, installResult, cancellationToken);
        }

        if (installResult.Succeeded)
        {
            var installed = state
                .WithInstalledVersion(instruction.Version)
                .WithStatus(UpdateStatusNames.Installed, installResult.Message, timeProvider.GetUtcNow());
            await stateStore.SaveAsync(installed, cancellationToken);

            // Запоминаем пакет, который реально встал: это единственное, на что можно будет
            // откатиться, если следующая версия окажется сломанной.
            await stateStore.SaveLastKnownGoodAsync(
                new LastKnownGoodUpdate(instruction.Component, instruction.Version, artifact.FilePath, timeProvider.GetUtcNow()),
                cancellationToken);

            if (instruction.Component == UpdateComponentNames.OrganizationAdmin && organizationAdminProcessLauncher is not null)
            {
                await organizationAdminProcessLauncher.ScheduleAfterRestartAsync(instruction, cancellationToken);
            }

            var restartResult = await restartScheduler.ScheduleRestartAsync(instruction, installed, cancellationToken);
            if (!restartResult.Succeeded)
            {
                return UpdateInstallResult.Failed(restartResult.Message);
            }

            return restartResult.IsRequired
                ? UpdateInstallResult.Success($"{installResult.Message} {restartResult.Message}")
                : installResult;
        }

        var rollbackStarted = state.WithStatus(
            UpdateStatusNames.RollbackStarted,
            installResult.Message,
            timeProvider.GetUtcNow());
        await stateStore.SaveAsync(rollbackStarted, cancellationToken);

        var knownGood = await stateStore.LoadLastKnownGoodAsync(instruction.Component, cancellationToken);
        var rollbackTarget = UpdateRollbackPlan.ToKnownGood(rollbackStarted, knownGood);
        if (rollbackTarget is null)
        {
            // Откатываться некуда. Раньше в этом месте запускался тот же самый пакет, который
            // только что не установился, и его повторная установка называлась «откатом»: клуб
            // видел «RolledBack» там, где ничего не откатывалось.
            var noTarget = rollbackStarted.WithStatus(
                UpdateStatusNames.Failed,
                $"{installResult.Message} {UpdateRollbackPlan.NoTargetMessage}",
                timeProvider.GetUtcNow());
            await stateStore.SaveAsync(noTarget, cancellationToken);

            return UpdateInstallResult.Failed(noTarget.Message);
        }

        var rollbackResult = await rollbackExecutor.RollbackAsync(rollbackTarget, cancellationToken);
        var finalStatus = rollbackResult.Succeeded
            ? UpdateStatusNames.RolledBack
            : UpdateStatusNames.Failed;
        var finalMessage = $"{installResult.Message} {rollbackResult.Message}";
        await stateStore.SaveAsync(
            rollbackStarted
                .WithInstalledVersion(rollbackResult.Succeeded ? rollbackTarget.TargetVersion : rollbackStarted.InstalledVersion)
                .WithStatus(finalStatus, finalMessage, timeProvider.GetUtcNow()),
            cancellationToken);

        return UpdateInstallResult.Failed(finalMessage);
    }

    /// <summary>
    /// Установленная версия остаётся прежней: до перезагрузки машины работает старая сборка.
    ///
    /// Пакет в «известные хорошие» не записывается — он ещё ни разу не запускался, откатываться
    /// на него было бы откатом в неизвестность. Перезапуск службы тоже не нужен: файлы подменит
    /// перезагрузка, а не рестарт сервиса. А вот приложение клуба, которое закрыли ради
    /// установки, вернуть на экран кассиру нужно в любом случае.
    /// </summary>
    private async Task<UpdateInstallResult> RecordRestartPendingAsync(
        ComponentUpdateInstructionDto instruction,
        UpdateInstallState state,
        UpdateInstallResult installResult,
        CancellationToken cancellationToken)
    {
        await stateStore.SaveAsync(
            state.WithStatus(UpdateStatusNames.PendingRestart, installResult.Message, timeProvider.GetUtcNow()),
            cancellationToken);

        if (instruction.Component == UpdateComponentNames.OrganizationAdmin && organizationAdminProcessLauncher is not null)
        {
            await organizationAdminProcessLauncher.ScheduleAfterRestartAsync(instruction, cancellationToken);
        }

        return installResult;
    }

    private string GetInstalledVersion(string component)
    {
        return component switch
        {
            UpdateComponentNames.AgentService => options.Value.AgentVersion,
            UpdateComponentNames.PlayerShell => options.Value.ShellVersion,
            _ => "unknown"
        };
    }
}
