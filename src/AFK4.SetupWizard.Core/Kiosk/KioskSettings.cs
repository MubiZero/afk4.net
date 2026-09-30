namespace AFK4.SetupWizard.Core.Kiosk;

/// <summary>Значение реестра: строка (REG_SZ) или число (REG_DWORD). Оба пусты — значения нет.</summary>
public sealed record KioskRegistryValue(string? Text, int? Number)
{
    public static KioskRegistryValue Of(string text) => new(text, null);

    public static KioskRegistryValue Of(int number) => new(null, number);

    public bool IsAbsent => Text is null && Number is null;
}

/// <summary>Параметр реестра ПК (HKLM), который киоск меняет и откат возвращает.</summary>
public sealed record KioskMachineSetting(string Key, string Name, KioskRegistryValue Value);

/// <summary>
/// Что киоск меняет в Windows (спека оболочки, §6.1). Одно место на установку и откат: откат
/// возвращает ровно эти параметры к тем значениям, что были до киоска.
/// </summary>
public static class KioskSettings
{
    public const string UserName = "AFK4 Player";

    public const string WinlogonKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

    /// <summary>Секрет LSA с паролем автовхода — Winlogon смотрит в него раньше, чем в реестр.</summary>
    public const string AutologonSecret = "DefaultPassword";

    /// <summary>
    /// Где лежит пароль прежнего автовхода, пока стоит киоск. Тоже секрет LSA, а не файл: в
    /// kiosk-state.json пароль открытым текстом был бы той же дырой, от которой спасает LSA.
    /// </summary>
    public const string SavedAutologonSecret = "AFK4.SavedAutologonPassword";

    /// <summary>Прежний пароль автовхода открытым текстом в Winlogon — киоск его удаляет.</summary>
    public const string PlainAutologonPassword = "DefaultPassword";

    public const string AutologonEnabled = "AutoAdminLogon";

    /// <summary>Ключ Winlogon в кусте пользователя: здесь живёт его личная оболочка.</summary>
    public const string UserWinlogonKey = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";

    public static IReadOnlyList<KioskMachineSetting> Machine { get; } =
    [
        // Автовход в учётку игрока. Пароль — секретом LSA, а не строкой в реестре.
        new(WinlogonKey, AutologonEnabled, KioskRegistryValue.Of("1")),
        new(WinlogonKey, "DefaultUserName", KioskRegistryValue.Of(UserName)),
        new(WinlogonKey, "DefaultDomainName", KioskRegistryValue.Of(".")),
        // Windows 11 прячет вход по паролю за Windows Hello — автовход тогда молча не срабатывает.
        new(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device", "DevicePasswordLessBuildVersion", KioskRegistryValue.Of(0)),
        // Экран блокировки между включением ПК и оболочкой — лишний шаг, который гость не пройдёт.
        new(@"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen", KioskRegistryValue.Of(1)),
        // «Привет, мы всё настраиваем» при первом входе — минута чёрного экрана на новом ПК.
        new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableFirstLogonAnimation", KioskRegistryValue.Of(0))
    ];

    /// <summary>
    /// Что киоск удаляет и откат не возвращает: пароль открытым текстом (он и был дырой) и
    /// счётчик входов, после которого автовход выключился бы сам.
    /// </summary>
    public static IReadOnlyList<(string Key, string Name)> MachineRemoved { get; } =
    [
        (WinlogonKey, PlainAutologonPassword),
        (WinlogonKey, "AutoLogonCount")
    ];

    /// <summary>
    /// Всё, что откат возвращает из реестра: параметры, которые киоск пишет, и счётчик входов,
    /// который он удаляет. Пароль сюда не входит — он уезжает в LSA, а не в файл состояния.
    /// </summary>
    public static IReadOnlyList<(string Key, string Name)> Tracked { get; } =
        Machine.Select(setting => (setting.Key, setting.Name))
            .Concat(MachineRemoved.Where(removed => removed.Name != PlainAutologonPassword))
            .ToList();

    /// <summary>Строка Shell для учётки игрока: путь в кавычках — в нём пробелы.</summary>
    public static string ShellCommand(string hostExecutablePath) => $"\"{hostExecutablePath}\"";
}
