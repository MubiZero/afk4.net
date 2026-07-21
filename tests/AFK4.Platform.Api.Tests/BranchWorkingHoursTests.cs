using AFK4.Platform.Api.Branches;
using AFK4.Shared.Contracts.Branches;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class BranchWorkingHoursTests
{
    [Fact]
    public void Default_ReturnsSevenDays_MondayToSunday_AllOpen()
    {
        var days = BranchWorkingHours.Default();
        Assert.Equal(7, days.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, days.Select(d => d.DayOfWeek).ToArray());
        Assert.All(days, d => Assert.False(d.IsClosed));
        Assert.All(days, d => Assert.Equal("10:00", d.OpenTime));
        Assert.All(days, d => Assert.Equal("22:00", d.CloseTime));
    }

    [Fact]
    public void Deserialize_Null_ReturnsDefault()
    {
        var days = BranchWorkingHours.Deserialize(null);
        Assert.Equal(7, days.Count);
    }

    [Fact]
    public void SerializeThenDeserialize_RoundTrips()
    {
        var input = BranchWorkingHours.Default()
            .Select(d => d.DayOfWeek == 7 ? d with { IsClosed = true } : d)
            .ToList();
        var json = BranchWorkingHours.Serialize(input);
        var back = BranchWorkingHours.Deserialize(json);
        Assert.True(back.Single(d => d.DayOfWeek == 7).IsClosed);
        Assert.False(back.Single(d => d.DayOfWeek == 1).IsClosed);
    }

    [Fact]
    public void Validate_ValidWeek_ReturnsNull()
    {
        Assert.Null(BranchWorkingHours.Validate(BranchWorkingHours.Default()));
    }

    [Fact]
    public void Validate_WrongDayCount_ReturnsError()
    {
        var days = BranchWorkingHours.Default().Take(6).ToList();
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_DuplicateDay_ReturnsError()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[6] = days[6] with { DayOfWeek = 1 };
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_OpenNotBeforeClose_ReturnsError()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[0] = days[0] with { OpenTime = "22:00", CloseTime = "10:00" };
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_BadTimeFormat_ReturnsError()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[0] = days[0] with { OpenTime = "9am", CloseTime = "22:00" };
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_ClosedDay_IgnoresTimes()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[0] = days[0] with { IsClosed = true, OpenTime = null, CloseTime = null };
        Assert.Null(BranchWorkingHours.Validate(days));
    }
}
