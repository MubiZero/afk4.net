using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceService(
    PlatformDbContext dbContext,
    IInvoiceGenerationRunner generationRunner,
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
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    public async Task<BillingOperationResult<InvoiceDto>> MarkPaidAsync(
        Guid invoiceId,
        MarkInvoicePaidRequest request,
        CancellationToken cancellationToken)
    {
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
