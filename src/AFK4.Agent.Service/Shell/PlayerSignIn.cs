using System.Collections.Concurrent;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

/// <summary>
/// Вход игрока на этом ПК (спека оболочки, §5.3–5.4). Хост к серверу за входом не ходит: ключ ПК есть
/// только у агента, и токены, выданные под него, привязаны к машине — сервер погасит их сам.
/// </summary>
public interface IPlayerSignIn
{
    /// <summary>Номер и ПИН-код с экрана. Удачный ответ пуст: токены уходят хосту кадром auth.</summary>
    Task<ShellPipeReplyDto> SignInWithPinAsync(ShellPipeRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Заявка QR с телефона: забрать и отдать хосту. Приходит и событием хаба, и в сердцебиении — на
    /// случай обрыва; повтор той же заявки не гасится второй раз.
    /// </summary>
    Task RedeemClaimAsync(Guid claimId, CancellationToken cancellationToken);
}

public sealed class PlayerSignIn(
    IPlayerSignInClient client,
    IAgentRuntimeStateStore runtimeStateStore,
    IShellHostChannel hostChannel,
    IShellStateSignal stateSignal,
    TimeProvider timeProvider,
    ILogger<PlayerSignIn> logger) : IPlayerSignIn
{
    public const string PhonePayloadKey = "phone";
    public const string PinPayloadKey = "pin";

    /// <summary>Сколько помнить забранную заявку: сердцебиение может принести её ещё раз, пока сервер не узнал.</summary>
    private static readonly TimeSpan ClaimMemory = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> claims = new();

    public async Task<ShellPipeReplyDto> SignInWithPinAsync(ShellPipeRequestDto request, CancellationToken cancellationToken)
    {
        if (!request.Payload.TryGetValue(PhonePayloadKey, out var phone) || string.IsNullOrWhiteSpace(phone)
            || !request.Payload.TryGetValue(PinPayloadKey, out var pin) || string.IsNullOrWhiteSpace(pin))
        {
            return Rejected(request, ShellPipeErrorCodeNames.InvalidPayload, "Sign-in needs a phone and a PIN.");
        }

        if (InMaintenance())
        {
            return Rejected(request, ShellPipeErrorCodeNames.DeviceInMaintenance, "This PC is under maintenance.");
        }

        PlayerSignInOutcome outcome;
        try
        {
            outcome = await client.SignInWithPinAsync(phone.Trim(), pin, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Номер и ПИН-код в журнал не пишутся — только то, что путь к серверу оборвался.
            logger.LogWarning(exception, "Player sign-in did not reach the platform.");
            return Rejected(request, ShellPipeErrorCodeNames.PlatformUnreachable, "The club could not be reached.");
        }

        if (outcome.Session is null)
        {
            logger.LogInformation("Player sign-in refused: {ErrorCode}.", outcome.ErrorCode);
            return Rejected(request, PipeCodeFor(outcome.ErrorCode), "Sign-in refused.");
        }

        PassToHost(outcome);
        return new ShellPipeReplyDto(request.RequestId, Ok: true);
    }

    public async Task RedeemClaimAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        Forget(now);

        // Без экрана токены ушли бы в пустоту: заявка подождёт, пока хост вернётся, или истечёт.
        if (!hostChannel.HostConnected)
        {
            logger.LogInformation("Sign-in claim {ClaimId} waits: the player screen is not connected.", claimId);
            return;
        }

        if (InMaintenance())
        {
            logger.LogInformation("Sign-in claim {ClaimId} ignored: this PC is under maintenance.", claimId);
            return;
        }

        if (!claims.TryAdd(claimId, now))
        {
            return;
        }

        PlayerSignInOutcome outcome;
        try
        {
            outcome = await client.RedeemClaimAsync(claimId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Сбой здесь не должен ронять ни сердцебиение, ни обработчик хаба. Заявка не забрана —
            // пусть сердцебиение принесёт её снова.
            claims.TryRemove(claimId, out _);
            logger.LogWarning(exception, "Sign-in claim {ClaimId} could not be redeemed.", claimId);
            return;
        }

        if (outcome.Session is null)
        {
            logger.LogInformation("Sign-in claim {ClaimId} refused: {ErrorCode}.", claimId, outcome.ErrorCode);
            return;
        }

        PassToHost(outcome);
    }

    private void PassToHost(PlayerSignInOutcome outcome)
    {
        if (!hostChannel.TryPost(new ShellPipeMessage(ShellPipeMessageTypeNames.Auth, Auth: outcome.Session)))
        {
            // Токены привязаны к ПК: без начатой сессии сервер погасит их через пять минут.
            logger.LogWarning("Player signed in, but the player screen left before it could be told.");
            return;
        }

        logger.LogInformation("Player signed in on this PC.");
        stateSignal.Notify();
    }

    private bool InMaintenance() => runtimeStateStore.Current.State == PlayerShellStateNames.Maintenance;

    private void Forget(DateTimeOffset now)
    {
        foreach (var (claimId, redeemedAt) in claims)
        {
            if (now - redeemedAt > ClaimMemory)
            {
                claims.TryRemove(claimId, out _);
            }
        }
    }

    private static string PipeCodeFor(string? serverCode) => serverCode switch
    {
        DevicePlayerSignInErrorCodeNames.SignInRefused => ShellPipeErrorCodeNames.SignInRefused,
        DevicePlayerSignInErrorCodeNames.TooManyAttempts => ShellPipeErrorCodeNames.TooManyAttempts,
        DevicePlayerSignInErrorCodeNames.SessionNotYours => ShellPipeErrorCodeNames.SessionNotYours,
        DevicePlayerSignInErrorCodeNames.DeviceInMaintenance => ShellPipeErrorCodeNames.DeviceInMaintenance,
        _ => ShellPipeErrorCodeNames.PlatformUnreachable
    };

    private static ShellPipeReplyDto Rejected(ShellPipeRequestDto request, string errorCode, string message) =>
        new(request.RequestId, Ok: false, errorCode, message);
}
