using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>
/// Условия оплаты, которые задаёт платформа: пробный период, обещанный платёж и льгота до перехода
/// на бесплатный тариф. Строки нет — значения по умолчанию.
/// </summary>
public static class BillingTerms
{
    public static readonly BillingTermsDto Defaults = new(
        ClubPlanLimits.TrialDays, ClubPlanLimits.PromisedPaymentDays, ClubPlanLimits.FallbackAfterOverdueDays, UpdatedAtUtc: null);

    public static async Task<BillingTermsDto> LoadAsync(PlatformDbContext db, CancellationToken ct)
    {
        var row = await db.PlatformBillingTerms.AsNoTracking()
            .SingleOrDefaultAsync(terms => terms.Id == PlatformBillingTermsEntity.SingletonId, ct);
        return row is null
            ? Defaults
            : new BillingTermsDto(row.TrialDays, row.PromisedPaymentDays, row.FallbackAfterOverdueDays, row.UpdatedAtUtc);
    }

    public static string? Validate(UpdateBillingTermsRequest request)
    {
        if (request.TrialDays is < 0 or > BillingTermsLimits.MaxTrialDays)
            return $"TrialDays must be between 0 and {BillingTermsLimits.MaxTrialDays}.";
        if (request.PromisedPaymentDays is < 0 or > BillingTermsLimits.MaxPromisedPaymentDays)
            return $"PromisedPaymentDays must be between 0 and {BillingTermsLimits.MaxPromisedPaymentDays}.";
        if (request.FallbackAfterOverdueDays is < 0 or > BillingTermsLimits.MaxFallbackAfterOverdueDays)
            return $"FallbackAfterOverdueDays must be between 0 and {BillingTermsLimits.MaxFallbackAfterOverdueDays}.";
        return null;
    }

    public static async Task<BillingTermsDto> SaveAsync(
        PlatformDbContext db, UpdateBillingTermsRequest request, Guid? actorPlatformAdminUserId, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.PlatformBillingTerms.SingleOrDefaultAsync(terms => terms.Id == PlatformBillingTermsEntity.SingletonId, ct);
        if (row is null)
        {
            row = new PlatformBillingTermsEntity();
            db.PlatformBillingTerms.Add(row);
        }

        row.TrialDays = request.TrialDays;
        row.PromisedPaymentDays = request.PromisedPaymentDays;
        row.FallbackAfterOverdueDays = request.FallbackAfterOverdueDays;
        row.UpdatedAtUtc = now;
        row.UpdatedByPlatformAdminUserId = actorPlatformAdminUserId;
        await db.SaveChangesAsync(ct);
        return new BillingTermsDto(row.TrialDays, row.PromisedPaymentDays, row.FallbackAfterOverdueDays, row.UpdatedAtUtc);
    }
}
