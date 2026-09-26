using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Install;

/// <summary>
/// Код установки в руках техника: его диктуют по телефону, вбивают в команду развёртывания и
/// вставляют в скрипт. Поэтому алфавит Крокфорда — без I, L, O и U, которые путают с 1, 0 и V, —
/// и четыре группы по четыре знака. Шестнадцать знаков — 80 бит: перебрать их нельзя, сколько бы
/// запросов в минуту ни пропускала дверь.
/// </summary>
internal static class InstallCodes
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 16;
    private const int GroupLength = 4;

    public static string Create()
    {
        var symbols = RandomNumberGenerator.GetItems<char>(Alphabet, Length);
        return string.Join('-', Enumerable.Range(0, Length / GroupLength)
            .Select(group => new string(symbols, group * GroupLength, GroupLength)));
    }

    /// <summary>
    /// Код в том виде, в каком его хешируют: без дефисов и пробелов, заглавными; O, I и L
    /// читаются как 0 и 1 — так их и вбивают, переписывая с экрана. Null — это не код.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var builder = new StringBuilder(Length);
        foreach (var symbol in input)
        {
            if (symbol is '-' or ' ')
            {
                continue;
            }

            var upper = char.ToUpperInvariant(symbol) switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                var other => other
            };
            if (!Alphabet.Contains(upper) || builder.Length == Length)
            {
                return null;
            }

            builder.Append(upper);
        }

        return builder.Length == Length ? builder.ToString() : null;
    }

    public static string Hash(string normalizedCode) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(normalizedCode)));
}
