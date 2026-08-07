using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SubscriptionMovementTests
{
    private static readonly Guid Club = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherClub = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly First = new(2026, 1, 1);
    private static readonly DateOnly Last = new(2026, 4, 1);

    private static SnapshotRow Row(Guid club, int month, int day, string status) =>
        new(club, new DateOnly(2026, month, day), status);

    [Fact]
    public void ClubBecomingActive_CountsAsJoinedInThatMonth()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Trial),
            Row(Club, 2, 28, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).Joined);
        Assert.Equal(0, points.Single(point => point.Month == 2).Left);
    }

    [Fact]
    public void ClubLeavingActive_CountsAsLeftInThatMonth()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.Cancelled)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).Left);
        Assert.Equal(0, points.Single(point => point.Month == 2).Joined);
    }

    [Fact]
    public void PastDue_IsStillPaying_NotChurn()
    {
        // Клуб, которому шлют напоминания, ещё не ушёл: отток — это cancelled, а не долг.
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.PastDue)
        ], First, Last);

        Assert.Equal(0, points.Single(point => point.Month == 2).Left);
        Assert.Equal(1, points.Single(point => point.Month == 2).PayingAtMonthEnd);
    }

    [Fact]
    public void ClubAppearingAlreadyActive_CountsAsJoined()
    {
        // Организации не было в снимках вовсе — значит она новая, а не «всегда была».
        var points = SubscriptionMovement.Compute([Row(Club, 3, 31, SubscriptionStatusNames.Active)], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 3).Joined);
    }

    [Fact]
    public void ReturningClub_CountsAsJoinedAgain()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.Cancelled),
            Row(Club, 3, 31, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).Left);
        Assert.Equal(1, points.Single(point => point.Month == 3).Joined);
    }

    [Fact]
    public void PayingCount_IsTakenFromTheLastSnapshotOfTheMonth()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 2, 1, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.Cancelled),
            Row(OtherClub, 2, 28, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).PayingAtMonthEnd);
    }

    [Fact]
    public void ClubAlreadyPayingBeforeTheWindow_IsNotCountedAsJoinedInTheFirstMonth()
    {
        // Буферный снимок за месяц ДО окна — единственный источник «платил ли клуб раньше».
        // Без него первый месяц окна ошибочно посчитает уже платящий клуб «пришедшим».
        var points = SubscriptionMovement.Compute(
        [
            new SnapshotRow(Club, new DateOnly(2025, 12, 31), SubscriptionStatusNames.Active), // буфер перед First
            Row(Club, 1, 31, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(0, points.Single(point => point.Month == 1).Joined);
    }

    [Fact]
    public void EveryMonthOfTheWindow_IsPresent_EvenWithoutSnapshots()
    {
        var points = SubscriptionMovement.Compute([], First, Last);

        Assert.Equal(4, points.Count);
        Assert.All(points, point => Assert.Equal(0, point.PayingAtMonthEnd));
    }
}
