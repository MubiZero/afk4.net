using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IInvoiceService
{
    Task<BillingOperationResult<IReadOnlyList<InvoiceDto>>> ListForOrganizationAsync(
        Guid organizationId,
        string? status,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<InvoiceDto>> GenerateAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    /// <summary>Manually issues a one-off charge or credit note for an organization.
    /// Automatic kinds (subscription, proration) are rejected here — they are only ever issued
    /// by <see cref="GenerateAsync"/>.</summary>
    Task<BillingOperationResult<InvoiceDto>> CreateAsync(
        Guid organizationId,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<InvoiceDto>> MarkPaidAsync(
        Guid invoiceId,
        MarkInvoicePaidRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<InvoiceDto>> VoidAsync(
        Guid invoiceId,
        VoidInvoiceRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>> ListAllAsync(
        string? status,
        CancellationToken cancellationToken);

    /// <summary>Compact arrears summary for the club's own admin banner — computed from the same
    /// <see cref="BillingBalance.Compute"/> the platform side uses, over this organization's unpaid
    /// invoices only.</summary>
    Task<BillingOperationResult<OrganizationBillingStatusDto>> GetBillingStatusAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}
