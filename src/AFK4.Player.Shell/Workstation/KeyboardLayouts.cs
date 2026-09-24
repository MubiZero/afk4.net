using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Workstation;

/// <summary>
/// Раскладки, которые оболочка предлагает игроку: русская, английская, таджикская — языки клубов.
/// Имя раскладки — по языку из её идентификатора, а не по стране: британская английская тоже «EN».
/// </summary>
public static class KeyboardLayouts
{
    private const ushort Russian = 0x0419;
    private const ushort Tajik = 0x0428;

    /// <summary>Основной язык: младшие 10 бит идентификатора языка.</summary>
    private const ushort PrimaryLanguageMask = 0x03FF;
    private const ushort EnglishPrimary = 0x09;

    public static string? LabelFor(ushort languageId) =>
        languageId == Russian ? ShellKeyboardLayoutNames.Russian
        : languageId == Tajik ? ShellKeyboardLayoutNames.Tajik
        : (languageId & PrimaryLanguageMask) == EnglishPrimary ? ShellKeyboardLayoutNames.English
        : null;

    /// <summary>Идентификатор раскладки для LoadKeyboardLayout; null — такую оболочка не предлагает.</summary>
    public static string? KlidFor(string? label) => label switch
    {
        ShellKeyboardLayoutNames.Russian => "00000419",
        ShellKeyboardLayoutNames.English => "00000409",
        ShellKeyboardLayoutNames.Tajik => "00000428",
        _ => null
    };
}
