using AFK4.Platform.Api.Billing;

namespace AFK4.Platform.Api.Tests;

internal sealed class FakeSessionBillingService : ISessionBillingService
{
    // The comp value the fake returns from ComputeCompValueAsync; lets a test drive the §5.4 gate.
    public long CompValueMinorUnits { get; set; }

    // The billing mode the service last passed to validation — lets a test assert that an extend
    // without an explicit mode inherits the session's mode instead of defaulting to a free guest.
    public string? LastStartBillingMode { get; private set; }

    public string? LastExtendBillingMode { get; private set; }

    public Task<SessionBillingValidationResult> ComputeCompValueAsync(
        Guid organizationId,
        Guid branchId,
        Guid tariffVersionId,
        int durationMinutes,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new SessionBillingValidationResult(
            Succeeded: true,
            Error: null,
            TariffRuleVersionId: tariffVersionId.ToString("D"),
            TariffVersionId: tariffVersionId,
            BillableSeconds: Math.Max(0, durationMinutes) * 60,
            AmountMinorUnits: CompValueMinorUnits,
            CurrencyCode: "TJS"));
    }

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
        LastStartBillingMode = billingMode;
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
        LastExtendBillingMode = billingMode;
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
