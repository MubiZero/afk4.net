using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Loyalty;

/// <summary>
/// «Приведи друга».
///
/// Игроки у нас не регистрируются сами — аккаунт заводит клуб на стойке, — поэтому код нельзя
/// назвать «при регистрации»: друг называет его уже в приложении, отдельным действием. Из этого
/// следует всё остальное устройство: окно приёма считается от заведения аккаунта, а платится
/// приглашение не за сам код, а за первое настоящее пополнение приглашённого.
/// </summary>
public sealed class ReferralService(
    PlatformDbContext dbContext,
    IOrganizationEntitlements entitlements,
    TimeProvider timeProvider) : IReferralService
{
    /// <summary>
    /// Алфавит кода: без похожих друг на друга знаков. Код называют голосом и переписывают от
    /// руки, а «0» против «O» и «1» против «I» — это чужой бонус и спор на стойке.
    /// </summary>
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private const int CodeLength = 6;

    public async Task<OrganizationReferralSettingsEntity?> GetActiveSettingsAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        // Общий рубильник тот же, что у кешбэка: выключенная тарифом лояльность не должна
        // продолжать раздавать деньги клуба мимо выключателя.
        if (!await entitlements.IsEnabledAsync(organizationId, PlatformFeatureNames.Loyalty, cancellationToken))
        {
            return null;
        }

        var settings = await dbContext.OrganizationReferralSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.OrganizationId == organizationId, cancellationToken);

        return settings is { Enabled: true } ? settings : null;
    }

    public async Task<string> EnsureCodeAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        var player = await dbContext.PlayerAccounts
            .SingleAsync(account => account.PlayerAccountId == playerAccountId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(player.ReferralCode))
        {
            return player.ReferralCode;
        }

        // Код уникален внутри клуба. Столкновения на шести знаках редки, но не невозможны, и
        // упасть на них значит не показать игроку экран — поэтому просто берём следующий.
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var candidate = GenerateCode(playerAccountId, attempt);
            var taken = await dbContext.PlayerAccounts.AnyAsync(
                account => account.OrganizationId == player.OrganizationId && account.ReferralCode == candidate,
                cancellationToken);
            if (taken)
            {
                continue;
            }

            player.ReferralCode = candidate;
            await dbContext.SaveChangesAsync(cancellationToken);
            return candidate;
        }

        throw new InvalidOperationException("Could not allocate a referral code.");
    }

    public async Task<ReferralClaimOutcome> ClaimAsync(
        Guid inviteePlayerAccountId,
        string code,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        if (normalized.Length != CodeLength)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.UnknownCode);
        }

        var invitee = await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(account => account.PlayerAccountId == inviteePlayerAccountId, cancellationToken);
        if (invitee is null)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.UnknownCode);
        }

        var settings = await GetActiveSettingsAsync(invitee.OrganizationId, cancellationToken);
        if (settings is null)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.Disabled);
        }

        var already = await dbContext.PlayerReferrals
            .AsNoTracking()
            .AnyAsync(row => row.InviteePlayerAccountId == inviteePlayerAccountId, cancellationToken);
        if (already)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.AlreadyClaimed);
        }

        var now = timeProvider.GetUtcNow();
        if (settings.ClaimWindowDays > 0 &&
            invitee.CreatedAtUtc.AddDays(settings.ClaimWindowDays) < now)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.WindowClosed);
        }

        // Код ищется только внутри своего клуба: приглашения не ходят между заведениями, и
        // платит по ним конкретный клуб.
        var referrer = await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                account =>
                    account.OrganizationId == invitee.OrganizationId &&
                    account.ReferralCode == normalized &&
                    account.IsActive,
                cancellationToken);
        if (referrer is null)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.UnknownCode);
        }

        if (referrer.PlayerAccountId == inviteePlayerAccountId)
        {
            return ReferralClaimOutcome.Fail(ReferralErrorCodes.OwnCode);
        }

        dbContext.PlayerReferrals.Add(new PlayerReferralEntity
        {
            InviteePlayerAccountId = inviteePlayerAccountId,
            ReferrerPlayerAccountId = referrer.PlayerAccountId,
            OrganizationId = invitee.OrganizationId,
            ClaimedAtUtc = now,
            ReferrerBonusMinorUnits = settings.ReferrerBonusMinorUnits,
            InviteeBonusMinorUnits = settings.InviteeBonusMinorUnits
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReferralClaimOutcome.Ok(referrer.DisplayName);
    }

    public async Task<IReadOnlyList<LedgerEntryEntity>> BuildTopUpRewardEntriesAsync(
        Guid organizationId,
        Guid branchId,
        Guid inviteePlayerAccountId,
        long topUpMinorUnits,
        string currencyCode,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        var referral = await dbContext.PlayerReferrals
            .SingleOrDefaultAsync(
                row => row.InviteePlayerAccountId == inviteePlayerAccountId && row.RewardedAtUtc == null,
                cancellationToken);
        if (referral is null)
        {
            return [];
        }

        var settings = await GetActiveSettingsAsync(organizationId, cancellationToken);
        if (settings is null)
        {
            return [];
        }

        // Суммы берутся те, что были обещаны при вводе кода: клуб мог поменять условия, но
        // договор с этим игроком уже состоялся.
        if (settings.MinimumTopUpMinorUnits > 0 && topUpMinorUnits < settings.MinimumTopUpMinorUnits)
        {
            return [];
        }

        if (settings.MaxRewardedPerReferrer > 0)
        {
            var rewarded = await dbContext.PlayerReferrals.CountAsync(
                row =>
                    row.ReferrerPlayerAccountId == referral.ReferrerPlayerAccountId &&
                    row.RewardedAtUtc != null,
                cancellationToken);
            if (rewarded >= settings.MaxRewardedPerReferrer)
            {
                // Лимит пригласившего не наказывает приглашённого: его бонус платится, потому
                // что он ничего не нарушал и о лимите не знал.
                referral.ReferrerBonusMinorUnits = 0;
            }
        }

        referral.RewardedAtUtc = createdAtUtc;
        referral.CurrencyCode = currencyCode;

        var entries = new List<LedgerEntryEntity>(2);
        if (referral.InviteeBonusMinorUnits > 0)
        {
            entries.Add(BuildBonusEntry(
                organizationId, branchId, inviteePlayerAccountId,
                referral.InviteeBonusMinorUnits, currencyCode,
                $"referral_bonus:invitee:{referral.ReferrerPlayerAccountId:D}", createdAtUtc));
        }

        if (referral.ReferrerBonusMinorUnits > 0)
        {
            entries.Add(BuildBonusEntry(
                organizationId, branchId, referral.ReferrerPlayerAccountId,
                referral.ReferrerBonusMinorUnits, currencyCode,
                $"referral_bonus:referrer:{inviteePlayerAccountId:D}", createdAtUtc.AddTicks(1)));
        }

        return entries;
    }

    private static LedgerEntryEntity BuildBonusEntry(
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        long amountMinorUnits,
        string currencyCode,
        string reason,
        DateTimeOffset createdAtUtc) =>
        BillingEntryFactory.Create(
            organizationId,
            branchId,
            playerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.ReferralBonus,
            LedgerAccountTypeNames.Wallet,
            amountMinorUnits,
            quantitySeconds: 0,
            currencyCode,
            description: LedgerEntryTypeNames.ReferralBonus,
            reason,
            reversesLedgerEntryId: null,
            // Начисление системное, а не кассовое: смены у него нет и быть не должно.
            actorStaffUserId: Guid.Empty,
            createdAtUtc);

    public static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant().Replace(" ", string.Empty);

    /// <summary>
    /// Код из идентификатора игрока: одинаковый на повторных попытках и не подряд идущий, чтобы
    /// чужой код нельзя было получить прибавлением единицы.
    /// </summary>
    private static string GenerateCode(Guid playerAccountId, int attempt)
    {
        var bytes = playerAccountId.ToByteArray();
        unchecked
        {
            var hash = 1469598103934665603UL;
            foreach (var value in bytes)
            {
                hash = (hash ^ value) * 1099511628211UL;
            }

            hash = (hash ^ (ulong)attempt) * 1099511628211UL;

            var code = new char[CodeLength];
            for (var index = 0; index < CodeLength; index++)
            {
                code[index] = CodeAlphabet[(int)(hash % (ulong)CodeAlphabet.Length)];
                hash /= (ulong)CodeAlphabet.Length;
            }

            return new string(code);
        }
    }
}
