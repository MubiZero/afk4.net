namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Отправка одного пуша на одно устройство. За абстракцией — FCM; тесты подставляют свою
/// реализацию и не ходят в сеть.
/// </summary>
public interface IPushTransport
{
    Task SendAsync(PushMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Пуш как его видит игрок: заголовок, текст и куда открыть приложение по нажатию.
/// <paramref name="Data"/> — то, что читает приложение: экран, id сессии и прочее.
/// </summary>
public sealed record PushMessage(
    string DeviceToken,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);

/// <summary>
/// Отказ отправки. <see cref="IsPermanent"/> — повторять бессмысленно (испорченный токен,
/// отвергнутый ключ). <see cref="IsUnregistered"/> — устройство больше не существует: приложение
/// удалили или переустановили, и токен надо стереть, а не долбиться в него до конца дней.
/// </summary>
public sealed class PushTransportException(bool isPermanent, string message, bool isUnregistered = false)
    : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;

    public bool IsUnregistered { get; } = isUnregistered;
}
