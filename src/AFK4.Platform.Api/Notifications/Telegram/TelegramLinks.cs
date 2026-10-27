using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>
/// Привязка чата Telegram к сотруднику. Панель выдаёт разовую ссылку на бота, человек открывает её
/// на телефоне, бот получает код и привязывает чат, из которого пришёл. Код одноразовый и короткоживущий:
/// чужой, увидевший QR на экране кассы через полчаса, ничего не привяжет.
/// </summary>
public sealed class TelegramLinks(
    PlatformDbContext db,
    ITelegramBotApi bot,
    IAuditRecordWriter audit,
    TimeProvider clock,
    IOptions<TelegramOptions> options)
{
    public async Task<StaffTelegramStatusDto> GetStatusAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var staff = await db.StaffUsers.AsNoTracking().SingleAsync(row => row.StaffUserId == staffUserId, cancellationToken);
        return new StaffTelegramStatusDto(options.Value.IsConfigured, staff.TelegramChatId is not null, staff.TelegramLinkedAtUtc);
    }

    public async Task<StaffTelegramLinkDto> CreateLinkAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace('+', '-').Replace('/', '_');
        var expiresAt = clock.GetUtcNow() + options.Value.LinkCodeLifetime;
        var staff = await db.StaffUsers.SingleAsync(row => row.StaffUserId == staffUserId, cancellationToken);
        staff.TelegramLinkCodeHash = Hash(code);
        staff.TelegramLinkCodeExpiresAtUtc = expiresAt;
        await db.SaveChangesAsync(cancellationToken);

        var botName = await bot.GetBotUserNameAsync(cancellationToken);
        return new StaffTelegramLinkDto($"https://t.me/{botName}?start={code}", expiresAt);
    }

    /// <returns>Сотрудник, к которому привязан чат, или null — код неизвестен или истёк.</returns>
    public async Task<StaffUserEntity?> LinkAsync(string code, long chatId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = Hash(code);
        var staff = await db.StaffUsers.SingleOrDefaultAsync(
            row => row.TelegramLinkCodeHash == hash && row.IsActive,
            cancellationToken);
        if (staff is null || staff.TelegramLinkCodeExpiresAtUtc is not { } expiresAt || expiresAt <= now)
        {
            return null;
        }

        staff.TelegramChatId = chatId;
        staff.TelegramLinkedAtUtc = now;
        staff.TelegramLinkCodeHash = null;
        staff.TelegramLinkCodeExpiresAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(staff, AuditActionNames.LinkStaffTelegram, cancellationToken);
        return staff;
    }

    public async Task UnlinkAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var staff = await db.StaffUsers.SingleAsync(row => row.StaffUserId == staffUserId, cancellationToken);
        await UnlinkAsync([staff], cancellationToken);
    }

    /// <summary>Команда /stop в боте: отвязать этот чат от всех, к кому он привязан.</summary>
    public async Task<int> UnlinkChatAsync(long chatId, CancellationToken cancellationToken)
    {
        var linked = await db.StaffUsers.Where(row => row.TelegramChatId == chatId).ToListAsync(cancellationToken);
        await UnlinkAsync(linked, cancellationToken);
        return linked.Count;
    }

    private async Task UnlinkAsync(IReadOnlyList<StaffUserEntity> staffUsers, CancellationToken cancellationToken)
    {
        var wasLinked = staffUsers.Where(staff => staff.TelegramChatId is not null).ToList();
        foreach (var staff in staffUsers)
        {
            staff.TelegramChatId = null;
            staff.TelegramLinkedAtUtc = null;
            staff.TelegramLinkCodeHash = null;
            staff.TelegramLinkCodeExpiresAtUtc = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var staff in wasLinked)
        {
            await WriteAuditAsync(staff, AuditActionNames.UnlinkStaffTelegram, cancellationToken);
        }
    }

    private Task WriteAuditAsync(StaffUserEntity staff, string action, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditRecordWriteRequest(
            staff.OrganizationId,
            BranchId: null,
            ActorStaffUserId: staff.StaffUserId,
            action,
            TargetType: "StaffUser",
            TargetId: staff.StaffUserId.ToString("D"),
            AuditOutcome.Succeeded,
            SourceApp: "PlatformApi",
            DetailsJson: JsonSerializer.Serialize(new { channel = "telegram" })),
            cancellationToken);

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code))).ToLowerInvariant();
}
