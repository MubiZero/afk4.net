using System.Security.Cryptography;

namespace AFK4.SetupWizard.Core.Kiosk;

/// <summary>
/// Пароль учётки игрока (спека оболочки, §6.1). Его не знает никто: Windows достаёт его из секрета
/// LSA при автовходе, а человеку он не нужен ни разу. 32 символа из четырёх классов проходят любую
/// политику сложности, которую клуб мог включить на ПК.
/// </summary>
public static class KioskPassword
{
    public const int Length = 32;

    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!#$%&*+-=?@^_";

    public static string Generate()
    {
        var all = Upper + Lower + Digits + Symbols;
        var characters = new char[Length];
        // По одному из каждого класса — политика сложности Windows требует три из четырёх, берём все.
        characters[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        characters[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        characters[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        characters[3] = Symbols[RandomNumberGenerator.GetInt32(Symbols.Length)];
        for (var index = 4; index < Length; index++)
        {
            characters[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(characters.AsSpan());
        return new string(characters);
    }
}
