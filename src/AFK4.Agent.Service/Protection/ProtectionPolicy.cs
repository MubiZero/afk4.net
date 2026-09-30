using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Protection;

/// <summary>
/// Чьей ветки реестра касается запись. Машинная (HKLM) действует на всех, включая администратора
/// и техника; игрока — только запись в его кусте (HKU\&lt;SID&gt;): так запрет «Выполнить» не мешает
/// тому, кто обслуживает ПК под своей учёткой.
/// </summary>
public enum PolicyScope
{
    Machine,
    Player
}

/// <summary>Одна запись: число, строка или список строк подключом «1», «2»… (как URLBlocklist).</summary>
public sealed record RegistryWrite(
    string Key, string Name, int? Number = null, string? Text = null, IReadOnlyList<string>? List = null,
    PolicyScope Scope = PolicyScope.Machine)
{
    public bool IsList => List is not null;
}

/// <summary>
/// Пункт защиты: что он пишет, когда включён, и каким исходом его честно назвать. Выключенный
/// пункт свои значения удаляет — иначе снятая в Панели галочка продолжала бы запрещать.
/// </summary>
public sealed record ProtectionItem(string Name, bool Enabled, IReadOnlyList<RegistryWrite> Writes, string AppliedStatus);

/// <summary>
/// Профиль защиты → записи реестра (спека оболочки, §6.3). Чистая функция: какие ключи и значения
/// — решается здесь и проверяется тестами на любой ОС; Windows только исполняет.
/// </summary>
public static class ProtectionPolicy
{
    public const string SystemPolicies = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    public const string ExplorerPolicies = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer";
    public const string RemovableStorage = @"SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices";
    public const string Chrome = @"SOFTWARE\Policies\Google\Chrome";
    public const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";

    // Всё, что Windows умеет по-пользовательски, ставится игроку; машинным остаётся только то, что
    // по-другому не работает: HideFastUserSwitching читается лишь из HKLM, а Chrome и Edge берут
    // обязательные политики из машинной ветки.
    private static RegistryWrite ForPlayer(string key, string name, int number) =>
        new(key, name, Number: number, Scope: PolicyScope.Player);

    public static IReadOnlyList<ProtectionItem> Plan(ProtectionProfileDto profile) =>
    [
        // Основа киоска — всегда вне обслуживания (§6.2): Ctrl+Alt+Del не перехватить, режут меню.
        new(ProtectionItemNames.KioskBaseline, true,
        [
            ForPlayer(SystemPolicies, "DisableLockWorkstation", 1),
            ForPlayer(SystemPolicies, "DisableChangePassword", 1),
            new(SystemPolicies, "HideFastUserSwitching", Number: 1),
            ForPlayer(ExplorerPolicies, "NoLogoff", 1)
        ], ProtectionItemStatusNames.Applied),
        new(ProtectionItemNames.RemovableStorage, profile.BlockRemovableStorage,
            [ForPlayer(RemovableStorage, "Deny_All", 1)], ProtectionItemStatusNames.Applied),
        new(ProtectionItemNames.BrowserDownloads, profile.BlockBrowserDownloads,
        [
            // 3 — «запретить все загрузки».
            new(Chrome, "DownloadRestrictions", Number: 3),
            new(Edge, "DownloadRestrictions", Number: 3)
        ], ProtectionItemStatusNames.Applied),
        new(ProtectionItemNames.BrowserIncognito, profile.BlockBrowserIncognito,
        [
            // 1 — «режим недоступен»; у Edge своё имя для того же.
            new(Chrome, "IncognitoModeAvailability", Number: 1),
            new(Edge, "InPrivateModeAvailability", Number: 1)
        ], ProtectionItemStatusNames.Applied),
        new(ProtectionItemNames.BrowserUrlBlocklist, profile.UrlBlocklist.Count > 0,
        [
            new(Chrome, "URLBlocklist", List: profile.UrlBlocklist),
            new(Edge, "URLBlocklist", List: profile.UrlBlocklist)
        ], ProtectionItemStatusNames.Applied),
        new(ProtectionItemNames.RunDialog, profile.DisableRunDialog,
            [ForPlayer(ExplorerPolicies, "NoRun", 1)], ProtectionItemStatusNames.Applied),
        // Скрытые диски — только в Проводнике. Программа откроет диск по пути, и отчёт так и говорит.
        new(ProtectionItemNames.HiddenDrives, profile.HiddenDrives.Count > 0,
            [ForPlayer(ExplorerPolicies, "NoDrives", DriveMask(profile.HiddenDrives))], ProtectionItemStatusNames.ExplorerOnly)
    ];

    /// <summary>NoDrives — битовая маска: бит 0 — A, бит 25 — Z.</summary>
    public static int DriveMask(IEnumerable<string> drives) =>
        drives
            .Where(drive => drive.Length == 1 && char.IsAsciiLetter(drive[0]))
            .Select(drive => char.ToUpperInvariant(drive[0]) - 'A')
            .Distinct()
            .Aggregate(0, (mask, bit) => mask | (1 << bit));
}
