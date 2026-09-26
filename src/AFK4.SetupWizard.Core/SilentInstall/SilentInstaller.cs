using System.Net;
using System.Net.Http;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core.SilentInstall;

/// <summary>
/// Тихая установка игрового ПК по коду — то же, что делает окно мастера, только без человека:
/// регистрация, настройка агента, оболочка, киоск и запуск службы. Каждый шаг пишется в журнал
/// мастера; сам код — никогда.
/// </summary>
public sealed class SilentInstaller(
    IInstallCodeEnrollmentClient apiClient,
    IDeviceKeyStore deviceKeyStore,
    SetupWizardMachineInfo machineInfo,
    SetupWizardDeviceSetup deviceSetup,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    /// <summary>
    /// Паузы между попытками достучаться до платформы: около восьми минут в сумме. Скрипт
    /// развёртывания часто запускают сразу после образа, когда сеть на ПК ещё поднимается.
    /// </summary>
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(60)
    ];

    public async Task<int> RunAsync(SilentInstallOptions options, CancellationToken cancellationToken)
    {
        SetupWizardStartupLog.Write(options.SeatName is null
            ? $"Silent install started on {machineInfo.MachineName}: the seat is looked up by the PC name."
            : $"Silent install started on {machineInfo.MachineName} for seat '{options.SeatName}'.");

        var publicKey = await deviceKeyStore.GetOrCreatePublicKeyPemAsync(cancellationToken);
        var request = new InstallCodeEnrollRequest(
            options.InstallCode,
            options.SeatName,
            DisplayName: null,
            machineInfo.MachineName,
            publicKey);

        InstallEnrollResponse response;
        try
        {
            response = await EnrollWithRetriesAsync(request, cancellationToken);
        }
        catch (Exception exception) when (IsRefusal(exception))
        {
            var reason = exception is SetupWizardApiException refusal ? refusal.Code : exception.Message;
            SetupWizardStartupLog.Write($"Silent install refused by the platform: {reason}. Retrying with the same code will not help.");
            return SilentInstallExitCodes.CodeRefused;
        }
        catch (Exception exception) when (IsTransient(exception))
        {
            SetupWizardStartupLog.Write("Silent install gave up: the platform stayed unreachable.", exception);
            return SilentInstallExitCodes.PlatformUnreachable;
        }

        SetupWizardStartupLog.Write(
            $"Silent install enrolled device {response.DeviceId:D} ({response.EnrollmentState}); "
            + (response.AssignedSeatName is null
                ? "no seat matched — assign one in the AFK4.net Panel."
                : $"seat '{response.AssignedSeatName}'."));

        try
        {
            await deviceSetup.WriteBootstrapAsync(response, DeviceRoleNames.GamingPc);
        }
        catch (SetupWizardApiException)
        {
            return SilentInstallExitCodes.SetupFailed;
        }

        var outcome = await deviceSetup.FinalizeForRoleAsync(DeviceRoleNames.GamingPc, cancellationToken);
        if (outcome.Status is "failed" or SetupWizardDeviceSetup.AgentStartFailedStatus)
        {
            SetupWizardStartupLog.Write($"Silent install stopped: {outcome.Status} (exitCode={outcome.ExitCode}) {outcome.Message}");
            return SilentInstallExitCodes.SetupFailed;
        }

        if (outcome.Kiosk?.Status == "failed")
        {
            SetupWizardStartupLog.Write($"Silent install finished without the kiosk: {outcome.Kiosk.Message}");
            return SilentInstallExitCodes.KioskFailed;
        }

        SetupWizardStartupLog.Write("Silent install finished: the player screen starts after the PC restarts.");
        return SilentInstallExitCodes.Installed;
    }

    private async Task<InstallEnrollResponse> EnrollWithRetriesAsync(
        InstallCodeEnrollRequest request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await apiClient.EnrollByCodeAsync(request, cancellationToken);
            }
            catch (Exception exception) when (
                IsTransient(exception) && !cancellationToken.IsCancellationRequested && attempt < RetryDelays.Count)
            {
                SetupWizardStartupLog.Write(
                    $"Silent install: the platform did not answer (attempt {attempt + 1}); retrying in {RetryDelays[attempt].TotalSeconds:0} s. {exception.Message}");
                await (delay ?? Task.Delay)(RetryDelays[attempt], cancellationToken);
            }
        }
    }

    /// <summary>
    /// Платформа ответила отказом: с причиной или без неё (клуб приостановлен — ответ без кода),
    /// но это ответ, и повтор его не изменит.
    /// </summary>
    private static bool IsRefusal(Exception exception) => exception switch
    {
        SetupWizardApiException => true,
        HttpRequestException { StatusCode: { } status } =>
            (int)status is >= 400 and < 500 && status != HttpStatusCode.TooManyRequests,
        _ => false
    };

    /// <summary>
    /// Стоит ли повторять: нет связи, сервер упал или просит подождать. Отказ с причиной —
    /// <see cref="SetupWizardApiException"/> — не повторяется: код от этого верным не станет.
    /// </summary>
    private static bool IsTransient(Exception exception) => exception switch
    {
        SetupWizardApiException => false,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: var status } => status == HttpStatusCode.TooManyRequests || (int)status! >= 500,
        TaskCanceledException => true,
        _ => false
    };
}
