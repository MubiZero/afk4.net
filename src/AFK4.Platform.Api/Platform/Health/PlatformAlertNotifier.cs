using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Доставка оповещений о здоровье платформы. Идёт МИМО очереди уведомлений намеренно: один из
/// видов аварии, о котором надо кричать, — смерть самой очереди, и письмо «очередь встала»,
/// положенное в очередь, не уйдёт никогда. Ретраев здесь нет, поэтому результат каждой попытки
/// пишется как прогон задания alert_delivery: провалившееся предупреждение не должно исчезать.
/// </summary>
public sealed class PlatformAlertNotifier(
    PlatformDbContext dbContext,
    ISmtpTransport smtpTransport,
    ISmsTransport smsTransport,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<AFK4.Platform.Api.Notifications.SmsOptions> smsOptions,
    IOptions<PlatformAlertOptions> alertOptions,
    IJobRunRecorder jobRunRecorder,
    TimeProvider timeProvider,
    ILogger<PlatformAlertNotifier> logger) : IPlatformAlertNotifier
{
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;
    private readonly PlatformAlertOptions alertOptions = alertOptions.Value;

    // Виды, после которых теряются деньги или доверие клиентов. Список узкий намеренно:
    // SMS, приходящая на каждый warning, через неделю перестаёт читаться.
    /// <summary>Ключ шаблона payom для оповещений платформы. Шаблона пока нет — см. ниже.</summary>
    private const string PlatformAlertTemplateKey = "platform.alert";

    /// <summary>Кириллица уходит как UCS-2: 71-й символ стоит как второе сообщение.</summary>
    private static string Shorten(string value, int limit) =>
        value.Length <= limit ? value : value[..limit].TrimEnd();

    private static readonly HashSet<string> SmsWorthyKinds = new(StringComparer.Ordinal)
    {
        PlatformIncidentKindNames.NotificationQueueStuck,
        PlatformIncidentKindNames.BillingOutboxStuck
    };

    public Task NotifyOpenedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken) =>
        SendAsync(incident, isResolved: false, cancellationToken);

    public Task NotifyResolvedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken) =>
        SendAsync(incident, isResolved: true, cancellationToken);

    private async Task SendAsync(PlatformIncidentEntity incident, bool isResolved, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var delivered = 0;
        var errors = new List<string>();

        // Почта и SMS — независимые каналы. SMS — это именно резерв на случай, если почта
        // отказала (мёртвый SMTP, битый адрес одного из админов), поэтому отказ одного канала
        // не должен ни прерывать, ни отменять другой: каждый обёрнут в свой try, и внутри
        // почтового цикла отказ одному получателю не останавливает рассылку остальным.
        try
        {
            var recipients = await dbContext.PlatformAdminUsers
                .AsNoTracking()
                .Where(admin => admin.IsActive)
                .Select(admin => admin.UserName)
                .ToListAsync(cancellationToken);

            // Тело письма собирается ЗДЕСЬ и намеренно телеграфное: получатель — сотрудник
            // платформы, а не клиент, и его задача — открыть экран «Здоровье», а не прочитать прозу.
            var subject = $"[AFK4 {(isResolved ? "resolved" : incident.Severity)}] {incident.Kind}";
            var body = string.Join('\n',
            [
                $"kind: {incident.Kind}",
                $"key: {incident.DedupKey}",
                $"severity: {incident.Severity}",
                $"opened: {incident.OpenedAtUtc:O}",
                isResolved ? $"resolved: {incident.ResolvedAtUtc:O}" : $"last seen: {incident.LastSeenAtUtc:O}",
                $"details: {incident.DetailsJson}"
            ]);

            foreach (var address in recipients)
            {
                try
                {
                    await smtpTransport.SendAsync(
                        new SmtpMessage(
                            notificationOptions.FromAddress,
                            notificationOptions.FromName,
                            address,
                            subject,
                            body,
                            $"<pre>{System.Net.WebUtility.HtmlEncode(body)}</pre>"),
                        cancellationToken);
                    delivered++;
                }
                catch (Exception exception)
                {
                    errors.Add($"email {address}: {exception.Message}");
                    logger.LogError(exception, "Failed to email platform alert to {Address} for incident {DedupKey}.", address, incident.DedupKey);
                }
            }

            // Отбой по SMS не шлём: разбудить человека ради «всё снова хорошо» — верный способ
            // научить его игнорировать следующую SMS.
            var smsWorthy = !isResolved
                && incident.Severity == PlatformIncidentSeverityNames.Critical
                && (SmsWorthyKinds.Contains(incident.Kind) || IsInvoiceGenerationOverdue(incident));

            // Шлюз принимает только одобренный шаблон. Под оповещения платформы его нет, и
            // выдумывать чужой нельзя: пришлось бы отправить текст инцидента в плейсхолдер
            // шаблона про запись к мастеру. Пока шаблон не заведён — канал молчит, и это видно.
            var alertTemplateId = smsOptions.Value.TemplateIds.GetValueOrDefault(PlatformAlertTemplateKey);

            if (smsWorthy && string.IsNullOrWhiteSpace(alertTemplateId))
            {
                errors.Add($"sms: no payom template configured (Sms__TemplateIds__{PlatformAlertTemplateKey}).");
            }
            else if (smsWorthy)
            {
                foreach (var phone in alertOptions.SmsRecipients)
                {
                    try
                    {
                        await smsTransport.SendAsync(
                            new SmsMessage(phone, alertTemplateId!, new Dictionary<string, string>
                            {
                                ["text-1"] = Shorten(subject, smsOptions.Value.SenderLabelMaxLength),
                            }),
                            cancellationToken);
                        delivered++;
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"sms {phone}: {exception.Message}");
                        logger.LogError(exception, "Failed to SMS platform alert to {Phone} for incident {DedupKey}.", phone, incident.DedupKey);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            // Сбой до/между каналами (например, запрос получателей): SMS-блок выше уже не
            // выполнится, но сама запись прогона всё равно должна отразить, что случилось.
            errors.Add(exception.Message);
            logger.LogError(exception, "Failed to deliver platform alert for incident {DedupKey}.", incident.DedupKey);
        }

        var error = errors.Count == 0 ? null : string.Join(" | ", errors);

        await jobRunRecorder.RecordAsync(
            PlatformJobNames.AlertDelivery,
            startedAt,
            timeProvider.GetUtcNow(),
            error is null ? PlatformJobOutcomeNames.Succeeded : PlatformJobOutcomeNames.Failed,
            delivered,
            error,
            cancellationToken);
    }

    private static bool IsInvoiceGenerationOverdue(PlatformIncidentEntity incident) =>
        incident.Kind == PlatformIncidentKindNames.JobOverdue
        && incident.DedupKey.EndsWith(':' + PlatformJobNames.InvoiceGeneration, StringComparison.Ordinal);
}
