namespace AFK4.SetupWizard.Core.SilentInstall;

/// <summary>
/// Тихая установка по коду: <c>AFK4.SetupWizard.exe --install-code КОД [--seat МЕСТО]</c>. Так
/// мастер запускает установщик агента, когда ему передали <c>AFK4_INSTALL_CODE</c>.
/// </summary>
public sealed record SilentInstallOptions(string InstallCode, string? SeatName)
{
    public const string CodeArgument = "--install-code";
    public const string SeatArgument = "--seat";

    /// <summary>
    /// Requested — в строке запуска есть код: окна не будет, даже если сам код не разобрался, —
    /// показать окно на ПК, который ставят скриптом, некому.
    /// </summary>
    public static SilentInstallParse Parse(IReadOnlyList<string> arguments)
    {
        string? code = null;
        string? seat = null;
        var requested = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            var (name, inlineValue) = Split(arguments[index]);
            if (name is not (CodeArgument or SeatArgument))
            {
                continue;
            }

            var value = inlineValue ?? (index + 1 < arguments.Count && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal)
                ? arguments[++index]
                : null);
            if (name == CodeArgument)
            {
                requested = true;
                code = value;
            }
            else
            {
                seat = value;
            }
        }

        if (!requested)
        {
            return new SilentInstallParse(false, null, null);
        }

        return string.IsNullOrWhiteSpace(code)
            ? new SilentInstallParse(true, null, $"{CodeArgument} needs the install code from the AFK4.net Panel.")
            : new SilentInstallParse(true, new SilentInstallOptions(code.Trim(), string.IsNullOrWhiteSpace(seat) ? null : seat.Trim()), null);
    }

    private static (string Name, string? InlineValue) Split(string argument)
    {
        var separator = argument.IndexOf('=');
        return separator < 0 ? (argument, null) : (argument[..separator], argument[(separator + 1)..]);
    }
}

public sealed record SilentInstallParse(bool Requested, SilentInstallOptions? Options, string? Error);

/// <summary>
/// Коды выхода тихой установки: по ним скрипт развёртывания решает, что делать с ПК дальше.
/// Подробности — в %ProgramData%\AFK4\logs\setup-wizard.log.
/// </summary>
public static class SilentInstallExitCodes
{
    public const int Installed = 0;

    /// <summary>Нет кода в строке запуска.</summary>
    public const int BadArguments = 1;

    /// <summary>Запущено без прав администратора: без них ни службу, ни киоск не поставить.</summary>
    public const int NotElevated = 2;

    /// <summary>Платформа отказала: код неверен, истёк, исчерпан или кончился тариф. Повтор не поможет.</summary>
    public const int CodeRefused = 3;

    /// <summary>Платформа недоступна и после повторов: сеть, DNS, прокси. Повторить позже.</summary>
    public const int PlatformUnreachable = 4;

    /// <summary>ПК зарегистрирован, но настройка, оболочка или агент не встали.</summary>
    public const int SetupFailed = 5;

    /// <summary>ПК работает, но без киоска: учётка игрока или автовход не настроились.</summary>
    public const int KioskFailed = 6;
}
