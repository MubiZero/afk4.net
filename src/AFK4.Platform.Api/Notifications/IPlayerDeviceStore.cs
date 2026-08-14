using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Устройства игрока — те телефоны, на которые ему доходят пуши. Канал спрашивает store, куда
/// отправлять; приложение — регистрирует и снимает токен при входе и выходе.
/// </summary>
public interface IPlayerDeviceStore
{
    /// <summary>Запомнить устройство. Тот же токен, пришедший снова, обновляет строку, а не заводит вторую.</summary>
    Task RegisterAsync(Guid playerAccountId, string pushToken, string platform, string? locale, CancellationToken cancellationToken);

    /// <summary>Снять устройство — при выходе из аккаунта. Иначе пуши уйдут прежнему владельцу телефона.</summary>
    Task RemoveAsync(Guid playerAccountId, string pushToken, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerDeviceEntity>> ListAsync(Guid playerAccountId, CancellationToken cancellationToken);

    /// <summary>Забыть токен, про который FCM ответил, что такого устройства больше нет.</summary>
    Task ForgetAsync(string pushToken, CancellationToken cancellationToken);
}
