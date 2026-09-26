using System.Security.Cryptography;
using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>
/// «Приведи клуб» (PRD, «SaaS Plans And Onboarding»): у клуба свой код; клуб, подключённый по нему,
/// оплатил первый счёт за подписку — пригласившему месяц бесплатно. Месяцы копятся: клуб на
/// бесплатном тарифе получит их, когда начнёт платить.
/// </summary>
public static class ClubReferrals
{
    // Без похожих знаков: код диктуют голосом и переписывают с экрана.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static async Task<string> EnsureCodeAsync(PlatformDbContext db, OrganizationEntity organization, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(organization.ReferralCode)) return organization.ReferralCode;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = "AFK-" + RandomNumberGenerator.GetString(Alphabet, 6);
            if (await db.Organizations.AnyAsync(candidate => candidate.ReferralCode == code, ct)) continue;
            organization.ReferralCode = code;
            await db.SaveChangesAsync(ct);
            return code;
        }

        throw new InvalidOperationException("A referral code could not be generated.");
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant().Replace(" ", string.Empty);

    /// <summary>Счёт оплачен: если это первый оплаченный счёт приведённого клуба — месяц пригласившему.</summary>
    public static async Task RewardIfFirstPaidAsync(PlatformDbContext db, IAuditRecordWriter? audit, InvoiceEntity invoice, DateTimeOffset now, CancellationToken ct)
    {
        if (invoice.Kind != InvoiceKindNames.Subscription || invoice.AmountMinorUnits <= 0) return;
        var organization = await db.Organizations.SingleOrDefaultAsync(candidate => candidate.OrganizationId == invoice.OrganizationId, ct);
        if (organization?.ReferredByOrganizationId is not { } referrerId || organization.ReferralRewardedAtUtc is not null) return;

        var referrer = await db.OrganizationSubscriptions.SingleOrDefaultAsync(candidate => candidate.OrganizationId == referrerId, ct);
        if (referrer is null) return;
        referrer.FreeMonths++;
        referrer.UpdatedAtUtc = now;
        organization.ReferralRewardedAtUtc = now;
        await db.SaveChangesAsync(ct);
        if (audit is not null)
        {
            await audit.WriteAsync(new AuditRecordWriteRequest(
                referrerId, BranchId: null, ActorStaffUserId: null, Action: AuditActionNames.RewardClubReferral, TargetType: "OrganizationSubscription",
                TargetId: referrerId.ToString("N"), Outcome: AuditOutcome.Succeeded, SourceApp: "PlatformBilling",
                DetailsJson: JsonSerializer.Serialize(new { ReferredOrganizationId = organization.OrganizationId, invoice.Number, referrer.FreeMonths })), ct);
        }
    }
}
