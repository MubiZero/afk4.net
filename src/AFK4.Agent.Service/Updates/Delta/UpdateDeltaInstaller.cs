using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates.Delta;

/// <summary>Скачанное и проверенное обновление, готовое к подмене.</summary>
public sealed record UpdateDeltaPreparation(UpdateDeltaPlan Plan, StagedUpdate Staged);

/// <summary>
/// Дельта-путь обновления компонента. Любой отказ на этом пути (нет манифеста, не прошла проверка,
/// нечего или невыгодно качать, тела не скачались, подмена не удалась и откатилась) возвращает null —
/// координатор ставит целый MSI, как без дельты. Включается <see cref="AgentOptions.UpdateDeltaEnabled"/>.
/// </summary>
public sealed class UpdateDeltaInstaller(
    ILogger<UpdateDeltaInstaller> logger,
    UpdateManifestClient manifestClient,
    IUpdateRootResolver rootResolver,
    UpdateBlobFetcher blobFetcher,
    IUpdateInstallStateStore stateStore,
    IAgentRestartScheduler restartScheduler,
    IMachineEnvironment machineEnvironment,
    IOptions<AgentOptions> options,
    TimeProvider timeProvider,
    IOrganizationAdminProcessLauncher? organizationAdminProcessLauncher = null)
{
    private static readonly string[] Components =
    [
        UpdateComponentNames.AgentService,
        UpdateComponentNames.PlayerShell,
        UpdateComponentNames.OrganizationAdmin
    ];

    public bool IsEnabled => options.Value.UpdateDeltaEnabled;

    public async Task<UpdateDeltaPreparation?> PrepareAsync(
        ComponentUpdateInstructionDto instruction,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled || instruction.Manifest is null || ComponentVersionVariables.For(instruction.Component) is null)
        {
            return null;
        }

        try
        {
            var manifest = await manifestClient.FetchAsync(instruction, cancellationToken);
            if (!manifest.IsValid)
            {
                logger.LogWarning("Update manifest for {Component} {Version} rejected: {Reason}. Using the full package.",
                    instruction.Component, instruction.Version, manifest.Message);
                return null;
            }

            var plan = await UpdateDeltaPlanner.PlanAsync(manifest.Document!, rootResolver, cancellationToken);
            if (plan is null || !plan.IsWorthApplying(instruction.SizeBytes, options.Value.UpdateDeltaMaxDownloadRatio))
            {
                logger.LogInformation(
                    "Delta for {Component} {Version} not used ({Reason}). Using the full package.",
                    instruction.Component, instruction.Version,
                    plan is null ? "an install folder is missing" : $"{plan.ToFetch.Count} files, {plan.DownloadBytes} of {instruction.SizeBytes} bytes");
                return null;
            }

            var staged = await blobFetcher.FetchAsync(plan, StageDirectory(instruction), cancellationToken);
            return new UpdateDeltaPreparation(plan, staged);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException
            or InvalidDataException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Delta download for {Component} {Version} failed. Using the full package.",
                instruction.Component, instruction.Version);
            return null;
        }
    }

    /// <returns>Итог установки или null, если подмена не удалась и откатилась: тогда ставится целый MSI.</returns>
    public async Task<UpdateInstallResult?> ApplyAsync(
        ComponentUpdateInstructionDto instruction,
        UpdateDeltaPreparation preparation,
        string installedVersion,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.BeginInstallAsync(
            instruction,
            new DownloadedUpdateArtifact(instruction, preparation.Staged.StageDirectory, preparation.Plan.DownloadBytes),
            timeProvider.GetUtcNow(),
            cancellationToken);
        state = state.WithInstalledVersion(installedVersion);
        await stateStore.SaveAsync(state, cancellationToken);

        try
        {
            await UpdateDeltaApplier.ApplyAsync(
                preparation.Staged, instruction.Component, instruction.Version, JournalPath(instruction.Component), cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Delta apply for {Component} {Version} failed and was rolled back. Using the full package.",
                instruction.Component, instruction.Version);
            await stateStore.SaveAsync(
                state.WithStatus(UpdateStatusNames.Failed, $"Delta apply rolled back: {exception.Message} Installing the full package.", timeProvider.GetUtcNow()),
                cancellationToken);
            return null;
        }

        machineEnvironment.Set(ComponentVersionVariables.For(instruction.Component)!, instruction.Version);
        var message = $"Delta applied: {preparation.Plan.ToFetch.Count} files, {preparation.Plan.DownloadBytes} bytes downloaded instead of {instruction.SizeBytes}.";
        var installed = state
            .WithInstalledVersion(instruction.Version)
            .WithStatus(UpdateStatusNames.Installed, message, timeProvider.GetUtcNow());
        await stateStore.SaveAsync(installed, cancellationToken);
        TryDeleteDirectory(preparation.Staged.StageDirectory);

        if (instruction.Component == UpdateComponentNames.OrganizationAdmin && organizationAdminProcessLauncher is not null)
        {
            await organizationAdminProcessLauncher.ScheduleAfterRestartAsync(instruction, cancellationToken);
        }

        // Версия компонента живёт в машинной переменной, которую служба читает при старте: без
        // перезапуска агент сообщил бы платформе старую версию, и та предложила бы обновление снова.
        var restart = await restartScheduler.ScheduleRestartAsync(instruction, installed, cancellationToken);
        return restart.Succeeded
            ? UpdateInstallResult.Success(restart.IsRequired ? $"{message} {restart.Message}" : message)
            : UpdateInstallResult.Failed(restart.Message);
    }

    /// <summary>Разбор журналов подмены при старте службы: доубрать старые копии или вернуть оборванную подмену.</summary>
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        foreach (var component in Components)
        {
            var state = await UpdateDeltaApplier.RecoverAsync(JournalPath(component), cancellationToken);
            if (state == UpdateDeltaJournal.Applying)
            {
                logger.LogWarning("An interrupted delta update of {Component} was rolled back at startup.", component);
            }
        }
    }

    private string JournalPath(string component) =>
        Path.Combine(options.Value.UpdateStateDirectory, $"delta-journal-{component}.json");

    private string StageDirectory(ComponentUpdateInstructionDto instruction) =>
        Path.Combine(options.Value.UpdateStagingDirectory, "delta", $"{instruction.Component}-{instruction.UpdatePackageId:N}");

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
