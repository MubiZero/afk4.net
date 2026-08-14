using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Пуш на телефон игрока. В отличие от почты и SMS адрес здесь не один: у игрока бывает
/// телефон и планшет, и уведомление идёт на все его устройства — строка в очереди при этом
/// остаётся одна, чтобы повтор не размножал одно и то же сообщение.
/// </summary>
public sealed class PushChannel(IPlayerDeviceStore devices, IPushTransport transport) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Push;

    public async Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        if (row.PlayerAccountId is not { } playerAccountId)
        {
            return ChannelResult.PermanentFailure("Push notifications are addressed to a player account.");
        }

        if (string.IsNullOrWhiteSpace(row.BodyText))
        {
            return ChannelResult.PermanentFailure("Push body is empty.");
        }

        var registered = await devices.ListAsync(playerAccountId, cancellationToken);
        if (registered.Count == 0)
        {
            // Игрок не заходил с телефона или запретил уведомления. Повторять нечего: устройство
            // появится — придёт следующее событие, а это уже протухнет.
            return ChannelResult.PermanentFailure("The player has no registered devices.");
        }

        // Приложение читает это, чтобы открыть нужный экран по нажатию, а не просто запуститься.
        var data = new Dictionary<string, string> { ["template"] = row.TemplateKey };
        if (row.BranchId is { } branchId)
        {
            data["branchId"] = branchId.ToString();
        }

        var delivered = 0;
        var retryable = false;
        string? lastError = null;

        foreach (var device in registered)
        {
            try
            {
                await transport.SendAsync(
                    new PushMessage(device.PushToken, row.Subject, row.BodyText, data), cancellationToken);
                delivered++;
            }
            catch (PushTransportException exception)
            {
                lastError = exception.Message;
                if (exception.IsUnregistered)
                {
                    // Приложение удалили или переустановили. Такой токен не оживёт никогда —
                    // стираем, иначе он будет копить ошибки в каждой рассылке.
                    await devices.ForgetAsync(device.PushToken, cancellationToken);
                    continue;
                }

                retryable |= !exception.IsPermanent;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                retryable = true;
            }
        }

        if (delivered > 0)
        {
            // Хотя бы один телефон игрока зазвонил — уведомление считается доставленным. Повторять
            // рассылку ради второго устройства значило бы прислать то же сообщение дважды на первое.
            return ChannelResult.Sent();
        }

        return retryable
            ? ChannelResult.TransientFailure(lastError ?? "Push delivery failed.")
            : ChannelResult.PermanentFailure(lastError ?? "No device accepted the push.");
    }
}
