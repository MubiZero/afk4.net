using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>Raised when <see cref="InvoiceNumbering.SaveAsync"/> could not commit an invoice with a
/// unique number after exhausting its retries. Callers that can surface a user-facing outcome should
/// catch this and map it to a conflict; background callers can let it propagate to their normal
/// per-tick error handling.</summary>
public sealed class InvoiceNumberAllocationException(int attempts)
    : Exception($"Could not allocate a unique invoice number after {attempts} attempt(s).");

/// <summary>Two writers can race to number the next invoice: the subscription scheduler tick
/// (<see cref="EfInvoiceGenerationRunner"/>) and a manually issued one-off/credit invoice
/// (<see cref="EfInvoiceService.CreateAsync"/>) both compute the next number as MAX(Number)+1 with no
/// lock. The unique index on <c>Invoices.Number</c> is what actually prevents a collision; this is the
/// single place both writers go through so a collision becomes a bounded retry instead of two copies
/// of the same racy rule and a raw 500 for whichever writer loses.</summary>
public static class InvoiceNumbering
{
    private const int MaxAttempts = 5;

    /// <summary>Matches PlatformDbContext's <c>entity.HasIndex(invoice => invoice.Number).IsUnique()</c>.
    /// Scoping the retry to this specific index keeps an unrelated unique-constraint violation on the
    /// same insert from being misreported as a numbering conflict after burning every retry.</summary>
    private const string NumberUniqueIndexName = "IX_invoices_Number";

    public static Task<int> NextNumberAsync(PlatformDbContext dbContext, CancellationToken cancellationToken) =>
        NextNumberCoreAsync(dbContext, cancellationToken);

    private static async Task<int> NextNumberCoreAsync(PlatformDbContext dbContext, CancellationToken cancellationToken) =>
        ((await dbContext.Invoices.Select(invoice => (int?)invoice.Number).MaxAsync(cancellationToken)) ?? 0) + 1;

    /// <summary>Saves the pending changes tracked on <paramref name="dbContext"/>, which must include a
    /// freshly-added <paramref name="invoice"/> numbered via <see cref="NextNumberAsync"/>. If another
    /// writer already claimed that number, recomputes it and retries, up to <see cref="MaxAttempts"/>
    /// times, before throwing <see cref="InvoiceNumberAllocationException"/>.</summary>
    public static async Task SaveAsync(
        PlatformDbContext dbContext,
        InvoiceEntity invoice,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception)
                when (RelationalFailureClassifier.IsUniqueViolation(exception, NumberUniqueIndexName))
            {
                if (attempt == MaxAttempts)
                {
                    throw new InvoiceNumberAllocationException(attempt);
                }

                invoice.Number = await NextNumberCoreAsync(dbContext, cancellationToken);
            }
        }
    }
}
