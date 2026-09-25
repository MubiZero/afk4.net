namespace AFK4.Shared.Contracts.Install;

public static class DeviceRoleNames
{
    public const string GamingPc = "gaming_pc";

    public const string ManagerWorkstation = "manager_workstation";

    /// <summary>
    /// Консоль на месте — без агента: сессию ведёт администратор, ПК не отпирается и не запирается,
    /// сердцебиения нет. Запись устройства нужна, чтобы у сессии, кассы и отчётов было то же место,
    /// что у ПК.
    /// </summary>
    public const string Console = "console";

    /// <summary>Есть ли на устройстве агент: у консоли нет — ни команд, ни сердцебиения, ни «офлайн».</summary>
    public static bool HasAgent(string role) => role != Console;

    /// <summary>За устройством играют: ПК или консоль. Рабочее место управляющего — нет.</summary>
    public static bool IsPlayable(string role) => role is GamingPc or Console;
}
