using AFK4.Agent.Service.Games;

namespace AFK4.Agent.Service.Tests.Games;

/// <summary>
/// Возраст у игры (спека оболочки, §6.6): запирается только при известном возрасте — день рождения
/// игрок вводит по желанию, и без него игра открыта.
/// </summary>
public sealed class GameAgeGateTests
{
    [Theory]
    [InlineData(18, 17, true)]
    [InlineData(18, 18, false)]
    [InlineData(16, 30, false)]
    [InlineData(18, null, false)]
    [InlineData(null, 10, false)]
    [InlineData(0, 10, false)]
    public void OnlyAKnownYoungerAge_LocksTheGame(int? minAge, int? playerAge, bool locked)
    {
        Assert.Equal(locked, GameAgeGate.IsLocked(minAge, playerAge));
    }
}
