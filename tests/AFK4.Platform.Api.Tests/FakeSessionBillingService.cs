using AFK4.Platform.Api.Billing;

namespace AFK4.Platform.Api.Tests;

internal sealed class FakeSessionBillingService : ISessionBillingService
{
    public Task<SessionBillingValidationResult> ValidateStartAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int durationMinutes,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Valid(durationMinutes));
    }

    public Task<SessionBillingValidationResult> ValidateExtendAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int additionalMinutes,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Valid(additionalMinutes));
    }

    public Task AppendStartLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task AppendExtendLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Valid(0));
    }

    public Task AppendCheckoutLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static SessionBillingValidationResult Valid(int minutes)
    {
        return new SessionBillingValidationResult(
            Succeeded: true,
            Error: null,
            TariffRuleVersionId: string.Empty,
            TariffVersionId: null,
            BillableSeconds: Math.Max(0, minutes) * 60,
            AmountMinorUnits: 0,
            CurrencyCode: "TJS");
    }
}
