using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

/// <summary>Отвечает на запросы, которые оболочка присылает по каналу.</summary>
public interface IPlayerShellRequestHandler
{
    Task<ShellPipeReplyDto> HandleAsync(ShellPipeRequestDto request, CancellationToken cancellationToken);
}

public sealed class PlayerShellRequestHandler(
    IProcessPolicyEnforcer processPolicyEnforcer,
    IProcessLauncher processLauncher,
    IAgentRuntimeStateStore runtimeStateStore,
    IAssistanceRequestReporter assistanceRequestReporter,
    TimeProvider timeProvider,
    ILogger<PlayerShellRequestHandler> logger,
    IPlayerSignIn? playerSignIn = null,
    MaintenanceReturn? maintenanceReturn = null,
    AFK4.Agent.Service.Power.IPlayerPresence? presence = null,
    AFK4.Agent.Service.Showcase.IShowcaseImpressions? impressions = null) : IPlayerShellRequestHandler
{
    public const string AppIdPayloadKey = "appId";

    public Task<ShellPipeReplyDto> HandleAsync(ShellPipeRequestDto request, CancellationToken cancellationToken) =>
        request.Type switch
        {
            ShellPipeRequestTypeNames.Launch => LaunchAsync(request, cancellationToken),
            ShellPipeRequestTypeNames.Assist => AssistAsync(request, cancellationToken),
            ShellPipeRequestTypeNames.SignInPin when playerSignIn is not null => playerSignIn.SignInWithPinAsync(request, cancellationToken),
            ShellPipeRequestTypeNames.MaintenanceReturn when maintenanceReturn is not null => maintenanceReturn.ReturnAsync(request, cancellationToken),
            ShellPipeRequestTypeNames.Activity => RecordActivity(request),
            ShellPipeRequestTypeNames.ShowcaseImpression => RecordImpression(request),
            _ => Task.FromResult(Rejected(request, ShellPipeErrorCodeNames.UnknownRequest, $"Unknown request type '{request.Type}'."))
        };

    /// <summary>
    /// «Позвать администратора» уходит на сервер ключом устройства: на запертом экране сессии
    /// нет, и назвать себя игроку нечем — а машина известна всегда.
    /// </summary>
    private async Task<ShellPipeReplyDto> AssistAsync(ShellPipeRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await assistanceRequestReporter.ReportAsync(timeProvider.GetUtcNow(), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            // Оболочке важно знать правду: если стойка о вызове не узнала, показывать
            // «администратор идёт» нельзя.
            logger.LogWarning(exception, "Assistance request did not reach the platform.");
            return Rejected(request, ShellPipeErrorCodeNames.PlatformUnreachable, "The counter could not be reached.");
        }

        return new ShellPipeReplyDto(request.RequestId, Ok: true);
    }

    private async Task<ShellPipeReplyDto> LaunchAsync(ShellPipeRequestDto request, CancellationToken cancellationToken)
    {
        if (!request.Payload.TryGetValue(AppIdPayloadKey, out var appId) || string.IsNullOrWhiteSpace(appId))
        {
            return Rejected(request, ShellPipeErrorCodeNames.InvalidPayload, "Launch request must name an appId.");
        }

        var app = processPolicyEnforcer.FindAllowedLauncherApp(appId);
        if (app is null)
        {
            return Rejected(request, ShellPipeErrorCodeNames.AppNotAllowed, "This app is not on the club's list for this PC.");
        }

        if (!File.Exists(app.ExecutablePath))
        {
            return Rejected(request, ShellPipeErrorCodeNames.AppMissing, "This app is not installed on this PC.");
        }

        // Игра на запертом ПК — бесплатное время. Раньше агент запускал что угодно из списка
        // в любой момент, и проверял это только экран оболочки.
        if (!app.AllowWithoutSession && !SessionRuns())
        {
            return Rejected(request, ShellPipeErrorCodeNames.NoSession, "Apps start only during a session.");
        }

        try
        {
            await processLauncher.LaunchAsync(app.ExecutablePath, app.Arguments, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Launch of {AppId} from {ExecutablePath} failed.", app.AppId, app.ExecutablePath);
            return Rejected(request, ShellPipeErrorCodeNames.LaunchFailed, "The app could not be started.");
        }

        logger.LogInformation("Launched {AppId} at the player's request.", app.AppId);
        return new ShellPipeReplyDto(request.RequestId, Ok: true);
    }

    private Task<ShellPipeReplyDto> RecordActivity(ShellPipeRequestDto request)
    {
        presence?.Record(timeProvider.GetUtcNow());
        return Task.FromResult(new ShellPipeReplyDto(request.RequestId, Ok: true));
    }

    /// <summary>
    /// Показ витрины. Считается только реклама и только на свободном ПК: показ посреди сессии
    /// значил бы, что реклама попала туда, где её быть не должно, — и платить за него нельзя.
    /// </summary>
    private Task<ShellPipeReplyDto> RecordImpression(ShellPipeRequestDto request)
    {
        if (!request.Payload.TryGetValue("cardId", out var cardId) || string.IsNullOrWhiteSpace(cardId)
            || !request.Payload.TryGetValue("shownMs", out var shown)
            || !long.TryParse(shown, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var shownMs))
        {
            return Task.FromResult(Rejected(request, ShellPipeErrorCodeNames.InvalidPayload, "An impression needs a cardId and shownMs."));
        }

        if (!SessionRuns())
        {
            impressions?.Record(cardId, shownMs);
        }

        return Task.FromResult(new ShellPipeReplyDto(request.RequestId, Ok: true));
    }

    private bool SessionRuns() => runtimeStateStore.Current.SessionRuns;

    private static ShellPipeReplyDto Rejected(ShellPipeRequestDto request, string errorCode, string message) =>
        new(request.RequestId, Ok: false, errorCode, message);
}
