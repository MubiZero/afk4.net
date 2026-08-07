using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceNotifier(
    IOrganizationOwnerResolver ownerResolver,
    INotificationService notifications) : IInvoiceNotifier
{
    public Task NotifyIssuedAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoiceIssued, $"invoice-issued:{invoice.InvoiceId:N}", invoice.IssuedAtUtc, cancellationToken);

    public Task NotifyPaidAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoicePaid, $"invoice-paid:{invoice.InvoiceId:N}", invoice.PaidAtUtc ?? invoice.UpdatedAtUtc, cancellationToken);

    public Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoiceDueSoon, $"invoice-due-soon:{invoice.InvoiceId:N}", now, cancellationToken);

    public Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoiceOverdue, $"invoice-overdue:{invoice.InvoiceId:N}:{stage}", now, cancellationToken);

    private async Task SendAsync(
        InvoiceEntity invoice,
        string templateKey,
        string idempotencyKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var recipient = await ownerResolver.ResolveAsync(invoice.OrganizationId, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        var request = new NotificationRequest(
            TemplateKey: templateKey,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: string.Empty,
                EmailAddress: recipient.Email,
                StaffUserId: recipient.StaffUserId),
            Tokens: BuildTokens(invoice, recipient, now),
            IdempotencyKey: idempotencyKey,
            OrganizationId: invoice.OrganizationId);

        await notifications.SendAsync(request, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> BuildTokens(InvoiceEntity invoice, OwnerRecipient recipient, DateTimeOffset now) =>
        new Dictionary<string, string>
        {
            ["displayName"] = recipient.DisplayName,
            ["organizationName"] = recipient.OrganizationName,
            ["invoiceNumber"] = invoice.Number.ToString(CultureInfo.InvariantCulture),
            ["amount"] = MoneyFormatting.ToMajorString(invoice.AmountMinorUnits, invoice.CurrencyCode),
            ["currency"] = invoice.CurrencyCode,
            ["dueDate"] = invoice.DueAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["paidDate"] = (invoice.PaidAtUtc ?? invoice.UpdatedAtUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["daysOverdue"] = Math.Max(0, (int)Math.Floor((now - invoice.DueAtUtc).TotalDays))
                .ToString(CultureInfo.InvariantCulture),
        };
}
