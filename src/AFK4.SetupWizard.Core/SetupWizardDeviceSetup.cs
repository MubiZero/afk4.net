using AFK4.Shared.Contracts.Install;
using AFK4.SetupWizard.Core.Kiosk;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Всё, что делается с ПК после регистрации на платформе: настройка агента на диск, приложение
/// роли, киоск и запуск службы. Одна последовательность на два пути — окно мастера и тихую
/// установку по коду: разойдись они, одна из дорог однажды поставила бы ПК без киоска или без
/// агента, и заметили бы это только в зале.
/// </summary>
public sealed class SetupWizardDeviceSetup(
    ISetupWizardBootstrapWriter bootstrapWriter,
    ISetupWizardCompletionAction completionAction,
    ISetupWizardShellProvisioner shellProvisioner,
    ISetupWizardShellProvisioner operatorProvisioner,
    ISetupWizardOperatorLauncher operatorLauncher,
    KioskProvisioner? kiosk = null)
{
    /// <summary>Регистрация прошла, а настройка на эту машину не записалась.</summary>
    public const string LocalConfigWriteFailedCode = "wizard_local_config_write_failed";

    /// <summary>Приложение встало, а служба агента не запустилась.</summary>
    public const string AgentStartFailedStatus = "agent_start_failed";

    public bool KioskInstalled => kiosk?.IsInstalled ?? false;

    /// <summary>
    /// Настройка агента — на диск и в переменные среды.
    ///
    /// Запись конфигурации — тоже работа с системой: файлы под %ProgramData%, ужесточение прав через
    /// icacls, машинные переменные среды с рассылкой WM_SETTINGCHANGE. В потоке окна она
    /// подмораживала мастер ровно перед самой долгой частью — установкой.
    /// </summary>
    public async Task WriteBootstrapAsync(InstallEnrollResponse response, string role)
    {
        var bootstrap = new SetupWizardBootstrapConfig(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            response.CredentialId,
            response.CredentialSecret,
            role,
            response.ApiBaseUrl,
            response.UpdateChannel,
            response.LeaseSigningPublicKeyPem,
            response.UpdatePackageSigningPublicKeyPem);
        try
        {
            await Task.Run(() => bootstrapWriter.Write(bootstrap), CancellationToken.None);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Регистрация на платформе уже прошла, а настройка на машину не легла — чаще всего
            // из-за прав. Под общим «не удалось зарегистрировать» человек искал бы причину в
            // сети и в платформе, где её нет. Повтор при этом безопасен: платформа опознаёт ту
            // же машину по её ключу и возвращается на то же устройство.
            SetupWizardStartupLog.Write(
                "Device was enrolled with the platform, but writing the local configuration failed.",
                exception);
            throw new SetupWizardApiException(LocalConfigWriteFailedCode, exception.Message, remainingAttempts: null);
        }
    }

    /// <summary>
    /// Ставит приложение роли и поднимает агента.
    ///
    /// Всё это — синхронные обращения к системе: msiexec, sc.exe, explorer.exe. На чистой машине
    /// установка идёт минутами, и раньше она шла в потоке окна: мастер переставал реагировать на
    /// перетаскивание, сворачивание и закрытие — со стороны неотличимо от зависшей программы.
    /// Теперь работа уходит в фоновый поток, а окно остаётся живым.
    ///
    /// Токен отмены сюда намеренно не пробрасывается: оборвать msiexec на середине — это не
    /// «отменить», а оставить наполовину установленное приложение. Ждём до конца и пишем исход
    /// в журнал, даже если мост со стороны интерфейса уже отложил ожидание.
    /// </summary>
    public Task<WizardShellOutcome> FinalizeForRoleAsync(string role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => FinalizeForRole(role), CancellationToken.None);
    }

    public WizardShellOutcome FinalizeForRole(string role)
    {
        // Each role installs its own bundled app: gaming PCs get the Player Shell, cashier/manager
        // workstations get the Organization Admin. Roles with no app just start the agent.
        var provisioner = role switch
        {
            DeviceRoleNames.GamingPc => shellProvisioner,
            DeviceRoleNames.ManagerWorkstation => operatorProvisioner,
            _ => null
        };

        if (provisioner is null)
        {
            return StartAgentService(new WizardShellOutcome("skipped", null, null));
        }

        // msiexec runs synchronously and can take minutes on a fresh PC — log the outcome so a
        // result that arrives after the JS bridge timeout is not silent.
        var result = provisioner.Provision();
        if (result.Status == ShellProvisionStatus.Failed)
        {
            // Do NOT start the agent / mark ready — the finish screen shows an error + retry.
            SetupWizardStartupLog.Write(
                $"{role} app install failed (exitCode={result.ExitCode}): {result.Message}");
            return new WizardShellOutcome("failed", result.ExitCode, result.Message);
        }

        var status = result.Status == ShellProvisionStatus.AlreadyPresent ? "already_present" : "installed";
        SetupWizardStartupLog.Write($"{role} app install {status} (exitCode={result.ExitCode}).");

        // Киоск — после того как оболочка встала: учётка с оболочкой, которой нет на диске, после
        // перезагрузки показала бы чёрный экран. И до запуска агента: SID для прав на канал он
        // читает один раз, при старте.
        var kioskOutcome = role == DeviceRoleNames.GamingPc ? ProvisionKiosk() : null;
        var outcome = StartAgentService(new WizardShellOutcome(status, result.ExitCode, null, kioskOutcome));
        if (outcome.Status == AgentStartFailedStatus)
        {
            return outcome;
        }

        // Gaming PCs get their Player Shell launched by the agent service at the lock screen; the
        // Organization Admin has no such trigger, so start it here so the operator doesn't have to click
        // the Start Menu shortcut after enrolling.
        if (role == DeviceRoleNames.ManagerWorkstation)
        {
            operatorLauncher.Launch();
        }

        return outcome;
    }

    /// <summary>
    /// Поднять службу агента. Без неё машина зарегистрирована и настроена, но не работает: не
    /// шлёт сердцебиение, не запирается, не открывается гостю.
    ///
    /// Раньше отказ отсюда вылетал исключением и доезжал до человека как «не удалось
    /// зарегистрировать устройство» — хотя регистрация прошла, а настройка легла. Искать причину
    /// он шёл в сеть и в платформу, где её нет.
    /// </summary>
    private WizardShellOutcome StartAgentService(WizardShellOutcome installOutcome)
    {
        try
        {
            completionAction.Complete();
            return installOutcome;
        }
        catch (Exception exception)
        {
            SetupWizardStartupLog.Write("Agent service could not be started after enrollment.", exception);
            return new WizardShellOutcome(AgentStartFailedStatus, installOutcome.ExitCode, exception.Message, installOutcome.Kiosk);
        }
    }

    /// <summary>
    /// Учётка игрока, автовход и оболочка вместо проводника (спека оболочки, §6.1). Сорвалось —
    /// ПК всё равно работает, как до киоска: оболочку поднимет агент в текущей учётке. Поэтому
    /// агента запускаем в любом случае, а экран «Готово» говорит, что киоска нет, и даёт повтор.
    /// </summary>
    private WizardKioskOutcome? ProvisionKiosk()
    {
        if (kiosk is null)
        {
            return null;
        }

        try
        {
            var sid = kiosk.Provision(AgentBootstrapValues.PlayerShellExecutablePath());
            SetupWizardStartupLog.Write($"Kiosk ready: player account {sid} signs in on its own after a restart.");
            return new WizardKioskOutcome("ready", null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetupWizardStartupLog.Write("The kiosk could not be set up.", exception);
            return new WizardKioskOutcome("failed", exception.Message);
        }
    }

    /// <summary>
    /// «Снять киоск»: вернуть проводник и настройки входа, удалить учётку игрока — и перезапустить
    /// агента, чтобы канал с оболочкой снова пускал любого вошедшего, а не удалённую учётку.
    /// </summary>
    /// <returns>Остался ли киоск на ПК.</returns>
    public bool RemoveKiosk()
    {
        if (kiosk is null)
        {
            return false;
        }

        kiosk.Remove();
        SetupWizardStartupLog.Write("Kiosk removed: the player account and autologon are gone.");
        completionAction.Complete();
        return kiosk.IsInstalled;
    }
}

public sealed record WizardShellOutcome(string Status, int? ExitCode, string? Message, WizardKioskOutcome? Kiosk = null);

/// <summary>Киоск на игровом ПК: ready — войдёт в учётку игрока после перезагрузки; failed — нет.</summary>
public sealed record WizardKioskOutcome(string Status, string? Message);
