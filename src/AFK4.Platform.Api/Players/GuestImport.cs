using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

/// <summary>
/// Перенос гостей из прежней программы (план `2026-09-25-guest-import.md`). Строка — номер, имя,
/// баланс и бонусы; номер ищется среди гостей клуба, нет — заводится карточка. Деньги ложатся
/// записями <c>opening_balance</c> на кошелёк: не выручка, не наличные смены. Гостю, которому
/// остатки уже переносили, второй раз не переносится.
/// </summary>
public sealed class GuestImport(PlatformDbContext db, TimeProvider clock)
{
    private const string Operation = "guest-import";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Line(int Row, string Phone, string Name, long Balance, long Bonus);

    public async Task<GuestImportResultDto> RunAsync(
        Guid organizationId, Guid branchId, Guid actorStaffUserId, GuestImportRequest request, CancellationToken ct)
    {
        var keyHash = BillingCommandIdempotencyKeyHasher.Hash(request.IdempotencyKey);
        if (!request.DryRun && await ReplayAsync(organizationId, branchId, keyHash, ct) is { } replay) return replay;

        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var issues = new List<GuestImportIssueDto>();
        var lines = new List<Line>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            var number = index + 1;
            var phone = NormalizePhone(row.Phone);
            if (phone is null) { issues.Add(new(number, GuestImportIssueNames.InvalidPhone)); continue; }
            var name = row.Name?.Trim() ?? string.Empty;
            if (name.Length == 0) { issues.Add(new(number, GuestImportIssueNames.MissingName)); continue; }
            if (row.BalanceMinorUnits < 0 || row.BonusMinorUnits < 0
                || row.BalanceMinorUnits > GuestImportLimits.MaxAmountMinorUnits || row.BonusMinorUnits > GuestImportLimits.MaxAmountMinorUnits)
            {
                issues.Add(new(number, GuestImportIssueNames.NegativeAmount));
                continue;
            }

            if (!seen.Add(phone)) { issues.Add(new(number, GuestImportIssueNames.DuplicateInFile)); continue; }
            lines.Add(new Line(number, phone, name.Length > GuestImportLimits.NameMax ? name[..GuestImportLimits.NameMax] : name,
                row.BalanceMinorUnits, row.BonusMinorUnits));
        }

        await using var transaction = request.DryRun ? null : await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var existing = await db.PlayerAccounts
            .Where(player => player.OrganizationId == organizationId && player.PhoneNumber != null)
            .ToListAsync(ct);
        var byPhone = existing
            .GroupBy(player => NormalizePhone(player.PhoneNumber) ?? string.Empty)
            .Where(group => group.Key.Length > 0)
            .ToDictionary(group => group.Key, group => group.OrderBy(player => player.CreatedAtUtc).First());
        var imported = (await db.LedgerEntries.AsNoTracking()
                .Where(entry => entry.OrganizationId == organizationId && entry.EntryType == LedgerEntryTypeNames.OpeningBalance)
                .Select(entry => entry.PlayerAccountId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var now = clock.GetUtcNow();
        var created = 0;
        var matched = 0;
        long balanceTotal = 0;
        long bonusTotal = 0;
        foreach (var line in lines)
        {
            if (byPhone.TryGetValue(line.Phone, out var player))
            {
                if (imported.Contains(player.PlayerAccountId)) { issues.Add(new(line.Row, GuestImportIssueNames.AlreadyImported)); continue; }
                matched++;
            }
            else
            {
                created++;
                player = new PlayerAccountEntity
                {
                    PlayerAccountId = Guid.NewGuid(), OrganizationId = organizationId, HomeBranchId = branchId,
                    DisplayName = line.Name, PhoneNumber = "+" + line.Phone, IsActive = true, CreatedAtUtc = now
                };
                if (!request.DryRun) db.PlayerAccounts.Add(player);
                byPhone[line.Phone] = player;
            }

            balanceTotal += line.Balance;
            bonusTotal += line.Bonus;
            if (request.DryRun) continue;
            AddOpening(player, organizationId, branchId, actorStaffUserId, line.Balance, "balance", request.Source, currency, now);
            AddOpening(player, organizationId, branchId, actorStaffUserId, line.Bonus, "bonus", request.Source, currency, now);
            imported.Add(player.PlayerAccountId);
        }

        var result = new GuestImportResultDto(
            !request.DryRun, request.Rows.Count, created, matched, request.Rows.Count - created - matched,
            new MoneyDto(currency, balanceTotal), new MoneyDto(currency, bonusTotal),
            issues.OrderBy(issue => issue.Row).ToList());
        if (request.DryRun) return result;

        db.BillingCommandIdempotency.Add(new BillingCommandIdempotencyEntity
        {
            BillingCommandIdempotencyId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId, Operation = Operation,
            IdempotencyKeyHash = keyHash, RequestHash = Hash(request), ResponseJson = JsonSerializer.Serialize(result, Json),
            CreatedAtUtc = now, ExpiresAtUtc = now.AddDays(7)
        });
        await db.SaveChangesAsync(ct);
        await transaction!.CommitAsync(ct);
        return result;
    }

    /// <summary>
    /// Номер в выгрузке прежней программы бывает местным — девять цифр таджикского мобильного без
    /// кода страны. Такой получает +992; остальное — как в регистрации: код страны обязателен.
    /// </summary>
    public static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 9) digits = "992" + digits;
        return PhoneNumberNormalizer.Normalize(digits);
    }

    private void AddOpening(PlayerAccountEntity player, Guid organizationId, Guid branchId, Guid actor, long amount,
        string kind, string source, string currency, DateTimeOffset now)
    {
        if (amount <= 0) return;
        db.LedgerEntries.Add(BillingEntryFactory.Create(
            organizationId, branchId, player.PlayerAccountId, sessionId: null, playerPackageId: null,
            LedgerEntryTypeNames.OpeningBalance, LedgerAccountTypeNames.Wallet, amount, quantitySeconds: 0, currency,
            description: kind, reason: string.IsNullOrWhiteSpace(source) ? "import" : source.Trim()[..Math.Min(source.Trim().Length, GuestImportLimits.SourceMax)],
            reversesLedgerEntryId: null, actor, now,
            // Смены нет: наличные в ящике не двигались, деньги гость отдал прежней программе.
            shiftId: null));
    }

    private async Task<GuestImportResultDto?> ReplayAsync(Guid organizationId, Guid branchId, string keyHash, CancellationToken ct)
    {
        var stored = await db.BillingCommandIdempotency.AsNoTracking().SingleOrDefaultAsync(record =>
            record.OrganizationId == organizationId && record.BranchId == branchId && record.Operation == Operation
            && record.IdempotencyKeyHash == keyHash, ct);
        return stored is null ? null : JsonSerializer.Deserialize<GuestImportResultDto>(stored.ResponseJson, Json);
    }

    private static string Hash(GuestImportRequest request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request with { DryRun = false }, Json))));
}
