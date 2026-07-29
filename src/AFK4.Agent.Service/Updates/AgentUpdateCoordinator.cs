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
    IOrganizationAdminUpdateReadiness? organizationAdminReadiness = null) : IAgentUpdateCoordinator
{
    private readonly Lock attemptedRolloutsLock = new();

    private readonly HashSet<Guid> attemptedRolloutIds = [];

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

                await ReportStatusAsync(
                    instruction,
                    installedVersion,
                    UpdateStatusNames.Installing,
                    "Starting update installer.",
                    cancellationToken);
                var installResult = await installer.InstallAsync(instruction, artifact, cancellationToken);
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
            failedCount);
    }

    private bool TryMarkAttempted(Guid rolloutId)
    {
        lock (attemptedRolloutsLock)
        {
            return attemptedRolloutIds.Add(rolloutId);
        }
    }

    private void ClearAttempted(Guid rolloutId)
    {
        lock (attemptedRolloutsLock)
        {
            attemptedRolloutIds.Remove(rolloutId);
        }
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
