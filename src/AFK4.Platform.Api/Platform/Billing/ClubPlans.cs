using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>
/// Тарифы клуба (спека `2026-09-25-club-plans-per-pc-design.md`): бесплатно до десяти ПК, дальше
/// за каждый ПК; пробный период и обещанный платёж клуб берёт сам, неоплата переводит на
/// бесплатный тариф, а не блокирует.
/// </summary>
public sealed class ClubPlans(PlatformDbContext db, IAuditRecordWriter audit, TimeProvider clock)
{
    private const string AuditTarget = "OrganizationSubscription";

    /// <summary>Сумма за период: фиксированная часть и каждый ПК сверх включённых.</summary>
    public static long AmountFor(SubscriptionPlanEntity plan, int devices) =>
        plan.PriceMinorUnits + Math.Max(0, devices - plan.IncludedDevices) * plan.PricePerDeviceMinorUnits;

    public static bool IsPerDevice(SubscriptionPlanEntity plan) => plan.PricePerDeviceMinorUnits > 0;

    // Платят за ПК, которые ведут игры: ожидающий подтверждения игрока за собой не посадит, а
    // рабочее место управляющего — не игровой ПК.
    public static Task<int> ApprovedDevicesAsync(PlatformDbContext db, Guid organizationId, CancellationToken ct) =>
        db.Devices.CountAsync(device => device.OrganizationId == organizationId
            && device.Role == DeviceRoleNames.GamingPc
            && device.EnrollmentState == DeviceEnrollmentStateNames.Approved, ct);

    /// <summary>Ставит клубу тариф: подписка, зеркало в организации и лимиты тарифа.</summary>
    public static void Apply(OrganizationEntity organization, OrganizationSubscriptionEntity subscription,
        SubscriptionPlanEntity plan, string status, long amountMinorUnits, DateTimeOffset now)
    {
        subscription.PlanCode = plan.PlanCode;
        subscription.Status = status;
        subscription.AmountMinorUnits = amountMinorUnits;
        subscription.CurrencyCode = plan.CurrencyCode;
        subscription.BillingInterval = plan.BillingInterval;
        subscription.UpdatedAtUtc = now;
        organization.PlanCode = plan.PlanCode;
        organization.SubscriptionStatus = status;
        organization.LimitsJson = JsonSerializer.Serialize(new OrganizationLimitsDto(
            plan.MaxBranches, plan.MaxDevicesPerBranch, plan.MaxConcurrentSessions, plan.MaxStaffUsersPerBranch));
        organization.UpdatedAtUtc = now;
    }

    public async Task<ClubPlanDto?> DescribeAsync(Guid organizationId, CancellationToken ct)
    {
        var state = await LoadAsync(organizationId, ct);
        if (state is null) return null;
        var (_, subscription, plan) = state.Value;
        var now = clock.GetUtcNow();
        var devices = await ApprovedDevicesAsync(db, organizationId, ct);
        var perPc = await PlanAsync(OrganizationPlanCodeNames.PerPc, ct);
        var currency = plan?.CurrencyCode ?? subscription.CurrencyCode;
        var included = plan is { } current && IsPerDevice(current) ? current.IncludedDevices : perPc?.IncludedDevices ?? ClubPlanLimits.FreeDevices;
        var pricePerDevice = plan is { } priced && IsPerDevice(priced) ? priced.PricePerDeviceMinorUnits : perPc?.PricePerDeviceMinorUnits ?? ClubPlanLimits.PricePerDeviceMinorUnits;
        var kind = KindOf(subscription, plan);
        var estimate = kind == ClubPlanKindNames.PerPc && plan is not null ? AmountFor(plan, devices) : 0;
        var overdue = await OverdueAsync(organizationId, now, ct);
        var unpaid = await OldestUnpaidAsync(organizationId, ct);
        var referralCode = await ClubReferrals.EnsureCodeAsync(db, state.Value.Organization, ct);
        var referred = await db.Organizations.AsNoTracking()
            .CountAsync(candidate => candidate.ReferredByOrganizationId == organizationId && candidate.ReferralRewardedAtUtc != null, ct);

        return new ClubPlanDto(
            subscription.PlanCode,
            kind,
            devices,
            included,
            Math.Max(0, devices - included),
            new MoneyDto(currency, pricePerDevice),
            new MoneyDto(currency, estimate),
            kind == ClubPlanKindNames.Trial ? subscription.CurrentPeriodEndUtc : null,
            TrialAvailable: subscription.TrialStartedAtUtc is null && kind is ClubPlanKindNames.Free or ClubPlanKindNames.Legacy,
            CanSwitchToPerPc: kind is ClubPlanKindNames.Free or ClubPlanKindNames.Legacy && overdue == 0,
            PromisedPaymentAvailable: unpaid is not null && subscription.PromisedPaymentInvoiceId != unpaid.InvoiceId
                && !(subscription.PaymentGraceUntilUtc > now),
            PromisedPaymentUntilUtc: subscription.PaymentGraceUntilUtc > now ? subscription.PaymentGraceUntilUtc : null,
            Overdue: overdue > 0 ? new MoneyDto(currency, overdue) : null,
            ReferralCode: referralCode,
            FreeMonths: subscription.FreeMonths,
            ReferredClubs: referred);
    }

