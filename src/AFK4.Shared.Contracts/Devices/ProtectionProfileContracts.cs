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

    public static string Report(Guid deviceId) => $"/api/devices/{deviceId:D}/policy/report";
}

/// <summary>Что именно агент запрещает на ПК — по пункту на строку отчёта.</summary>
public static class ProtectionItemNames
{
    /// <summary>Постоянная основа киоска: меню Ctrl+Alt+Del без блокировки, выхода, смены пользователя и данных входа.</summary>
    public const string KioskBaseline = "kiosk-baseline";

    public const string RemovableStorage = "removable-storage";

    public const string BrowserDownloads = "browser-downloads";

    public const string BrowserIncognito = "browser-incognito";

    public const string BrowserUrlBlocklist = "browser-url-blocklist";

    public const string RunDialog = "run-dialog";

    public const string HiddenDrives = "hidden-drives";
}

/// <summary>
/// Что получилось с пунктом. Скрытие дисков — отдельный исход: диск пропал из Проводника, но
/// программа откроет его по пути, и называть это «запрещено» было бы неправдой (§6.3).
/// </summary>
public static class ProtectionItemStatusNames
{
    public const string Applied = "applied";

    /// <summary>Действует только в Проводнике: это не запрет.</summary>
    public const string ExplorerOnly = "explorer-only";

    public const string Failed = "failed";

    /// <summary>Здесь не применить: ПК не на Windows или агент без доступа к политикам машины.</summary>
    public const string Unsupported = "unsupported";

    /// <summary>Снято на время обслуживания.</summary>
    public const string Released = "released";
}

/// <summary>Строка отчёта: пункт, исход (ProtectionItemStatusNames) и подробность для разбора.</summary>
public sealed record ProtectionItemReportDto(
    /// Одно из ProtectionItemNames.
    string Item,
    /// Одно из ProtectionItemStatusNames.
    string Status,
    string? Detail);

/// <summary>Агент применил профиль (или снял его на обслуживание) и докладывает, что вышло.</summary>
public sealed record DeviceProtectionReportRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    int Version,
    DateTimeOffset AppliedAtUtc,
    IReadOnlyList<ProtectionItemReportDto> Items);

/// <summary>Последний отчёт ПК о защите — для карточки ПК в Панели.</summary>
public sealed record DeviceProtectionReportDto(
    int Version,
    DateTimeOffset AppliedAtUtc,
    IReadOnlyList<ProtectionItemReportDto> Items);
