#if NETFRAMEWORK
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace System;

// DateOnly и TimeOnly появились в .NET 6; программам на ПК под .NET Framework 4.8 (docs/roadmap/netfx48.md)
// нужны те же контракты. Здесь — ровно то, чем пользуются клиенты, и тот же JSON, что у .NET:
// «yyyy-MM-dd» и «HH:mm:ss[.fffffff]». Публичные: тип поля контракта должен быть одним на все сборки ПК.

[JsonConverter(typeof(DateOnlyJsonConverter))]
public readonly record struct DateOnly(int Year, int Month, int Day) : IComparable<DateOnly>
{
    public static DateOnly FromDateTime(DateTime dateTime) => new(dateTime.Year, dateTime.Month, dateTime.Day);

    public DateTime ToDateTime(TimeOnly time) => new DateTime(Year, Month, Day).Add(time.ToTimeSpan());

    public static DateOnly Parse(string text) =>
        FromDateTime(DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None));

    public int CompareTo(DateOnly other) => (Year, Month, Day).CompareTo((other.Year, other.Month, other.Day));

    public static bool operator <(DateOnly left, DateOnly right) => left.CompareTo(right) < 0;
    public static bool operator >(DateOnly left, DateOnly right) => left.CompareTo(right) > 0;
    public static bool operator <=(DateOnly left, DateOnly right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DateOnly left, DateOnly right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0:D4}-{1:D2}-{2:D2}", Year, Month, Day);
}

[JsonConverter(typeof(TimeOnlyJsonConverter))]
public readonly record struct TimeOnly : IComparable<TimeOnly>
{
    private readonly long ticks;

    public TimeOnly(int hour, int minute, int second = 0) : this(new TimeSpan(hour, minute, second).Ticks)
    {
    }

    private TimeOnly(long ticks)
    {
        if (ticks < 0 || ticks >= TimeSpan.TicksPerDay)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), "Time of day must be within one day.");
        }

        this.ticks = ticks;
    }

    public int Hour => ToTimeSpan().Hours;

    public int Minute => ToTimeSpan().Minutes;

    public static TimeOnly FromDateTime(DateTime dateTime) => new(dateTime.TimeOfDay.Ticks);

    public static TimeOnly FromTimeSpan(TimeSpan timeOfDay) => new(timeOfDay.Ticks);

    public TimeSpan ToTimeSpan() => new(ticks);

    public static TimeOnly Parse(string text) =>
        new(TimeSpan.ParseExact(text, ["hh\\:mm\\:ss\\.FFFFFFF", "hh\\:mm\\:ss", "hh\\:mm"], CultureInfo.InvariantCulture).Ticks);

    public int CompareTo(TimeOnly other) => ticks.CompareTo(other.ticks);

    public static bool operator <(TimeOnly left, TimeOnly right) => left.ticks < right.ticks;
    public static bool operator >(TimeOnly left, TimeOnly right) => left.ticks > right.ticks;
    public static bool operator <=(TimeOnly left, TimeOnly right) => left.ticks <= right.ticks;
    public static bool operator >=(TimeOnly left, TimeOnly right) => left.ticks >= right.ticks;

    public override string ToString() =>
        ToTimeSpan().ToString(ticks % TimeSpan.TicksPerSecond == 0 ? "hh\\:mm\\:ss" : "hh\\:mm\\:ss\\.FFFFFFF", CultureInfo.InvariantCulture);
}

internal sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateOnly.Parse(reader.GetString() ?? throw new JsonException("A date was expected."));

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

internal sealed class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeOnly.Parse(reader.GetString() ?? throw new JsonException("A time of day was expected."));

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
#endif
