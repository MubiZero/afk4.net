using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Чтение, проверка и сохранение профиля защиты филиала — одно место на Панель и агента.</summary>
public static class ProtectionProfiles
{
    public const int MaxUrlBlocklistEntries = 200;
    public const int MaxUrlLength = 2048;
    public const int MaxBlockedWindows = 100;
    public const int MaxWindowPatternLength = 256;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly ProtectionProfileDto Empty = new(0, false, false, false, false, [], [], [], SessionTraceNames.All);

    public static ProtectionProfileDto For(BranchProtectionProfileEntity? entity) =>
        entity is null
            ? Empty
            : new ProtectionProfileDto(
                entity.Version,
                entity.BlockRemovableStorage,
                entity.BlockBrowserDownloads,
                entity.BlockBrowserIncognito,
                entity.DisableRunDialog,
                entity.HiddenDrives.Select(letter => letter.ToString()).ToList(),
                JsonSerializer.Deserialize<List<string>>(entity.UrlBlocklistJson, Json) ?? [],
                JsonSerializer.Deserialize<List<BlockedWindowRuleDto>>(entity.BlockedWindowsJson, Json) ?? [],
                JsonSerializer.Deserialize<List<string>>(entity.ClearAfterSessionJson, Json) ?? []);

    public static async Task<ProtectionProfileDto> ResolveAsync(
        PlatformDbContext dbContext, Guid branchId, CancellationToken cancellationToken) =>
        For(await dbContext.BranchProtectionProfiles.AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.BranchId == branchId, cancellationToken));

    /// <summary>Что не так с запросом, по-английски для журнала; null — всё в порядке.</summary>
    public static string? Validate(UpdateBranchProtectionProfileRequest request)
    {
        if (request.ExpectedVersion < 0)
        {
            return "ExpectedVersion cannot be negative.";
        }

        if (request.HiddenDrives.Any(drive => drive.Length != 1 || !char.IsAsciiLetter(drive[0])))
        {
            return "HiddenDrives must be single drive letters A-Z.";
        }

        if (request.UrlBlocklist.Count > MaxUrlBlocklistEntries)
        {
            return $"UrlBlocklist holds at most {MaxUrlBlocklistEntries} entries.";
        }

        if (request.UrlBlocklist.Any(url => string.IsNullOrWhiteSpace(url) || url.Trim().Length > MaxUrlLength))
        {
            return $"Every UrlBlocklist entry must be non-empty and at most {MaxUrlLength} characters.";
        }

        if (request.ClearAfterSession.Any(item => !SessionTraceNames.All.Contains(item)))
        {
            return $"ClearAfterSession takes only: {string.Join(", ", SessionTraceNames.All)}.";
        }

        if (request.BlockedWindows.Count > MaxBlockedWindows)
        {
            return $"BlockedWindows holds at most {MaxBlockedWindows} rules.";
        }

        foreach (var rule in request.BlockedWindows)
        {
            if (string.IsNullOrWhiteSpace(rule.TitleContains) && string.IsNullOrWhiteSpace(rule.ClassName))
            {
                return "Every BlockedWindows rule needs a title fragment or a window class.";
            }

            if ((rule.TitleContains?.Trim().Length ?? 0) > MaxWindowPatternLength ||
                (rule.ClassName?.Trim().Length ?? 0) > MaxWindowPatternLength)
            {
                return $"Window patterns are at most {MaxWindowPatternLength} characters.";
            }
        }

        return null;
    }

    /// <summary>
    /// Записать профиль. null — версия уже не та, что открывал человек: кто-то сохранил раньше.
    /// Списки нормализуются здесь: пробелы по краям, повторы и порядок дисков не должны давать
    /// новую версию и лишний круг перечитывания на всех ПК.
    /// </summary>
    public static async Task<ProtectionProfileDto?> SaveAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        Guid staffUserId,
        UpdateBranchProtectionProfileRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.BranchProtectionProfiles
            .SingleOrDefaultAsync(profile => profile.BranchId == branchId, cancellationToken);
        var currentVersion = entity?.Version ?? 0;
        if (request.ExpectedVersion != currentVersion)
        {
            return null;
        }

        var isNew = entity is null;
        if (entity is null)
        {
            entity = new BranchProtectionProfileEntity { BranchId = branchId, OrganizationId = organizationId };
            dbContext.BranchProtectionProfiles.Add(entity);
        }

        entity.Version = currentVersion + 1;
        entity.BlockRemovableStorage = request.BlockRemovableStorage;
        entity.BlockBrowserDownloads = request.BlockBrowserDownloads;
        entity.BlockBrowserIncognito = request.BlockBrowserIncognito;
        entity.DisableRunDialog = request.DisableRunDialog;
        entity.HiddenDrives = new string(request.HiddenDrives
            .Select(drive => char.ToUpperInvariant(drive[0]))
            .Distinct()
            .Order()
            .ToArray());
        entity.UrlBlocklistJson = JsonSerializer.Serialize(
            request.UrlBlocklist.Select(url => url.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), Json);
        entity.BlockedWindowsJson = JsonSerializer.Serialize(
            request.BlockedWindows
                .Select(rule => new BlockedWindowRuleDto(Blank(rule.TitleContains), Blank(rule.ClassName)))
                .Distinct()
                .ToList(),
            Json);
        // В порядке каталога: порядок галочек в Панели не должен давать новую версию.
        entity.ClearAfterSessionJson = JsonSerializer.Serialize(
            SessionTraceNames.All.Where(request.ClearAfterSession.Contains).ToList(), Json);
        entity.UpdatedAtUtc = now;
        entity.UpdatedByStaffUserId = staffUserId;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Двое нажали «Сохранить» одновременно: база пропустила первого, второй перечитывает.
            return null;
        }
        catch (DbUpdateException) when (isNew)
        {
            // То же при самом первом сохранении: строку филиала успел завести другой.
            return null;
        }

        return For(entity);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
