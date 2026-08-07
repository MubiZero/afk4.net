using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceService(
    PlatformDbContext dbContext,
    IInvoiceGenerationRunner generationRunner,
    IInvoiceNotifier invoiceNotifier,
    TimeProvider timeProvider,
    IOptions<BillingOptions> options) : IInvoiceService
{
    private const int MaxVoidReasonLength = 512;

    private const int MaxDescriptionLength = 400;

    private static readonly HashSet<string> AllowedStatusFilters = new(StringComparer.Ordinal)
    {
        InvoiceStatusNames.Issued,
        InvoiceStatusNames.Paid,
        InvoiceStatusNames.Void,
        InvoiceStatusNames.Overdue
    };

    public async Task<BillingOperationResult<IReadOnlyList<InvoiceDto>>> ListForOrganizationAsync(
        Guid organizationId,
        string? status,
        CancellationToken cancellationToken)
    {
        var orgExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!orgExists)
        {
            return BillingOperationResult<IReadOnlyList<InvoiceDto>>.NotFound("Organization was not found.");
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
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);
        if (subscription is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound(
                "Organization has no subscription. Open the subscription first to create one.");
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

    public async Task<BillingOperationResult<InvoiceDto>> CreateAsync(
        Guid organizationId,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var kind = request.Kind?.Trim() ?? string.Empty;
        if (kind is not (InvoiceKindNames.OneOff or InvoiceKindNames.Credit))
        {
            return BillingOperationResult<InvoiceDto>.BadRequest(
                "Kind must be 'one_off' or 'credit'; subscription and proration invoices are issued automatically.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("Description is required.");
        }

        if (request.Description.Trim().Length > MaxDescriptionLength)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest(
                $"Description must be at most {MaxDescriptionLength} characters.");
        }

        // A negative amount is what makes a credit note subtract from the balance; allowing it
        // anywhere else would let a typo silently erase real debt.
        if (kind == InvoiceKindNames.Credit && request.AmountMinorUnits >= 0)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("A credit note must carry a negative amount.");
        }

        if (kind == InvoiceKindNames.OneOff && request.AmountMinorUnits <= 0)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("A one-off charge must carry a positive amount.");
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound("Organization was not found.");
        }

        var subscription = await dbContext.OrganizationSubscriptions
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            Number = ((await dbContext.Invoices.Select(candidate => (int?)candidate.Number)
                .MaxAsync(cancellationToken)) ?? 0) + 1,
            Kind = kind,
            PeriodStartUtc = now,
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = request.DueAtUtc ?? now.Add(options.Value.InvoiceDueAfter),
            AmountMinorUnits = request.AmountMinorUnits,
            GrossAmountMinorUnits = request.AmountMinorUnits,
            DiscountMinorUnits = 0,
            CurrencyCode = subscription?.CurrencyCode ?? options.Value.DefaultCurrencyCode,
            Status = InvoiceStatusNames.Issued,
            Description = request.Description.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        // A credit note can clear the debt outright, so the club should stop being flagged at once
        // rather than at the next scheduler tick.
        if (kind == InvoiceKindNames.Credit)
        {
            await RestoreSubscriptionIfSettledAsync(organizationId, now, cancellationToken);
        }

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
        await RestoreSubscriptionIfSettledAsync(invoice.OrganizationId, now, cancellationToken);
        await invoiceNotifier.NotifyPaidAsync(invoice, cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    /// <summary>Payment is a restoration, so it applies immediately rather than waiting for the next
    /// scheduler tick — the club should not stay flagged after it has paid.</summary>
    private async Task RestoreSubscriptionIfSettledAsync(
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (subscription is null || subscription.Status != SubscriptionStatusNames.PastDue)
        {
            return;
        }

        var unpaid = await dbContext.Invoices
            .Where(candidate => candidate.OrganizationId == organizationId
                && (candidate.Status == InvoiceStatusNames.Issued || candidate.Status == InvoiceStatusNames.Overdue))
            .ToListAsync(cancellationToken);
        if (BillingBalance.Compute(unpaid).InArrears)
        {
            return;
        }

        subscription.Status = SubscriptionStatusNames.Active;
        subscription.UpdatedAtUtc = now;
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is not null)
        {
            organization.SubscriptionStatus = SubscriptionStatusNames.Active;
            organization.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
        await RestoreSubscriptionIfSettledAsync(invoice.OrganizationId, now, cancellationToken);
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
            Description: entity.Description,
            GrossAmountMinorUnits: entity.GrossAmountMinorUnits,
            DiscountMinorUnits: entity.DiscountMinorUnits);
}
