namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Профиль защиты ПК филиала (спека оболочки, §6.3): что агент запрещает на игровом ПК. Версия
/// растёт с каждым сохранением и едет в сердцебиении — по её смене агент перечитывает профиль.
/// Версия 0 — клуб профиль не настраивал, действует только постоянная база киоска.
/// </summary>
public sealed record ProtectionProfileDto(
    int Version,
    /// Флешки и внешние диски — запрет Windows на все съёмные накопители.
    bool BlockRemovableStorage,
    /// Скачивание в Chrome и Edge.
    bool BlockBrowserDownloads,
    /// Режим инкогнито в Chrome и InPrivate в Edge.
    bool BlockBrowserIncognito,
    /// Окно «Выполнить» (Win+R).
    bool DisableRunDialog,
    /// Буквы дисков, скрытых в Проводнике. Это не запрет: программа откроет диск по пути.
    IReadOnlyList<string> HiddenDrives,
    /// Адреса и шаблоны, которые Chrome и Edge не открывают (формат URLBlocklist).
    IReadOnlyList<string> UrlBlocklist,
    /// Окна, которые оболочка закрывает, едва они появятся.
    IReadOnlyList<BlockedWindowRuleDto> BlockedWindows);

/// <summary>Правило закрытия окна: часть заголовка, класс окна или оба сразу.</summary>
public sealed record BlockedWindowRuleDto(string? TitleContains, string? ClassName);

/// <summary>Профиль защиты филиала для Панели: сам профиль и кто его менял последним.</summary>
public sealed record BranchProtectionProfileDto(
    Guid OrganizationId,
    Guid BranchId,
    ProtectionProfileDto Profile,
    DateTimeOffset? UpdatedAtUtc);

/// <summary>
/// Сохранить профиль. <paramref name="ExpectedVersion"/> — версия, которую человек открыл: если
/// профиль успели поменять, сохранение отказывает, а не затирает чужую правку молча.
/// </summary>
public sealed record UpdateBranchProtectionProfileRequest(
    Guid OrganizationId,
    int ExpectedVersion,
    bool BlockRemovableStorage,
    bool BlockBrowserDownloads,
    bool BlockBrowserIncognito,
    bool DisableRunDialog,
    IReadOnlyList<string> HiddenDrives,
    IReadOnlyList<string> UrlBlocklist,
    IReadOnlyList<BlockedWindowRuleDto> BlockedWindows);

public static class ProtectionProfileErrorCodeNames
{
    /// <summary>Профиль успели сохранить после того, как его открыли: нужно перечитать.</summary>
    public const string VersionConflict = "protection_profile_version_conflict";
}

/// <summary>Пути профиля защиты для агента.</summary>
public static class DeviceProtectionRoutes
{
    public static string Profile(Guid deviceId, Guid organizationId, Guid branchId) =>
        $"/api/devices/{deviceId:D}/policy?organizationId={organizationId:D}&branchId={branchId:D}";
}
