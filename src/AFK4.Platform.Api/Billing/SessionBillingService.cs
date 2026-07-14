using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Billing;

public sealed class SessionBillingService(
    PlatformDbContext dbContext,
    ITariffService tariffService,
    IOpenShiftResolver openShiftResolver,
    TimeProvider timeProvider) : ISessionBillingService
{
    private const string DefaultCurrencyCode = "TJS";

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
        return ValidateAsync(
            organizationId,
            branchId,
            playerAccountId,
            billingMode,
            tariffVersionId,
            playerPackageId,
            durationMinutes,
            cancellationToken);
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
        return ValidateAsync(
            organizationId,
            branchId,
            playerAccountId,
            billingMode,
            tariffVersionId,
            playerPackageId,
            additionalMinutes,
            cancellationToken);
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
        return AppendLedgerEntriesAsync(
            sessionId,
            actorStaffUserId,
            validation,
            playerAccountId,
            playerPackageId,
            billingMode,
            now,
            cancellationToken);
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
        return AppendLedgerEntriesAsync(
            sessionId,
            actorStaffUserId,
            validation,
            playerAccountId,
            playerPackageId,
            billingMode,
            now,
            cancellationToken);
    }

    private async Task<SessionBillingValidationResult> ValidateAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int durationMinutes,
        CancellationToken cancellationToken)
    {
        if (durationMinutes <= 0)
        {
            return Invalid("Billable duration must be positive.");
        }

        if (string.IsNullOrWhiteSpace(billingMode))
        {
            return new SessionBillingValidationResult(
                Succeeded: true,
                Error: null,
                TariffRuleVersionId: string.Empty,
                TariffVersionId: null,
                BillableSeconds: checked(durationMinutes * 60),
                AmountMinorUnits: 0,
                DefaultCurrencyCode);
        }

        if (playerAccountId is null)
        {
            return Invalid("Player account id is required for session billing.");
        }

        var playerExists = await dbContext.PlayerAccounts
            .AsNoTracking()
            .AnyAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.HomeBranchId == branchId &&
                    player.PlayerAccountId == playerAccountId.Value &&
                    player.IsActive,
                cancellationToken);

        if (!playerExists)
        {
            return Invalid("Player account was not found.");
        }

        var validation = billingMode.Trim() switch
        {
            BillingModeNames.PrepaidWallet => await ValidateTariffBillingAsync(
                organizationId,
                branchId,
                playerAccountId.Value,
                tariffVersionId,
                durationMinutes,
                requireWalletBalance: true,
                cancellationToken),
            BillingModeNames.PostpaidDebt => await ValidateTariffBillingAsync(
                organizationId,
                branchId,
                playerAccountId.Value,
                tariffVersionId,
                durationMinutes,
                requireWalletBalance: false,
                cancellationToken),
            BillingModeNames.Package => await ValidatePackageBillingAsync(
                organizationId,
                branchId,
                playerAccountId.Value,
                playerPackageId,
                durationMinutes,
                cancellationToken),
            _ => Invalid("Unsupported billing mode.")
        };

        if (!validation.Succeeded)
        {
            return validation;
        }

        var openShift = await openShiftResolver.GetOpenShiftIdAsync(organizationId, branchId, cancellationToken);
        return openShift.Succeeded
            ? validation
            : Invalid(openShift.Error ?? "An open shift is required.");
    }

    private async Task<SessionBillingValidationResult> ValidateTariffBillingAsync(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? tariffVersionId,
        int durationMinutes,
        bool requireWalletBalance,
        CancellationToken cancellationToken)
    {
        if (tariffVersionId is null)
        {
            return Invalid("Tariff version id is required for tariff billing.");
        }

        var calculation = await tariffService.CalculateAsync(
            branchId,
            new CalculateTariffRequest(organizationId, tariffVersionId.Value, durationMinutes),
            cancellationToken);

        if (calculation is null)
        {
            return Invalid("Tariff calculation could not be completed.");
        }

        var ledgerCurrencyValidation = await GetLedgerCurrencyForWriteAsync(
            playerAccountId,
            calculation.Amount.CurrencyCode,
            cancellationToken);
        if (ledgerCurrencyValidation is not null)
        {
            return Invalid(ledgerCurrencyValidation);
        }

        if (requireWalletBalance)
        {
            var walletBalance = await GetBalanceAsync(playerAccountId, LedgerAccountTypeNames.Wallet, cancellationToken);
            if (walletBalance < calculation.Amount.MinorUnits)
            {
                return Invalid("Insufficient wallet balance.");
            }
        }

        return new SessionBillingValidationResult(
            Succeeded: true,
            Error: null,
            calculation.TariffRuleVersionId,
            calculation.TariffVersionId,
            checked(calculation.BillableMinutes * 60),
            calculation.Amount.MinorUnits,
            calculation.Amount.CurrencyCode);
    }

    private async Task<SessionBillingValidationResult> ValidatePackageBillingAsync(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? playerPackageId,
        int durationMinutes,
        CancellationToken cancellationToken)
    {
        if (playerPackageId is null)
        {
            return Invalid("Player package id is required for package billing.");
        }

        var playerPackage = await dbContext.PlayerPackages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                package =>
                    package.OrganizationId == organizationId &&
                    package.BranchId == branchId &&
                    package.PlayerAccountId == playerAccountId &&
                    package.PlayerPackageId == playerPackageId.Value,
                cancellationToken);

        if (playerPackage is null)
        {
            return Invalid("Player package was not found.");
        }

        var now = timeProvider.GetUtcNow();
        if (playerPackage.ExpiresAtUtc is not null && playerPackage.ExpiresAtUtc <= now)
        {
            return Invalid("Player package is expired.");
        }

        var requestedSeconds = checked(durationMinutes * 60);
        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(
            dbContext,
            playerPackageId.Value,
            cancellationToken);

        if (remaining.IncludedSeconds + remaining.BonusSeconds < requestedSeconds)
        {
            return Invalid("Insufficient package time remaining.");
        }

        return new SessionBillingValidationResult(
            Succeeded: true,
            Error: null,
            $"package:{playerPackageId.Value:D}",
            TariffVersionId: null,
            requestedSeconds,
            AmountMinorUnits: 0,
            playerPackage.CurrencyCode);
    }

    private async Task AppendLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Cannot append ledger entries for failed session billing validation.");
        }

        // A start workflow may stage the authoritative session in this DbContext before its
        // transaction owner performs the final save. Extensions still resolve through the
        // persisted query path, while staged starts can append their ledger rows atomically.
        var session = dbContext.ChangeTracker.Entries<SessionEntity>()
            .SingleOrDefault(entry => entry.State == EntityState.Added && entry.Entity.SessionId == sessionId)
            ?.Entity
            ?? await dbContext.Sessions.SingleAsync(
                candidate => candidate.SessionId == sessionId,
                cancellationToken);
        var shiftId = await GetRequiredOpenShiftIdAsync(session.OrganizationId, session.BranchId, cancellationToken);

        switch (billingMode.Trim())
        {
            case BillingModeNames.PrepaidWallet:
                dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                    session.OrganizationId,
                    session.BranchId,
                    playerAccountId,
                    sessionId,
                    playerPackageId: null,
                    LedgerEntryTypeNames.GameplayCharge,
                    LedgerAccountTypeNames.Wallet,
                    -validation.AmountMinorUnits,
                    quantitySeconds: 0,
                    validation.CurrencyCode,
                    LedgerEntryTypeNames.GameplayCharge,
                    "prepaid wallet gameplay charge",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now,
                    shiftId));
                break;

            case BillingModeNames.PostpaidDebt:
                dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                    session.OrganizationId,
                    session.BranchId,
                    playerAccountId,
                    sessionId,
                    playerPackageId: null,
                    LedgerEntryTypeNames.PostpaidDebt,
                    LedgerAccountTypeNames.Debt,
                    validation.AmountMinorUnits,
                    quantitySeconds: 0,
                    validation.CurrencyCode,
                    LedgerEntryTypeNames.PostpaidDebt,
                    "postpaid gameplay debt",
                    reversesLedgerEntryId: null,
                    actorStaffUserId,
                    now,
                    shiftId));
                break;

            case BillingModeNames.Package:
                if (playerPackageId is null)
                {
                    throw new InvalidOperationException("Player package id is required for package session billing.");
                }

                await AppendPackageConsumptionAsync(
                    session,
                    actorStaffUserId,
                    validation,
                    playerAccountId,
                    playerPackageId.Value,
                    now,
                    shiftId,
                    cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported session billing mode '{billingMode}'.");
        }
    }

    private async Task AppendPackageConsumptionAsync(
        SessionEntity session,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid playerPackageId,
        DateTimeOffset now,
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(
            dbContext,
            playerPackageId,
            cancellationToken);

        if (remaining.IncludedSeconds + remaining.BonusSeconds < validation.BillableSeconds)
        {
            throw new InvalidOperationException("Insufficient package time remaining.");
        }

        var bonusToConsume = Math.Min(remaining.BonusSeconds, validation.BillableSeconds);
        var packageToConsume = validation.BillableSeconds - bonusToConsume;

        if (bonusToConsume > 0)
        {
            dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                session.OrganizationId,
                session.BranchId,
                playerAccountId,
                session.SessionId,
                playerPackageId,
                LedgerEntryTypeNames.BonusConsumption,
                LedgerAccountTypeNames.BonusTime,
                amountMinorUnits: 0,
                -bonusToConsume,
                validation.CurrencyCode,
                LedgerEntryTypeNames.BonusConsumption,
                "package bonus time consumption",
                reversesLedgerEntryId: null,
                actorStaffUserId,
                now,
                shiftId));
        }

        if (packageToConsume > 0)
        {
            dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                session.OrganizationId,
                session.BranchId,
                playerAccountId,
                session.SessionId,
                playerPackageId,
                LedgerEntryTypeNames.PackageConsumption,
                LedgerAccountTypeNames.PackageTime,
                amountMinorUnits: 0,
                -packageToConsume,
                validation.CurrencyCode,
                LedgerEntryTypeNames.PackageConsumption,
                "package included time consumption",
                reversesLedgerEntryId: null,
                actorStaffUserId,
                now.AddTicks(1),
                shiftId));
        }
    }

    public async Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.SessionId == sessionId, cancellationToken);

        if (session is null)
        {
            return Invalid("Session was not found.");
        }

        // Anti-fraud §5.4: a comp (free) session never bills, whatever tariff id it carries.
        if (session.IsComp)
        {
            return new SessionBillingValidationResult(
                Succeeded: true,
                Error: null,
                session.TariffRuleVersionId,
                TariffVersionId: null,
                BillableSeconds: 0,
                AmountMinorUnits: 0,
                DefaultCurrencyCode);
        }

        // A guest/unbilled session carries a non-GUID tariff rule id; nothing accrues.
        if (!Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
        {
            return new SessionBillingValidationResult(
                Succeeded: true,
                Error: null,
                session.TariffRuleVersionId,
                TariffVersionId: null,
                BillableSeconds: 0,
                AmountMinorUnits: 0,
                DefaultCurrencyCode);
        }

        var version = await dbContext.TariffVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == session.OrganizationId &&
                    candidate.BranchId == session.BranchId &&
                    candidate.TariffVersionId == tariffVersionId,
                cancellationToken);

        // The session is bound to this version for its whole life, even if the
        // tariff was retired after the session started, so no effective-window guard here.
        if (version is null)
        {
            return Invalid("Tariff version for the session was not found.");
        }

        // Fixed-duration wallet sessions settle their tariff charge atomically at start (and each
        // extension). Checkout may still collect an attached POS tab, but must never charge the
        // already-paid session time again.
        if (session.BillingMode == BillingModeNames.PrepaidWallet)
        {
            return new SessionBillingValidationResult(
                Succeeded: true,
                Error: null,
                version.TariffVersionId.ToString("D"),
                version.TariffVersionId,
                BillableSeconds: 0,
                AmountMinorUnits: 0,
                version.CurrencyCode);
        }

        var startedAtUtc = session.StartedAtUtc ?? session.RequestedAtUtc;
        var pricing = new TariffPricing(
            version.PricePerMinuteMinorUnits,
            version.MinimumBillableMinutes,
            version.RoundingIncrementMinutes,
            version.CurrencyCode);
        var computation = TariffBilling.ComputeForElapsed(now - startedAtUtc, pricing);
        if (computation is null)
        {
            return Invalid("Checkout charge could not be computed.");
        }

        var billableSeconds = (int)Math.Min(int.MaxValue, (long)computation.BillableMinutes * 60);
        return new SessionBillingValidationResult(
            Succeeded: true,
            Error: null,
            version.TariffVersionId.ToString("D"),
            version.TariffVersionId,
            billableSeconds,
            computation.AmountMinorUnits,
            computation.CurrencyCode);
    }

    public async Task<SessionBillingValidationResult> ComputeCompValueAsync(
        Guid organizationId,
        Guid branchId,
        Guid tariffVersionId,
        int durationMinutes,
        CancellationToken cancellationToken)
    {
        if (durationMinutes <= 0)
        {
            return Invalid("Comp duration must be positive.");
        }

        var calculation = await tariffService.CalculateAsync(
            branchId,
            new CalculateTariffRequest(organizationId, tariffVersionId, durationMinutes),
            cancellationToken);

        if (calculation is null)
        {
            return Invalid("Comp value could not be computed; tariff version was not found.");
        }

        return new SessionBillingValidationResult(
            Succeeded: true,
            Error: null,
            calculation.TariffRuleVersionId,
            calculation.TariffVersionId,
            checked(calculation.BillableMinutes * 60),
            calculation.Amount.MinorUnits,
            calculation.Amount.CurrencyCode);
    }

    public async Task AppendCheckoutLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!validation.Succeeded)
        {
            throw new InvalidOperationException("Cannot append ledger entries for failed checkout charge validation.");
        }

        if (validation.AmountMinorUnits == 0)
        {
            // Open tab with zero billable time produces no debt entry.
            return;
        }

        var session = await dbContext.Sessions
            .SingleAsync(candidate => candidate.SessionId == sessionId, cancellationToken);
        var shiftId = await GetRequiredOpenShiftIdAsync(session.OrganizationId, session.BranchId, cancellationToken);

        dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
            session.OrganizationId,
            session.BranchId,
            playerAccountId,
            sessionId,
            playerPackageId: null,
            LedgerEntryTypeNames.PostpaidDebt,
            LedgerAccountTypeNames.Debt,
            validation.AmountMinorUnits,
            quantitySeconds: 0,
            validation.CurrencyCode,
            LedgerEntryTypeNames.PostpaidDebt,
            "postpaid open-tab gameplay debt (checkout)",
            reversesLedgerEntryId: null,
            actorStaffUserId,
            now,
            shiftId));
    }

    private async Task<Guid> GetRequiredOpenShiftIdAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var openShift = await openShiftResolver.GetOpenShiftIdAsync(organizationId, branchId, cancellationToken);
        if (!openShift.Succeeded)
        {
            throw new InvalidOperationException(openShift.Error ?? "An open shift is required.");
        }

        return openShift.Response;
    }

    private async Task<string?> GetLedgerCurrencyForWriteAsync(
        Guid playerAccountId,
        string requestedCurrencyCode,
        CancellationToken cancellationToken)
    {
        var requested = requestedCurrencyCode.Trim().ToUpperInvariant();
        var rawCurrencies = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId)
            .Select(entry => entry.CurrencyCode)
            .ToListAsync(cancellationToken);
        var currencies = rawCurrencies
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (currencies.Count > 1)
        {
            return "Player ledger contains multiple currencies.";
        }

        var existingCurrency = currencies.SingleOrDefault();
        if (existingCurrency is not null &&
            !string.Equals(existingCurrency, requested, StringComparison.OrdinalIgnoreCase))
        {
            return "Requested currency must match the player ledger currency.";
        }

        return null;
    }

    private async Task<long> GetBalanceAsync(
        Guid playerAccountId,
        string accountType,
        CancellationToken cancellationToken)
    {
        return await dbContext.LedgerEntries
            .Where(entry => entry.PlayerAccountId == playerAccountId && entry.AccountType == accountType)
            .SumAsync(entry => (long?)entry.AmountMinorUnits, cancellationToken) ?? 0;
    }

    private static SessionBillingValidationResult Invalid(string error)
    {
        return new SessionBillingValidationResult(
            Succeeded: false,
            error,
            TariffRuleVersionId: string.Empty,
            TariffVersionId: null,
            BillableSeconds: 0,
            AmountMinorUnits: 0,
            DefaultCurrencyCode);
    }
}
