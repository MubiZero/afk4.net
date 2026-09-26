using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Tips;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tips;

/// <summary>
/// Чаевые администратору смены (спека `2026-09-25-visit-tips-design.md`). Деньги уходят с кошелька
/// игрока записью <c>tip</c> со сменой и сессией; ни один расчёт выручки этот вид не видит.
/// Возврат — записью <c>reversal</c>: <c>refund</c> уменьшил бы выручку за игру.
/// </summary>
public sealed class VisitTips(PlatformDbContext db, TimeProvider clock)
{
    private const string Operation = "visit-tip";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record GiveResult(PlayerTipResponse? Response, string? Refusal, bool NotFound = false);

    private sealed record Visit(Guid OrganizationId, Guid BranchId, string State, DateTimeOffset? EndedAtUtc);

    private sealed record Recipient(Guid ShiftId, string Name);

    public async Task<PlayerTipOfferDto?> OfferAsync(Guid playerAccountId, Guid sessionId, CancellationToken ct)
    {
        var visit = await FindVisitAsync(playerAccountId, sessionId, ct);
        if (visit is null) return null;

        var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(db, playerAccountId, ct);
        var currency = wallet?.WalletBalance.CurrencyCode ?? "TJS";
        var balance = wallet?.WalletBalance ?? new MoneyDto(currency, 0);
        var presets = TipLimits.PresetMinorUnits.Select(units => new MoneyDto(currency, units)).ToList();
        var given = await GivenAsync(sessionId, ct);
        var recipient = await RecipientAsync(visit, ct);
        var refusal = await RefusalAsync(visit, given, recipient, balance, ct);

        return new PlayerTipOfferDto(
            refusal is null, refusal, presets, balance, recipient?.Name,
            given > 0 ? new MoneyDto(currency, given) : null);
    }

