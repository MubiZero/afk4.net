using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// SMS через payom.tj. Канал переводит наше уведомление на язык шлюза: наш ключ шаблона — в его
/// идентификатор одобренного шаблона, наши значения — в его типизированные плейсхолдеры.
/// </summary>
public sealed class SmsChannel(ISmsTransport transport, IOptions<SmsOptions> options) : INotificationChannel
{
    /// <summary>
    /// Одноразовый код обязан ехать в <c>code-1</c>: модерация отклоняет код, засунутый
    /// в текстовый плейсхолдер. Все наши SMS сегодня — это код и ничего больше.
    /// </summary>
    private const string CodeVariable = "code-1";

    /// <summary>Кто пишет. В одобренном тексте — «{text-1} - код подтверждения: {code-1}».</summary>
    private const string SenderVariable = "text-1";

    private readonly SmsOptions options = options.Value;

    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.RecipientAddress))
        {
            return ChannelResult.PermanentFailure("SMS recipient phone number is missing.");
        }

        if (!options.TemplateIds.TryGetValue(row.TemplateKey, out var templateId)
            || string.IsNullOrWhiteSpace(templateId))
        {
            // Без одобренного шаблона отправлять нечего, и повтор не поможет: нужен человек,
            // который заведёт шаблон в кабинете payom и впишет его идентификатор в настройки.
            return ChannelResult.PermanentFailure(
                $"No payom template configured for '{row.TemplateKey}' (set Sms__TemplateIds__{row.TemplateKey}).");
        }

        var variables = BuildVariables(row, options);
        if (variables is null)
        {
            return ChannelResult.PermanentFailure($"Notification '{row.TemplateKey}' carries no code to send.");
        }

        try
        {
            await transport.SendAsync(
                new SmsMessage(row.RecipientAddress, templateId, variables), cancellationToken);
            return ChannelResult.Sent();
        }
        catch (SmsTransportException exception)
        {
            return exception.IsPermanent
                ? ChannelResult.PermanentFailure(exception.Message)
                : ChannelResult.TransientFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return ChannelResult.TransientFailure(exception.Message);
        }
    }

    private static IReadOnlyDictionary<string, string>? BuildVariables(
        NotificationOutboxEntity row, SmsOptions options)
    {
        if (string.IsNullOrWhiteSpace(row.TokensJson))
        {
            return null;
        }

        Dictionary<string, string>? tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<Dictionary<string, string>>(row.TokensJson);
        }
        catch (JsonException)
        {
            return null;
        }

        if (tokens is null || !tokens.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var label = options.SenderLabel.Length <= options.SenderLabelMaxLength
            ? options.SenderLabel
            : options.SenderLabel[..options.SenderLabelMaxLength].TrimEnd();

        return new Dictionary<string, string>
        {
            [SenderVariable] = label,
            [CodeVariable] = code,
        };
    }
}
