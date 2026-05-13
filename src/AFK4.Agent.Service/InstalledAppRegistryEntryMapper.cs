using System.Globalization;

namespace AFK4.Agent.Service;

public static class InstalledAppRegistryEntryMapper
{
    public static IReadOnlyCollection<InstalledAppSnapshot> Map(IEnumerable<InstalledAppRegistryEntry> entries)
    {
        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DisplayName))
            .Select(entry => new InstalledAppSnapshot(
                DisplayName: entry.DisplayName!.Trim(),
                Version: Normalize(entry.DisplayVersion),
                Publisher: Normalize(entry.Publisher),
                InstallLocation: Normalize(entry.InstallLocation),
                InstalledAtUtc: ParseInstalledDate(entry.InstallDate)))
            .ToArray();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static DateTimeOffset? ParseInstalledDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParseExact(
            value.Trim(),
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? new DateTimeOffset(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0, TimeSpan.Zero)
            : null;
    }
}
