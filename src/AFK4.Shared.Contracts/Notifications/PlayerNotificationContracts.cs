namespace AFK4.Shared.Contracts.Notifications;

/// <summary>
/// Уведомление, каким его видит игрок в приложении.
///
/// Берётся из той же очереди, что и пуш: отдельного хранилища у центра уведомлений нет и не нужно —
/// текст уже отрисован и сохранён там, где сообщение ставилось в отправку. Поэтому список
/// показывает и то, что до телефона не доехало: пуш, потерянный из-за выключенных уведомлений или
/// переустановленного приложения, до сих пор было невозможно прочитать нигде.
/// </summary>
public sealed record PlayerNotificationDto(
    Guid NotificationId,
    // Служебное имя события (`player.order_ready` и подобные). Приложение по нему ставит значок —
    // показывать его человеку незачем.
    string TemplateKey,
    string Subject,
    string Body,
    Guid? BranchId,
    DateTimeOffset CreatedAtUtc,
    bool IsUnread);

public sealed record PlayerNotificationsDto(
    IReadOnlyList<PlayerNotificationDto> Notifications,
    int UnreadCount);