    public async Task<string?> StartTrialAsync(Guid organizationId, Guid actorStaffUserId, CancellationToken ct)
    {
        var state = await LoadAsync(organizationId, ct);
        if (state is null) return null;
        var (organization, subscription, plan) = state.Value;
        if (subscription.TrialStartedAtUtc is not null) return ClubPlanErrorCodeNames.TrialUsed;
        if (KindOf(subscription, plan) is ClubPlanKindNames.PerPc or ClubPlanKindNames.Trial) return ClubPlanErrorCodeNames.AlreadyOnPlan;
        var perPc = await PlanAsync(OrganizationPlanCodeNames.PerPc, ct) ?? throw new InvalidOperationException("The per-PC plan is missing.");

        var now = clock.GetUtcNow();
        Apply(organization, subscription, perPc, SubscriptionStatusNames.Trial, 0, now);
        subscription.TrialStartedAtUtc = now;
        subscription.CurrentPeriodStartUtc = now;
        subscription.CurrentPeriodEndUtc = now.AddDays(ClubPlanLimits.TrialDays);
        // Пробный месяц не выставляется: следующий счёт — через месяц после его конца.
        subscription.NextInvoiceUtc = null;
        await SaveWithAuditAsync(organizationId, actorStaffUserId, AuditActionNames.StartPlanTrial, new { subscription.CurrentPeriodEndUtc }, ct);
        return string.Empty;
    }

    public async Task<string?> SwitchToPerPcAsync(Guid organizationId, Guid actorStaffUserId, CancellationToken ct)
    {
        var state = await LoadAsync(organizationId, ct);
        if (state is null) return null;
        var (organization, subscription, plan) = state.Value;
        if (KindOf(subscription, plan) is ClubPlanKindNames.PerPc or ClubPlanKindNames.Trial) return ClubPlanErrorCodeNames.AlreadyOnPlan;
        var now = clock.GetUtcNow();
        if (await OverdueAsync(organizationId, now, ct) > 0) return ClubPlanErrorCodeNames.OverdueInvoices;
        var perPc = await PlanAsync(OrganizationPlanCodeNames.PerPc, ct) ?? throw new InvalidOperationException("The per-PC plan is missing.");

        StartPaidPeriod(organization, subscription, perPc, await ApprovedDevicesAsync(db, organizationId, ct), now);
        await SaveWithAuditAsync(organizationId, actorStaffUserId, AuditActionNames.SwitchPlanToPerPc, new { perPc.PlanCode }, ct);
        return string.Empty;
    }

