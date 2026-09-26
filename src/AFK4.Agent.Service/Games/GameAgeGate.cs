namespace AFK4.Agent.Service.Games;

/// <summary>
/// Возраст у игры (спека оболочки, §6.6). Проверяется только известный возраст: день рождения
/// игрок вводит по желанию, и без него игра не запирается — иначе запретили бы всем, кто не ввёл.
/// </summary>
public static class GameAgeGate
{
    public static bool IsLocked(int? minAge, int? playerAge) =>
        minAge is > 0 && playerAge is { } age && age < minAge;
}
