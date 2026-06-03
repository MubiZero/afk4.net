using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceService(
    PlatformDbContext dbContext,
    IInvoiceGenerationRunner generationRunner,
    IInvoiceNotifier invoiceNotifier,
    TimeProvider timeProvider) : IInvoiceService
{
    private const int MaxVoidReasonLength = 512;

    private static readonly HashSet<string> AllowedStatusFilters = new(StringComparer.Ordinal)
    {
        InvoiceStatusNames.Issued,
        InvoiceStatusNames.Paid,
        InvoiceStatusNames.Void,
        InvoiceStatusNames.Overdue
    };

    public async Task<BillingOperationResult<IReadOnlyList<InvoiceDto>>> ListForTenantAsync(
        Guid organizationId,
        string? status,
        CancellationToken cancellationToken)
    {
        var orgExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!orgExists)
        {
            return BillingOperationResult<IReadOnlyList<InvoiceDto>>.NotFound("Tenant was not found.");
        }

        var query = dbContext.Invoices.AsNoTracking()
            .Where(invoice => invoice.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (!AllowedStatusFilters.Contains(normalized))
            {
                return BillingOperationResult<IReadOnlyList<InvoiceDto>>.BadRequest(
                    $"status must be one of: {string.Join(", ", AllowedStatusFilters)}.");
            }

            query = query.Where(invoice => invoice.Status == normalized);
        }

        var invoices = await query
            .OrderByDescending(invoice => invoice.Number)
            .ToListAsync(cancellationToken);
        IReadOnlyList<InvoiceDto> dtos = invoices.Select(ToDto).ToList();
        return BillingOperationResult<IReadOnlyList<InvoiceDto>>.Success(dtos);
    }

    public async Task<BillingOperationResult<InvoiceDto>> GenerateAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);
        if (subscription is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound(
                "Tenant has no subscription. Open the subscription first to create one.");
        }

        var now = timeProvider.GetUtcNow();
        var invoice = await generationRunner.GenerateForSubscriptionAsync(subscription, now, cancellationToken);
        if (invoice is null)
        {
            return BillingOperationResult<InvoiceDto>.Conflict(
                "An invoice already exists for the current period.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await invoiceNotifier.NotifyIssuedAsync(invoice, cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    public async Task<BillingOperationResult<InvoiceDto>> MarkPaidAsync(
        Guid invoiceId,
        MarkInvoicePaidRequest request,
        CancellationToken cancellationToken)
    {
        // request.Reference is recorded in the mark-paid audit record at the endpoint layer, not on the
        // invoice itself — invoices carry no payment metadata (no payment provider; see design spec §8).
        var invoice = await dbContext.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceId == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound("Invoice was not found.");
        }

        if (invoice.Status == InvoiceStatusNames.Paid)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("Invoice is already paid.");
        }

        if (invoice.Status == InvoiceStatusNames.Void)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("A voided invoice cannot be marked paid.");
        }

        var now = timeProvider.GetUtcNow();
        invoice.Status = InvoiceStatusNames.Paid;
        invoice.PaidAtUtc = now;
        invoice.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        await invoiceNotifier.NotifyPaidAsync(invoice, cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    public async Task<BillingOperationResult<InvoiceDto>> VoidAsync(
        Guid invoiceId,
        VoidInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("Reason is required to void an invoice.");
        }

        if (request.Reason.Trim().Length > MaxVoidReasonLength)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest(
                $"Reason must contain {MaxVoidReasonLength} characters or fewer.");
        }

        var invoice = await dbContext.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceId == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound("Invoice was not found.");
        }

        if (invoice.Status == InvoiceStatusNames.Paid)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("A paid invoice cannot be voided.");
        }

        if (invoice.Status == InvoiceStatusNames.Void)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("Invoice is already void.");
        }

        var now = timeProvider.GetUtcNow();
        invoice.Status = InvoiceStatusNames.Void;
        invoice.VoidedAtUtc = now;
        invoice.VoidReason = request.Reason.Trim();
        invoice.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    public async Task<BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>> ListAllAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            normalized = status.Trim();
            if (!AllowedStatusFilters.Contains(normalized))
            {
                return BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>.BadRequest(
                    $"status must be one of: {string.Join(", ", AllowedStatusFilters)}.");
            }
        }

        var query =
            from invoice in dbContext.Invoices.AsNoTracking()
            join org in dbContext.Organizations.AsNoTracking()
                on invoice.OrganizationId equals org.OrganizationId
            select new { invoice, org.Name, org.Slug };

        if (normalized is not null)
        {
            query = query.Where(x => x.invoice.Status == normalized);
        }

        var rows = await query
            .OrderByDescending(x => x.invoice.Number)
            .ToListAsync(cancellationToken);

        IReadOnlyList<InvoiceListItemDto> dtos = rows.Select(x => new InvoiceListItemDto(
            InvoiceId: x.invoice.InvoiceId,
            OrganizationId: x.invoice.OrganizationId,
            OrganizationName: x.Name,
            OrganizationSlug: x.Slug,
            Number: x.invoice.Number,
            Kind: x.invoice.Kind,
            IssuedAtUtc: x.invoice.IssuedAtUtc,
            DueAtUtc: x.invoice.DueAtUtc,
            AmountMinorUnits: x.invoice.AmountMinorUnits,
            CurrencyCode: x.invoice.CurrencyCode,
            Status: x.invoice.Status)).ToList();

        return BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>.Success(dtos);
    }

    private static InvoiceDto ToDto(InvoiceEntity entity) =>
        new(
            InvoiceId: entity.InvoiceId,
            OrganizationId: entity.OrganizationId,
            Number: entity.Number,
            Kind: entity.Kind,
            PeriodStartUtc: entity.PeriodStartUtc,
            PeriodEndUtc: entity.PeriodEndUtc,
            IssuedAtUtc: entity.IssuedAtUtc,
            DueAtUtc: entity.DueAtUtc,
            AmountMinorUnits: entity.AmountMinorUnits,
            CurrencyCode: entity.CurrencyCode,
            Status: entity.Status,
            PaidAtUtc: entity.PaidAtUtc,
            VoidedAtUtc: entity.VoidedAtUtc,
            VoidReason: entity.VoidReason,
            Description: entity.Description);
}