    public async Task<string?> PromisePaymentAsync(Guid organizationId, Guid actorStaffUserId, CancellationToken ct)
    {
        var state = await LoadAsync(organizationId, ct);
        if (state is null) return null;
        var (_, subscription, _) = state.Value;
        var unpaid = await OldestUnpaidAsync(organizationId, ct);
        if (unpaid is null) return ClubPlanErrorCodeNames.NothingToPromise;
        var now = clock.GetUtcNow();
        if (subscription.PromisedPaymentInvoiceId == unpaid.InvoiceId || subscription.PaymentGraceUntilUtc > now)
            return ClubPlanErrorCodeNames.PromiseUsed;

        subscription.PaymentGraceUntilUtc = now.AddDays(ClubPlanLimits.PromisedPaymentDays);
        subscription.PromisedPaymentInvoiceId = unpaid.InvoiceId;
        subscription.UpdatedAtUtc = now;
        await SaveWithAuditAsync(organizationId, actorStaffUserId, AuditActionNames.PromisePlanPayment,
            new { unpaid.Number, subscription.PaymentGraceUntilUtc }, ct);
        return string.Empty;
    }

    /// <summary>
    /// Переходы по времени — из часового задания счетов: кончился пробный период; счёт просрочен
    /// дольше двух недель без обещанного платежа — клуб уходит на бесплатный тариф.
    /// </summary>
    public async Task<int> RunTransitionsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var free = await PlanAsync(OrganizationPlanCodeNames.Free, ct);
        var perPc = await PlanAsync(OrganizationPlanCodeNames.PerPc, ct);
        if (free is null || perPc is null) return 0;
        var changed = 0;

        var endedTrials = await db.OrganizationSubscriptions
            .Where(subscription => subscription.Status == SubscriptionStatusNames.Trial
                && subscription.PlanCode == OrganizationPlanCodeNames.PerPc
                && subscription.CurrentPeriodEndUtc <= now)
            .ToListAsync(ct);
        foreach (var subscription in endedTrials)
        {
            var organization = await db.Organizations.SingleAsync(candidate => candidate.OrganizationId == subscription.OrganizationId, ct);
            var devices = await ApprovedDevicesAsync(db, subscription.OrganizationId, ct);
            // Десять ПК и меньше — платить не за что, и клуб остаётся на бесплатном.
            if (devices > perPc.IncludedDevices) StartPaidPeriod(organization, subscription, perPc, devices, now);
            else MoveToFree(organization, subscription, free, now);
            await WriteSystemAuditAsync(subscription.OrganizationId, AuditActionNames.EndPlanTrial, new { subscription.PlanCode, devices }, ct);
            changed++;
        }

        var fallbackBefore = now.AddDays(-ClubPlanLimits.FallbackAfterOverdueDays);
        var overdueOrganizations = await db.Invoices.AsNoTracking()
            .Where(invoice => (invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
                && invoice.DueAtUtc <= fallbackBefore)
            .Select(invoice => invoice.OrganizationId)
            .Distinct()
            .ToListAsync(ct);
        var falling = await db.OrganizationSubscriptions
            .Where(subscription => overdueOrganizations.Contains(subscription.OrganizationId)
                && subscription.PlanCode == OrganizationPlanCodeNames.PerPc
                && (subscription.PaymentGraceUntilUtc == null || subscription.PaymentGraceUntilUtc <= now))
            .ToListAsync(ct);
        foreach (var subscription in falling)
        {
            var organization = await db.Organizations.SingleAsync(candidate => candidate.OrganizationId == subscription.OrganizationId, ct);
            MoveToFree(organization, subscription, free, now, keepStatus: true);
            await WriteSystemAuditAsync(subscription.OrganizationId, AuditActionNames.FallBackToFreePlan, new { subscription.PlanCode }, ct);
            changed++;
        }

        if (changed > 0) await db.SaveChangesAsync(ct);
        return changed;
    }

    /// <summary>Платный период с этого момента: счёт за него — в его конце, по числу ПК тогда.</summary>
    public static void StartPaidPeriod(OrganizationEntity organization, OrganizationSubscriptionEntity subscription,
        SubscriptionPlanEntity plan, int devices, DateTimeOffset now)
    {
        Apply(organization, subscription, plan, SubscriptionStatusNames.Active, AmountFor(plan, devices), now);
        subscription.CurrentPeriodStartUtc = now;
        subscription.CurrentPeriodEndUtc = BillingPeriod.Advance(now, plan.BillingInterval);
        subscription.NextInvoiceUtc = subscription.CurrentPeriodEndUtc;
    }

