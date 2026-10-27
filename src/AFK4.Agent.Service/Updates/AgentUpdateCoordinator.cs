using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public sealed class AgentUpdateCoordinator(
    ILogger<AgentUpdateCoordinator> logger,
    IAgentUpdateClient updateClient,
    IAgentComponentVersionProvider componentVersionProvider,
    IUpdateArtifactDownloader artifactDownloader,
    IUpdatePackageVerifier packageVerifier,
    IUpdateInstaller installer,
    TimeProvider timeProvider,
    IUpdateAttemptLedger attemptLedger,
    GuestSeatUpdateGuard guestSeatGuard,
    IOrganizationAdminUpdateReadiness? organizationAdminReadiness = null,
    AFK4.Agent.Service.Shell.IPlayerShellUpdateGate? shellUpdateGate = null,
    AFK4.Agent.Service.Updates.Delta.UpdateDeltaInstaller? deltaInstaller = null) : IAgentUpdateCoordinator
{

    public async Task<AgentUpdateExecutionResult> CheckAndApplyUpdatesAsync(CancellationToken cancellationToken)
    {
        var installedComponents = componentVersionProvider.GetInstalledComponents();
        var check = await updateClient.CheckForUpdatesAsync(
            installedComponents,
            timeProvider.GetUtcNow(),
            cancellationToken);

        var offeredCount = 0;
        var appliedCount = 0;
        var failedCount = 0;
        var pendingRestartCount = 0;
        var installedVersionByComponent = installedComponents
            .GroupBy(component => component.Component, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Version, StringComparer.Ordinal);

        foreach (var instruction in check.Updates)
        {
            if (!TryMarkAttempted(instruction.UpdateRolloutId))
            {
                logger.LogInformation(
                    "Skipping already attempted update rollout {UpdateRolloutId} for {Component} {Version}.",
                    instruction.UpdateRolloutId,
                    instruction.Component,
                    instruction.Version);
                continue;
            }

            offeredCount++;
            var installedVersion = installedVersionByComponent.GetValueOrDefault(instruction.Component, "unknown");
            await ReportStatusAsync(
                instruction,
                installedVersion,
                UpdateStatusNames.Offered,
                "Update offered by platform.",
                cancellationToken);

            var seat = guestSeatGuard.Evaluate(instruction.Component);
            if (!seat.CanInstall)
            {
                await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Deferred, seat.Message, cancellationToken);
                ClearAttempted(instruction.UpdateRolloutId);
                continue;
            }

            if (instruction.Component == UpdateComponentNames.OrganizationAdmin && organizationAdminReadiness is not null)
            {
                var readiness = await organizationAdminReadiness.EvaluateAsync(
                    instruction, check.OrganizationAdminPreference, check.ServerTimeUtc, cancellationToken);
                if (!readiness.CanInstall)
                {
                    await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Deferred, readiness.Message, cancellationToken);
                    ClearAttempted(instruction.UpdateRolloutId);
                    continue;
                }
                if (readiness.Status == OrganizationAdminUpdateReadinessNames.ReadyAfterShutdown)
                {
                    await ReportStatusAsync(
                        instruction, installedVersion, UpdateStatusNames.ReadyToInstall, readiness.Message, cancellationToken);
                }
            }

            try
            {
                var deltaResult = await TryDeltaAsync(instruction, installedVersion, cancellationToken);
                if (deltaResult is { Deferred: true })
                {
                    await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Deferred, deltaResult.Message, cancellationToken);
                    ClearAttempted(instruction.UpdateRolloutId);
                    continue;
                }

                if (deltaResult is not null)
                {
                    if (deltaResult.Succeeded)
                    {
                        appliedCount++;
                        await ReportStatusAsync(instruction, instruction.Version, UpdateStatusNames.Installed, deltaResult.Message, cancellationToken);
                    }
                    else
                    {
                        failedCount++;
                        await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Failed, deltaResult.Message, cancellationToken);
                    }

                    continue;
                }

                await ReportStatusAsync(
                    instruction,
                    installedVersion,
                    UpdateStatusNames.Downloading,
                    "Downloading update artifact.",
                    cancellationToken);
                var artifact = await artifactDownloader.DownloadAsync(instruction, cancellationToken);
                await ReportStatusAsync(
                    instruction,
                    installedVersion,
                    UpdateStatusNames.Downloaded,
                    "Update artifact downloaded.",
                    cancellationToken);

                var verification = await packageVerifier.VerifyAsync(instruction, artifact, cancellationToken);
                if (!verification.IsValid)
                {
                    failedCount++;
                    await ReportStatusAsync(
                        instruction,
                        installedVersion,
                        UpdateStatusNames.Failed,
                        verification.Message,
                        cancellationToken);
                    continue;
                }

                if (SeatTakenDuringDownload(instruction) is { } taken)
                {
                    await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Deferred, taken.Message, cancellationToken);
                    ClearAttempted(instruction.UpdateRolloutId);
                    continue;
                }

                await ReportStatusAsync(
                    instruction,
                    installedVersion,
                    UpdateStatusNames.Installing,
                    "Starting update installer.",
                    cancellationToken);
                var installResult = await InstallAsync(instruction, artifact, cancellationToken);
                if (installResult.RestartPending)
                {
                    pendingRestartCount++;
                    await ReportStatusAsync(
                        instruction,
                        installedVersion,
                        UpdateStatusNames.PendingRestart,
                        installResult.Message,
                        cancellationToken);
                    continue;
                }

                if (installResult.Succeeded)
                {
                    appliedCount++;
                    await ReportStatusAsync(
                        instruction,
                        instruction.Version,
                        UpdateStatusNames.Installed,
                        installResult.Message,
                        cancellationToken);
                    continue;
                }

                failedCount++;
                await ReportStatusAsync(
                    instruction,
                    installedVersion,
                    UpdateStatusNames.Failed,
                    installResult.Message,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedCount++;

                // A transient download/network fault should retry on the next scheduled check, not be
                // permanently skipped: clear the attempted mark so the rollout is offered again. The
                // next-check cadence keeps this from becoming a tight retry storm.
                if (IsTransientFailure(exception))
                {
                    ClearAttempted(instruction.UpdateRolloutId);
                }

                await ReportStatusAsync(
                    instruction,
                    installedVersion,
                    UpdateStatusNames.Failed,
                    exception.Message,
                    cancellationToken);
            }
        }

        return new AgentUpdateExecutionResult(
            offeredCount,
            appliedCount,
            failedCount,
            pendingRestartCount);
    }

    /// <summary>
    /// Счёт попыток переживает перезапуск службы: иначе сломанный пакет крутился бы по кругу —
    /// поставили, упало, перезапустились, обнулили память, поставили снова.
    /// </summary>
    // Оболочка — не служба: установщик меняет её файлы, а работающий процесс остаётся прежним до
    // перезагрузки ПК. Пока идёт установка, супервизор её не поднимает; после неё старый процесс
    // закрывается, и супервизор запускает новую сборку. 3010 — файлы заменятся только при
    // перезагрузке: старую оболочку тогда не трогаем.
    private async Task<UpdateInstallResult> InstallAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        CancellationToken cancellationToken) =>
        (await GateShellAsync(instruction, async () => await installer.InstallAsync(instruction, artifact, cancellationToken)))!;

    private async Task<UpdateInstallResult?> GateShellAsync(
        ComponentUpdateInstructionDto instruction,
        Func<Task<UpdateInstallResult?>> install)
    {
        if (instruction.Component != UpdateComponentNames.PlayerShell || shellUpdateGate is null)
        {
            return await install();
        }

        using (shellUpdateGate.Suspend())
        {
            var result = await install();
            if (result is { Succeeded: true, RestartPending: false })
            {
                shellUpdateGate.StopRunningShells();
            }

            return result;
        }
    }

    /// <summary>
    /// Дельта-путь: скачать только изменившиеся файлы и подменить их. null — дельта не применялась
    /// (выключена, нет манифеста, невыгодна, не скачалась или подмена откатилась): ставится целый MSI.
    /// </summary>
    private async Task<UpdateInstallResult?> TryDeltaAsync(
        ComponentUpdateInstructionDto instruction,
        string installedVersion,
        CancellationToken cancellationToken)
    {
        if (deltaInstaller is not { IsEnabled: true } || instruction.Manifest is null)
        {
            return null;
        }

        await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Downloading, "Downloading changed files.", cancellationToken);
        var preparation = await deltaInstaller.PrepareAsync(instruction, cancellationToken);
        if (preparation is null)
        {
            return null;
        }

        await ReportStatusAsync(
            instruction,
            installedVersion,
            UpdateStatusNames.Downloaded,
            $"Changed files downloaded: {preparation.Plan.ToFetch.Count} files, {preparation.Plan.DownloadBytes} bytes.",
            cancellationToken);
        if (SeatTakenDuringDownload(instruction) is { } taken)
        {
            return taken;
        }

        await ReportStatusAsync(instruction, installedVersion, UpdateStatusNames.Installing, "Replacing changed files.", cancellationToken);
        return await GateShellAsync(
            instruction,
            () => deltaInstaller.ApplyAsync(instruction, preparation, installedVersion, cancellationToken));
    }

    /// <summary>
    /// Место проверяли до скачивания, а целый пакет на сети клуба качается минутами: за это время за ПК
    /// мог сесть гость, и установка перезапустила бы ему экран в начале оплаченного часа. Поэтому место
    /// проверяется ещё раз прямо перед установкой. Скачанное не пропадает: следующая проверка
    /// переиспользует его по размеру и хешу.
    /// </summary>
    private UpdateInstallResult? SeatTakenDuringDownload(ComponentUpdateInstructionDto instruction)
    {
        var seat = guestSeatGuard.Evaluate(instruction.Component);
        return seat.CanInstall ? null : UpdateInstallResult.SeatTaken(seat.Message);
    }

    private bool TryMarkAttempted(Guid rolloutId)
    {
        if (!attemptLedger.ShouldAttempt(rolloutId))
        {
            return false;
        }

        attemptLedger.RecordAttempt(rolloutId);

        return true;
    }

    private void ClearAttempted(Guid rolloutId)
    {
        attemptLedger.ForgetAttempt(rolloutId);
    }

    private static bool IsTransientFailure(Exception exception)
    {
        return exception is HttpRequestException or IOException or TimeoutException;
    }

    private Task ReportStatusAsync(
        ComponentUpdateInstructionDto instruction,
        string installedVersion,
        string status,
        string message,
        CancellationToken cancellationToken)
    {
        return updateClient.ReportStatusAsync(
            instruction.UpdateRolloutId,
            instruction.UpdatePackageId,
            instruction.Component,
            installedVersion,
            instruction.Version,
            status,
            message,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}
