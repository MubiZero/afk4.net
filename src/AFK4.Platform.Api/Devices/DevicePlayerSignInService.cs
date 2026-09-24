using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Итог входа игрока на ПК: токены либо код отказа (DevicePlayerSignInErrorCodeNames).</summary>
public sealed record DevicePlayerSignInResult(
    PlatformPersonSessionResponse? Session,
    string? Error = null,
    DateTimeOffset? RetryAfterUtc = null);

public interface IDevicePlayerSignInService
{
    Task<DevicePlayerSignInResult> SignInAsync(
        DeviceEntity device,
        string? phoneNumber,
        string? pin,
        CancellationToken cancellationToken);
}

/// <summary>
/// Вход номером и ПИН-кодом на самом игровом ПК (спека оболочки, §5.2). Проверка ПИН-кода — та
/// же, что у публичного входа; отличие в том, что сервер знает машину: считает попытки на неё,
/// не открывает вошедшему чужую сессию и привязывает токены к этому ПК.
/// </summary>
public sealed class DevicePlayerSignInService(
    PlatformDbContext dbContext,
    IPlatformPinService pinService,
    IPlatformPersonTokenService tokenService,
    IDeviceBoundPlayerTokens deviceTokens,
    TimeProvider timeProvider) : IDevicePlayerSignInService
{
    /// <summary>
    /// Неудач с одной машины за окно. Предел ПИН-кода — пять на человека, и он не мешает
    /// перебирать чужие номера по пять попыток на каждый. Десять — это опечатки двух-трёх
    /// игроков подряд, но не перебор.
    /// </summary>
    public const int MaxFailedAttempts = 10;

    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    public async Task<DevicePlayerSignInResult> SignInAsync(
        DeviceEntity device,
        string? phoneNumber,
        string? pin,
        CancellationToken cancellationToken)
    {
        // До проверки ПИН-кода: закрытый ПК не должен ни впускать, ни тратить попытки.
        if (device.MaintenanceSinceUtc is not null)
        {
            return new DevicePlayerSignInResult(null, DevicePlayerSignInErrorCodeNames.DeviceInMaintenance);
        }

        var now = timeProvider.GetUtcNow();
        if (device.PlayerSignInWindowStartedAtUtc is { } windowStarted && now - windowStarted >= AttemptWindow)
        {
            device.PlayerSignInFailedCount = 0;
            device.PlayerSignInWindowStartedAtUtc = null;
        }

        if (device.PlayerSignInFailedCount >= MaxFailedAttempts)
        {
            return new DevicePlayerSignInResult(
                null,
                DevicePlayerSignInErrorCodeNames.TooManyAttempts,
                device.PlayerSignInWindowStartedAtUtc!.Value.Add(AttemptWindow));
        }

        var authentication = await pinService.AuthenticateAsync(
            device.OrganizationId, phoneNumber, pin, device.BranchId, cancellationToken);
        if (authentication.Status != PinSignInStatus.SignedIn)
        {
            // Удачный вход счёт не обнуляет: иначе подбирающий перемежал бы чужие номера своим.
            device.PlayerSignInWindowStartedAtUtc ??= now;
            device.PlayerSignInFailedCount++;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new DevicePlayerSignInResult(null, DevicePlayerSignInErrorCodeNames.SignInRefused);
        }

        var account = authentication.Account!;
        var liveSession = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.DeviceId == device.DeviceId
                && (session.State == SessionStateNames.Active
                    || session.State == SessionStateNames.Paused
                    || session.State == SessionStateNames.Ending))
            .Select(session => new { session.PlayerAccountId })
            .FirstOrDefaultAsync(cancellationToken);
        if (liveSession is not null && liveSession.PlayerAccountId != account.PlayerAccountId)
        {
            // Гостевая сессия у стойки или чужой счёт: вход верный, но открыть вошедшему нечего, а
            // продлить или заказать на чужую сессию нельзя.
            await dbContext.SaveChangesAsync(cancellationToken);
            return new DevicePlayerSignInResult(null, DevicePlayerSignInErrorCodeNames.SessionNotYours);
        }

        // Одна машина — один вошедший: прежний вход на этом ПК гаснет тем же сохранением, что
        // выдаёт новые токены.
        await deviceTokens.RevokeForDeviceAsync(device.DeviceId, cancellationToken);
        var session = await tokenService.IssueOnDeviceAsync(
            authentication.Person!, account, device.DeviceId, cancellationToken);
        return new DevicePlayerSignInResult(session);
    }
}
