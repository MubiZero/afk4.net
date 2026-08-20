namespace AFK4.Shared.Contracts.Branches;

/// <summary>
/// Как филиал принимает брони из приложения. Решение клуба, а не платформы: платформенный
/// рубильник онлайн-броней — это право организации на функцию целиком, а здесь филиал говорит,
/// что он делает с заявками, которые до него дошли.
/// </summary>
public static class BranchBookingAcceptanceModes
{
    /// <summary>Клуб подтверждает брони сам, без администратора.</summary>
    public const string Auto = "auto";

    /// <summary>Каждую заявку смотрит администратор.</summary>
    public const string Manual = "manual";

    /// <summary>Брони из приложения филиал не принимает вовсе.</summary>
    public const string Off = "off";

    public static readonly IReadOnlyList<string> All = [Auto, Manual, Off];

    public static bool IsSupported(string? value) =>
        value is not null && All.Contains(value, StringComparer.Ordinal);
}
