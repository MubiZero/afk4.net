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
    IOptions<PlatformAlertOptions> alertOptions,
    IJobRunRecorder jobRunRecorder,
    TimeProvider timeProvider,
    ILogger<PlatformAlertNotifier> logger) : IPlatformAlertNotifier
{
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;
    private readonly PlatformAlertOptions alertOptions = alertOptions.Value;

    // Виды, после которых теряются деньги или доверие клиентов. Список узкий намеренно:
    // SMS, приходящая на каждый warning, через неделю перестаёт читаться.
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
        string? error = null;

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

            // Отбой по SMS не шлём: разбудить человека ради «всё снова хорошо» — верный способ
            // научить его игнорировать следующую SMS.
            var smsWorthy = !isResolved
                && incident.Severity == PlatformIncidentSeverityNames.Critical
                && (SmsWorthyKinds.Contains(incident.Kind) || IsInvoiceGenerationOverdue(incident));

            if (smsWorthy)
            {
                foreach (var phone in alertOptions.SmsRecipients)
                {
                    await smsTransport.SendAsync(new SmsMessage(phone, subject), cancellationToken);
                    delivered++;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            logger.LogError(exception, "Failed to deliver platform alert for incident {DedupKey}.", incident.DedupKey);
        }

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
