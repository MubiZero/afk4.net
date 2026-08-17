using AFK4.Platform.Api.Billing;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Расписание тарифа: когда дешёвое утро дешёвое.
///
/// Часовой пояс здесь задан смещением, а не именем: проверяется логика окна, и подсовывать ей
/// зависимость от базы часовых поясов машины значит однажды получить разный ответ на Linux и на
/// Windows там, где ответ обязан быть один.
/// </summary>
public class TariffScheduleTests
{
    private static readonly TimeZoneInfo Plus5 = TimeZoneInfo.CreateCustomTimeZone(
        "test-plus-5", TimeSpan.FromHours(5), "test-plus-5", "test-plus-5");

    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private const int Monday = 1 << 0;
    private const int Friday = 1 << 4;
    private const int Saturday = 1 << 5;
    private const int Sunday = 1 << 6;

    // 2026-08-17 — понедельник.
    private static DateTimeOffset Utc0(int day, int hour, int minute = 0) =>
        new(2026, 8, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void ATariffWithoutASchedule_AppliesAtAnyHourOfAnyDay()
    {
        foreach (var day in new[] { 17, 18, 22, 23 })
        {
            foreach (var hour in new[] { 0, 7, 13, 23 })
            {
                Assert.True(TariffSchedule.AppliesAt(
                    TariffSchedule.EveryDayMask, null, null, Utc0(day, hour), Utc));
            }
        }
    }

    [Fact]
    public void AMorningTariff_AppliesInTheMorningAndNotInTheEvening()
    {
        Assert.True(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, Utc0(17, 10), Utc));
        Assert.False(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, Utc0(17, 20), Utc));
    }

    /// <summary>
    /// Границы — полуинтервал: начало входит, конец нет. Иначе в 16:00 действуют сразу два тарифа,
    /// и какой из них продадут, зависит от порядка строк в списке.
    /// </summary>
    [Fact]
    public void TheWindowIncludesItsStartAndExcludesItsEnd()
    {
        Assert.True(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, Utc0(17, 8), Utc));
        Assert.False(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, Utc0(17, 16), Utc));
        Assert.True(TariffSchedule.AppliesAt(
            TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, Utc0(17, 15, 59), Utc));
    }

    // Ночной тариф с 22:00 до 06:00 — половина его часов приходится на следующие сутки, и без
    // перехода через полночь его просто не выразить.
    [Fact]
    public void ANightTariff_CarriesOverPastMidnight()
    {
        Assert.True(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 22 * 60, 6 * 60, Utc0(17, 23), Utc));
        Assert.True(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 22 * 60, 6 * 60, Utc0(18, 3), Utc));
        Assert.False(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 22 * 60, 6 * 60, Utc0(18, 12), Utc));
    }

    /// <summary>
    /// Ночь принадлежит тому дню, в который началась. Владелец, отметивший «ночь с пятницы»,
    /// имеет в виду и три часа утра субботы, а субботу отдельно не отмечал — иначе он получил бы
    /// заодно ночь с субботы на воскресенье, которую не заказывал.
    /// </summary>
    [Fact]
    public void ANightThatStartsOnAMarkedDay_BelongsToThatDay()
    {
        // 21 августа 2026 — пятница, 22-е — суббота.
        Assert.True(TariffSchedule.AppliesAt(Friday, 22 * 60, 6 * 60, Utc0(21, 23), Utc));
        Assert.True(TariffSchedule.AppliesAt(Friday, 22 * 60, 6 * 60, Utc0(22, 3), Utc));
        // Ночь с субботы на воскресенье пятницей не отмечена.
        Assert.False(TariffSchedule.AppliesAt(Friday, 22 * 60, 6 * 60, Utc0(22, 23), Utc));
    }

    [Fact]
    public void AWeekendTariff_SkipsWeekdays()
    {
        Assert.False(TariffSchedule.AppliesAt(Saturday | Sunday, null, null, Utc0(17, 12), Utc));
        Assert.True(TariffSchedule.AppliesAt(Saturday | Sunday, null, null, Utc0(22, 12), Utc));
        Assert.True(TariffSchedule.AppliesAt(Saturday | Sunday, null, null, Utc0(23, 12), Utc));
    }

    [Fact]
    public void TheWindowIsReadInTheBranchTimeZoneAndNotInUtc()
    {
        // 05:00 UTC — это 10:00 в клубе на пять часов восточнее: утренний тариф действует там и
        // не действует у того, кто живёт по UTC.
        var instant = Utc0(17, 5);
        Assert.True(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, instant, Plus5));
        Assert.False(TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60, instant, Utc));
    }

    // Часовой пояс двигает и день недели, а не только час: за пять часов до полуночи по UTC в
    // клубе уже следующие сутки.
    [Fact]
    public void TheTimeZoneCanMoveTheDayOfWeekTooAndTheMaskFollowsIt()
    {
        var lateSundayUtc = Utc0(23, 20);
        Assert.True(TariffSchedule.AppliesAt(Monday, null, null, lateSundayUtc, Plus5));
        Assert.False(TariffSchedule.AppliesAt(Monday, null, null, lateSundayUtc, Utc));
    }

    [Fact]
    public void AllSevenDaysMarkedMeansTheSameAsNoDaysMarked()
    {
        foreach (var day in new[] { 17, 18, 22, 23 })
        {
            Assert.Equal(
                TariffSchedule.AppliesAt(TariffSchedule.EveryDayMask, null, null, Utc0(day, 12), Utc),
                TariffSchedule.AppliesAt(TariffSchedule.AllDaysMask, null, null, Utc0(day, 12), Utc));
        }
    }

    [Fact]
    public void AWholeSchedulelessTariffIsNotShownAsRestricted()
    {
        Assert.False(TariffSchedule.IsRestricted(TariffSchedule.EveryDayMask, null, null));
        Assert.False(TariffSchedule.IsRestricted(TariffSchedule.AllDaysMask, null, null));
        Assert.True(TariffSchedule.IsRestricted(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60));
        Assert.True(TariffSchedule.IsRestricted(Saturday | Sunday, null, null));
    }

    // Половина окна — это недописанная настройка, а не «с восьми и до упора»: догадываться за
    // владельца о цене его же часов нельзя.
    [Fact]
    public void HalfAWindowIsRefused()
    {
        Assert.Equal(
            TariffSchedule.InvalidScheduleCode,
            TariffSchedule.Validate(TariffSchedule.EveryDayMask, 8 * 60, null));
        Assert.Equal(
            TariffSchedule.InvalidScheduleCode,
            TariffSchedule.Validate(TariffSchedule.EveryDayMask, null, 16 * 60));
    }

    // Совпадающие границы читаются и как «круглые сутки», и как «нисколько» — обе догадки
    // ошибаются в деньгах.
    [Fact]
    public void AZeroLengthWindowIsRefused()
    {
        Assert.Equal(
            TariffSchedule.InvalidScheduleCode,
            TariffSchedule.Validate(TariffSchedule.EveryDayMask, 10 * 60, 10 * 60));
    }

    [Fact]
    public void MinutesOutsideADayAndAnImpossibleDayMaskAreRefused()
    {
        Assert.Equal(
            TariffSchedule.InvalidScheduleCode,
            TariffSchedule.Validate(TariffSchedule.EveryDayMask, -1, 60));
        Assert.Equal(
            TariffSchedule.InvalidScheduleCode,
            TariffSchedule.Validate(TariffSchedule.EveryDayMask, 0, 24 * 60));
        Assert.Equal(TariffSchedule.InvalidScheduleCode, TariffSchedule.Validate(128, null, null));
        Assert.Equal(TariffSchedule.InvalidScheduleCode, TariffSchedule.Validate(-1, null, null));
    }

    [Fact]
    public void AnOrdinaryScheduleIsAccepted()
    {
        Assert.Null(TariffSchedule.Validate(TariffSchedule.EveryDayMask, null, null));
        Assert.Null(TariffSchedule.Validate(TariffSchedule.EveryDayMask, 8 * 60, 16 * 60));
        Assert.Null(TariffSchedule.Validate(Saturday | Sunday, 22 * 60, 6 * 60));
    }
}