    /// <summary>
    /// На бесплатный: платить не за что. Долг, если он есть, остаётся долгом — статус «просрочено»
    /// переходом не прощается.
    /// </summary>
    public static void MoveToFree(OrganizationEntity organization, OrganizationSubscriptionEntity subscription,
        SubscriptionPlanEntity free, DateTimeOffset now, bool keepStatus = false)
    {
        Apply(organization, subscription, free, keepStatus ? subscription.Status : SubscriptionStatusNames.Active, 0, now);
        subscription.CurrentPeriodStartUtc = now;
        subscription.CurrentPeriodEndUtc = BillingPeriod.Advance(now, free.BillingInterval);
        subscription.NextInvoiceUtc = null;
    }

    private static string KindOf(OrganizationSubscriptionEntity subscription, SubscriptionPlanEntity? plan) =>
        subscription.PlanCode switch
        {
            OrganizationPlanCodeNames.Free => ClubPlanKindNames.Free,
            OrganizationPlanCodeNames.PerPc when subscription.Status == SubscriptionStatusNames.Trial => ClubPlanKindNames.Trial,
            OrganizationPlanCodeNames.PerPc => ClubPlanKindNames.PerPc,
            _ when plan is not null && IsPerDevice(plan) => ClubPlanKindNames.PerPc,
            _ => ClubPlanKindNames.Legacy
        };

    private async Task<(OrganizationEntity Organization, OrganizationSubscriptionEntity Subscription, SubscriptionPlanEntity? Plan)?> LoadAsync(
        Guid organizationId, CancellationToken ct)
    {
        var organization = await db.Organizations.SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, ct);
        var subscription = await db.OrganizationSubscriptions.SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, ct);
        if (organization is null || subscription is null) return null;
        return (organization, subscription, await PlanAsync(subscription.PlanCode, ct));
    }

    private Task<SubscriptionPlanEntity?> PlanAsync(string planCode, CancellationToken ct) =>
        db.SubscriptionPlans.AsNoTracking().SingleOrDefaultAsync(plan => plan.PlanCode == planCode, ct);

    private Task<long> OverdueAsync(Guid organizationId, DateTimeOffset now, CancellationToken ct) =>
        db.Invoices.AsNoTracking()
            .Where(invoice => invoice.OrganizationId == organizationId
                && (invoice.Status == InvoiceStatusNames.Overdue || (invoice.Status == InvoiceStatusNames.Issued && invoice.DueAtUtc < now)))
            .SumAsync(invoice => invoice.AmountMinorUnits, ct);

    private Task<InvoiceEntity?> OldestUnpaidAsync(Guid organizationId, CancellationToken ct) =>
        db.Invoices.AsNoTracking()
            .Where(invoice => invoice.OrganizationId == organizationId
                && (invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue))
            .OrderBy(invoice => invoice.DueAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task SaveWithAuditAsync(Guid organizationId, Guid actorStaffUserId, string action, object details, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditRecordWriteRequest(
            organizationId, BranchId: null, ActorStaffUserId: actorStaffUserId, Action: action, TargetType: AuditTarget,
            TargetId: organizationId.ToString("N"), Outcome: AuditOutcome.Succeeded, SourceApp: "PlatformApi",
            DetailsJson: JsonSerializer.Serialize(details)), ct);
    }

    private Task WriteSystemAuditAsync(Guid organizationId, string action, object details, CancellationToken ct) =>
        audit.WriteAsync(new AuditRecordWriteRequest(
            organizationId, BranchId: null, ActorStaffUserId: null, Action: action, TargetType: AuditTarget,
            TargetId: organizationId.ToString("N"), Outcome: AuditOutcome.Succeeded, SourceApp: "PlatformBilling",
            DetailsJson: JsonSerializer.Serialize(details)), ct);
}
