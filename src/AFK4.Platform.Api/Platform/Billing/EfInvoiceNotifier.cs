using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceNotifier(
    PlatformDbContext dbContext,
    INotificationService notifications) : IInvoiceNotifier
{
    public Task NotifyIssuedAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoiceIssued, $"invoice-issued:{invoice.InvoiceId:N}", cancellationToken);

    public Task NotifyPaidAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoicePaid, $"invoice-paid:{invoice.InvoiceId:N}", cancellationToken);

    public Task NotifyOverdueAsync(InvoiceEntity invoice, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    private async Task SendAsync(
        InvoiceEntity invoice,
        string templateKey,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var recipient = await ResolveOwnerAsync(invoice.OrganizationId, cancellationToken);
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
            Tokens: BuildTokens(invoice, recipient),
            IdempotencyKey: idempotencyKey,
            OrganizationId: invoice.OrganizationId);

        await notifications.SendAsync(request, cancellationToken);
    }

    private static IReadOnlyDictionary<string, string> BuildTokens(InvoiceEntity invoice, OwnerRecipient recipient) =>
        new Dictionary<string, string>
        {
            ["displayName"] = recipient.DisplayName,
            ["organizationName"] = recipient.OrganizationName,
            ["invoiceNumber"] = invoice.Number.ToString(CultureInfo.InvariantCulture),
            ["amount"] = (invoice.AmountMinorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture),
            ["currency"] = invoice.CurrencyCode,
            ["dueDate"] = invoice.DueAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["paidDate"] = (invoice.PaidAtUtc ?? invoice.UpdatedAtUtc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

    private async Task<OwnerRecipient?> ResolveOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var owner = await (
            from assignment in dbContext.StaffRoleAssignments
            join staff in dbContext.StaffUsers on assignment.StaffUserId equals staff.StaffUserId
            where assignment.OrganizationId == organizationId
                && assignment.RoleName == StaffRoleNames.Owner
                && staff.IsActive
                && staff.Email != null
            select staff)
            .FirstOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            return null;
        }

        var organizationName = await dbContext.Organizations
            .Where(org => org.OrganizationId == organizationId)
            .Select(org => org.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return new OwnerRecipient(owner.StaffUserId, owner.DisplayName, owner.Email!, organizationName);
    }

    private sealed record OwnerRecipient(Guid StaffUserId, string DisplayName, string Email, string OrganizationName);
}
