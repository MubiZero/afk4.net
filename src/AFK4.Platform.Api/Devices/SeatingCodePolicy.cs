namespace AFK4.Platform.Api.Devices;

/// <summary>Сколько живёт код посадки и как он выглядит.</summary>
public static class SeatingCodePolicy
{
    /// <summary>
    /// Две минуты. Достаточно, чтобы перевести взгляд с монитора на телефон и набрать; мало,
    /// чтобы сфотографированный код пригодился кому-то из дома.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>Шесть цифр — столько человек удерживает в голове между экраном и телефоном.</summary>
    public const int Digits = 6;

    /// <summary>Как человек набрал, так и принимаем: пробелы и дефисы к делу не относятся.</summary>
    public static string Normalize(string? typed) =>
        new((typed ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
