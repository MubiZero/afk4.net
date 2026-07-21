using System.Globalization;
using System.Text.Json;
using AFK4.Shared.Contracts.Branches;

namespace AFK4.Platform.Api.Branches;

/// <summary>Сериализация/валидация расписания клуба (7 дней). Хранится одной JSON-колонкой
/// branches.WorkingHoursJson; читается всегда нормализованно к дням 1..7.</summary>
public static class BranchWorkingHours
{
    private const string DefaultOpen = "10:00";
    private const string DefaultClose = "22:00";

    public static IReadOnlyList<BranchWorkingHoursDayDto> Default() =>
        Enumerable.Range(1, 7)
            .Select(day => new BranchWorkingHoursDayDto(day, false, DefaultOpen, DefaultClose))
            .ToList();

    public static string Serialize(IReadOnlyList<BranchWorkingHoursDayDto> days) =>
        JsonSerializer.Serialize(days);

    public static IReadOnlyList<BranchWorkingHoursDayDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default();
        }

        List<BranchWorkingHoursDayDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<BranchWorkingHoursDayDto>>(json);
        }
        catch (JsonException)
        {
            return Default();
        }

        if (parsed is null || parsed.Count == 0)
        {
            return Default();
        }

        // Нормализуем к 7 дням 1..7: берём известные дни, недостающие добираем дефолтом.
        var byDay = parsed
            .Where(d => d.DayOfWeek is >= 1 and <= 7)
            .GroupBy(d => d.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.First());

        return Enumerable.Range(1, 7)
            .Select(day => byDay.TryGetValue(day, out var found)
                ? found
                : new BranchWorkingHoursDayDto(day, false, DefaultOpen, DefaultClose))
            .ToList();
    }

    public static string? Validate(IReadOnlyList<BranchWorkingHoursDayDto> days)
    {
        if (days.Count != 7)
        {
            return "Working hours must contain exactly 7 days.";
        }

        if (days.Select(d => d.DayOfWeek).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 7)) == false)
        {
            return "Working hours must cover days 1..7 exactly once.";
        }

        foreach (var day in days)
        {
            if (day.IsClosed)
            {
                continue;
            }

            if (!TryParseTime(day.OpenTime, out var open) || !TryParseTime(day.CloseTime, out var close))
            {
                return "Open/close time must be in HH:mm format for non-closed days.";
            }

            if (open >= close)
            {
                return "Open time must be earlier than close time.";
            }
        }

        return null;
    }

    private static bool TryParseTime(string? value, out TimeOnly time)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        time = default;
        return false;
    }
}
