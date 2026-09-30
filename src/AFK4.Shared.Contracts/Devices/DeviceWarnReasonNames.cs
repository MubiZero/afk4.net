namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Причины предупреждения игроку (<see cref="DeviceCommandTypeNames.Warn"/>, поле reason в теле).
/// Агент показывает только эти: придумывать за сервер, чем пугать игрока, он не вправе. Поэтому и
/// сервер другой причины не принимает — раньше она доезжала до ПК, агент отвечал отказом, а Панель
/// считала сообщение ушедшим.
/// </summary>
public static class DeviceWarnReasonNames
{
    public const string TimeAlmostUp = "time-almost-up";

    public const string CreditLimit = "credit-limit";

    public const string LowBalance = "low-balance";

    public static bool IsKnown(string? reason) =>
        reason is TimeAlmostUp or CreditLimit or LowBalance;
}