    public async Task<GiveResult> GiveAsync(Guid playerAccountId, Guid sessionId, PlayerTipRequest request, CancellationToken ct)
    {
        var visit = await FindVisitAsync(playerAccountId, sessionId, ct);
        if (visit is null) return new GiveResult(null, null, NotFound: true);
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return new GiveResult(null, TipUnavailableReasonNames.InvalidAmount);

        var keyHash = BillingCommandIdempotencyKeyHasher.Hash(request.IdempotencyKey);
        var requestHash = Hash(new { playerAccountId, sessionId, request.Amount });
        if (await ReplayAsync(visit, keyHash, requestHash, ct) is { } replay) return replay;

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(db, playerAccountId, ct);
            var balance = wallet?.WalletBalance ?? new MoneyDto(request.Amount.CurrencyCode, 0);
            if (!TipLimits.PresetMinorUnits.Contains(request.Amount.MinorUnits)
                || !string.Equals(request.Amount.CurrencyCode, balance.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                return new GiveResult(null, TipUnavailableReasonNames.InvalidAmount);
            }

            var recipient = await RecipientAsync(visit, ct);
            var refusal = await RefusalAsync(visit, await GivenAsync(sessionId, ct), recipient, balance, ct);
            if (refusal is null && balance.MinorUnits < request.Amount.MinorUnits) refusal = TipUnavailableReasonNames.NotEnoughBalance;
            if (refusal is not null) return new GiveResult(null, refusal);

            var now = clock.GetUtcNow();
            db.LedgerEntries.Add(BillingEntryFactory.Create(
                visit.OrganizationId, visit.BranchId, playerAccountId, sessionId, playerPackageId: null,
                LedgerEntryTypeNames.Tip, LedgerAccountTypeNames.Wallet, -request.Amount.MinorUnits, quantitySeconds: 0,
                balance.CurrencyCode, description: LedgerEntryTypeNames.Tip, reason: LedgerEntryTypeNames.Tip,
                // Получатель — тот, кто открыл смену записи: его не нужно хранить второй раз.
                reversesLedgerEntryId: null, SystemActorIds.PlayerSelfService, now, recipient!.ShiftId));
            var response = new PlayerTipResponse(
                new MoneyDto(balance.CurrencyCode, request.Amount.MinorUnits),
                new MoneyDto(balance.CurrencyCode, balance.MinorUnits - request.Amount.MinorUnits),
                recipient.Name);
            db.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
            {
                BillingCommandIdempotencyId = Guid.NewGuid(), OrganizationId = visit.OrganizationId, BranchId = visit.BranchId,
                Operation = Operation, IdempotencyKeyHash = keyHash, RequestHash = requestHash,
                ResponseJson = JsonSerializer.Serialize(response, Json), CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(1)
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new GiveResult(response, null);
        }
        catch (DbUpdateException)
        {
            // Тот же ключ пришёл дважды одновременно: второй упёрся в ключ и отвечает ответом первого.
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return await ReplayAsync(visit, keyHash, requestHash, ct) ?? throw new InvalidOperationException("The tip could not be recorded.");
        }
    }

    public async Task<ShiftTipsDto?> ForShiftAsync(Guid organizationId, Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking()
            .Where(candidate => candidate.ShiftId == shiftId && candidate.OrganizationId == organizationId)
            .Select(candidate => new { candidate.ShiftId, candidate.OpenedByStaffUserId, candidate.CurrencyCode })
            .SingleOrDefaultAsync(ct);
        if (shift is null) return null;

        var tips = await db.LedgerEntries.AsNoTracking()
            .Where(entry => entry.ShiftId == shiftId && entry.EntryType == LedgerEntryTypeNames.Tip)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .Select(entry => new
            {
                entry.LedgerEntryId, entry.AmountMinorUnits, entry.CurrencyCode, entry.CreatedAtUtc,
                SeatLabel = db.Sessions.Where(session => session.SessionId == entry.SessionId)
                    .Join(db.Seats, session => session.SeatId, seat => seat.SeatId, (_, seat) => seat.Name)
                    .FirstOrDefault(),
                Reversed = db.LedgerEntries.Any(reversal => reversal.ReversesLedgerEntryId == entry.LedgerEntryId)
            })
            .ToListAsync(ct);
        var name = await StaffNameAsync(shift.OpenedByStaffUserId, ct);
        var currency = tips.FirstOrDefault()?.CurrencyCode ?? shift.CurrencyCode;
        var paidOut = await db.ShiftTipPayouts.AsNoTracking()
            .Where(payout => payout.ShiftId == shiftId)
            .SumAsync(payout => payout.AmountMinorUnits, ct);
        return new ShiftTipsDto(
            shift.ShiftId,
            shift.OpenedByStaffUserId,
            name,
            new MoneyDto(currency, tips.Where(tip => !tip.Reversed).Sum(tip => -tip.AmountMinorUnits)),
            tips.Select(tip => new ShiftTipDto(tip.LedgerEntryId, new MoneyDto(tip.CurrencyCode, -tip.AmountMinorUnits), tip.SeatLabel, tip.CreatedAtUtc, tip.Reversed))
                .ToList(),
            new MoneyDto(currency, paidOut));
    }

    /// <summary>
    /// Выдать невыданное наличными: обычная выдача из кассы (ожидаемая сумма в ящике сходится) и
    /// отметка, что эта сумма выдана. Повтор с тем же ключом отдаёт ту же выдачу.
    /// </summary>
    public async Task<(ShiftTipsDto? Tips, string? Error)> PayOutAsync(
        IShiftService shifts, Guid organizationId, Guid shiftId, Guid actorStaffUserId, string idempotencyKey, CancellationToken ct)
    {
        var current = await ForShiftAsync(organizationId, shiftId, ct);
        if (current is null) return (null, null);
        var unpaid = current.Total.MinorUnits - (current.PaidOut?.MinorUnits ?? 0);
        var request = new RecordCashMovementRequest(
            organizationId, CashMovementTypeNames.CashOut, new MoneyDto(current.Total.CurrencyCode, unpaid),
            $"Чаевые: {current.RecipientName}".Trim(), idempotencyKey);
        if (unpaid <= 0) return (current, TipErrorCodeNames.NothingToPay);

        var movement = await shifts.RecordCashMovementAsync(shiftId, actorStaffUserId, request, ct);
        if (movement.Response is null) return (current, movement.Code ?? movement.Error ?? TipErrorCodeNames.ShiftClosed);

        if (!await db.ShiftTipPayouts.AnyAsync(payout => payout.CashMovementId == movement.Response.CashMovementId, ct))
        {
            db.ShiftTipPayouts.Add(new ShiftTipPayoutEntity
            {
                ShiftTipPayoutId = Guid.NewGuid(), OrganizationId = organizationId, ShiftId = shiftId,
                CashMovementId = movement.Response.CashMovementId, AmountMinorUnits = movement.Response.Amount.MinorUnits,
                CreatedByStaffUserId = actorStaffUserId, CreatedAtUtc = clock.GetUtcNow()
            });
            await db.SaveChangesAsync(ct);
        }

        return (await ForShiftAsync(organizationId, shiftId, ct), null);
    }

    public enum ReverseOutcome { Reversed, NotFound, ShiftClosed, AlreadyReversed }

    public async Task<ReverseOutcome> ReverseAsync(Guid organizationId, Guid shiftId, Guid ledgerEntryId, Guid actorStaffUserId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var tip = await db.LedgerEntries.AsNoTracking().SingleOrDefaultAsync(entry =>
            entry.LedgerEntryId == ledgerEntryId && entry.ShiftId == shiftId && entry.OrganizationId == organizationId
            && entry.EntryType == LedgerEntryTypeNames.Tip, ct);
        if (tip is null) return ReverseOutcome.NotFound;

        var shiftOpen = await db.Shifts.AnyAsync(shift => shift.ShiftId == shiftId && shift.State == ShiftStateNames.Open, ct);
        if (!shiftOpen) return ReverseOutcome.ShiftClosed;
        if (await db.LedgerEntries.AnyAsync(entry => entry.ReversesLedgerEntryId == ledgerEntryId, ct)) return ReverseOutcome.AlreadyReversed;

        db.LedgerEntries.Add(BillingEntryFactory.Create(
            tip.OrganizationId, tip.BranchId, tip.PlayerAccountId, tip.SessionId, playerPackageId: null,
            LedgerEntryTypeNames.Reversal, LedgerAccountTypeNames.Wallet, -tip.AmountMinorUnits, quantitySeconds: 0,
            tip.CurrencyCode, description: LedgerEntryTypeNames.Tip, reason: "tip_reversal",
            reversesLedgerEntryId: tip.LedgerEntryId, actorStaffUserId, clock.GetUtcNow(), shiftId));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ReverseOutcome.Reversed;
    }

    private async Task<string?> RefusalAsync(Visit visit, long given, Recipient? recipient, MoneyDto balance, CancellationToken ct)
    {
        if (visit.State != SessionStateNames.Ended) return TipUnavailableReasonNames.NotEnded;
        var enabled = await db.OrganizationTipSettings.AsNoTracking()
            .AnyAsync(settings => settings.OrganizationId == visit.OrganizationId && settings.Enabled, ct);
        if (!enabled) return TipUnavailableReasonNames.Disabled;
        if (visit.EndedAtUtc is null || clock.GetUtcNow() - visit.EndedAtUtc > TipLimits.Window) return TipUnavailableReasonNames.TooLate;
        if (given > 0) return TipUnavailableReasonNames.AlreadyTipped;
        if (recipient is null) return TipUnavailableReasonNames.NoShift;
        return balance.MinorUnits < TipLimits.PresetMinorUnits.Min() ? TipUnavailableReasonNames.NotEnoughBalance : null;
    }

    private Task<Visit?> FindVisitAsync(Guid playerAccountId, Guid sessionId, CancellationToken ct) =>
        db.Sessions.AsNoTracking()
            .Where(session => session.SessionId == sessionId && session.PlayerAccountId == playerAccountId)
            .Select(session => new Visit(session.OrganizationId, session.BranchId, session.State, session.EndedAtUtc))
            .FirstOrDefaultAsync(ct);

    // Уже оставленные чаевые визита — без возвращённых.
    private async Task<long> GivenAsync(Guid sessionId, CancellationToken ct) =>
        -await db.LedgerEntries
            .Where(entry => entry.SessionId == sessionId && entry.EntryType == LedgerEntryTypeNames.Tip
                && !db.LedgerEntries.Any(reversal => reversal.ReversesLedgerEntryId == entry.LedgerEntryId))
            .SumAsync(entry => entry.AmountMinorUnits, ct);

    private async Task<Recipient?> RecipientAsync(Visit visit, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking()
            .Where(candidate => candidate.OrganizationId == visit.OrganizationId && candidate.BranchId == visit.BranchId
                && candidate.State == ShiftStateNames.Open)
            .OrderByDescending(candidate => candidate.OpenedAtUtc)
            .Select(candidate => new { candidate.ShiftId, candidate.OpenedByStaffUserId })
            .FirstOrDefaultAsync(ct);
        return shift is null ? null : new Recipient(shift.ShiftId, await StaffNameAsync(shift.OpenedByStaffUserId, ct));
    }

    private async Task<string> StaffNameAsync(Guid staffUserId, CancellationToken ct)
    {
        var name = await db.StaffUsers.AsNoTracking()
            .Where(staff => staff.StaffUserId == staffUserId)
            .Select(staff => staff.DisplayName)
            .SingleOrDefaultAsync(ct) ?? string.Empty;
        // «Шерзод Каримов» → «Шерзод»: на экране ПК — по имени, фамилия там ни к чему.
        return name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
    }

    private async Task<GiveResult?> ReplayAsync(Visit visit, string keyHash, string requestHash, CancellationToken ct)
    {
        var stored = await db.BillingCommandIdempotency.AsNoTracking().SingleOrDefaultAsync(record =>
            record.OrganizationId == visit.OrganizationId && record.BranchId == visit.BranchId
            && record.Operation == Operation && record.IdempotencyKeyHash == keyHash, ct);
        if (stored is null) return null;
        // Тот же ключ с другой суммой — не повтор, а ошибка клиента.
        return stored.RequestHash == requestHash
            ? new GiveResult(JsonSerializer.Deserialize<PlayerTipResponse>(stored.ResponseJson, Json), null)
            : new GiveResult(null, TipUnavailableReasonNames.InvalidAmount);
    }

    private static string Hash(object request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json))));
}
