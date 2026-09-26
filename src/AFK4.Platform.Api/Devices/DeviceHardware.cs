using System.Globalization;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Сверка железа ПК с принятым (P9). Чистые функции: снимки приходят снаружи, чтобы правило
/// «что считать изменением» проверялось без базы.
/// </summary>
public static class DeviceHardware
{
    /// <summary>
    /// Чем принятое отличается от нынешнего. Windows и BIOS не сравниваются: их обновляют каждый
    /// месяц, и каждое обновление поднимало бы тревогу «железо изменилось».
    /// </summary>
    public static IReadOnlyList<HardwareChangeDto> Diff(HardwareSnapshotDto? accepted, HardwareSnapshotDto? current)
    {
        if (accepted is null || current is null)
        {
            return [];
        }

        var changes = new List<HardwareChangeDto>();
        Compare(changes, HardwareComponentNames.Cpu, Clean(accepted.Cpu), Clean(current.Cpu));
        Compare(changes, HardwareComponentNames.Memory, Gigabytes(accepted.MemoryGb), Gigabytes(current.MemoryGb));
        Compare(changes, HardwareComponentNames.Gpu, Gpus(accepted.Gpus), Gpus(current.Gpus));
        Compare(changes, HardwareComponentNames.Motherboard, Clean(accepted.Motherboard), Clean(current.Motherboard));
        Compare(changes, HardwareComponentNames.Disk, Disks(accepted.Disks), Disks(current.Disks));
        return changes;
    }

    /// <summary>Отпечаток того, что сравнивается: одинаковый — одно и то же железо.</summary>
    public static string Fingerprint(HardwareSnapshotDto snapshot) => string.Join(
        '|',
        Clean(snapshot.Cpu),
        Gigabytes(snapshot.MemoryGb),
        Gpus(snapshot.Gpus),
        Clean(snapshot.Motherboard),
        Disks(snapshot.Disks));

    private static void Compare(List<HardwareChangeDto> changes, string component, string? was, string? now)
    {
        if (!string.Equals(was, now, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add(new HardwareChangeDto(component, was, now));
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Gigabytes(int value) => value.ToString(CultureInfo.InvariantCulture) + " GB";

    private static string? Gpus(IReadOnlyList<HardwareGpuDto>? gpus) =>
        gpus is null || gpus.Count == 0
            ? null
            : string.Join(", ", gpus
                .Select(gpu => gpu.MemoryGb is { } memory ? $"{Clean(gpu.Name)} {memory} GB" : Clean(gpu.Name))
                .Order(StringComparer.OrdinalIgnoreCase));

    private static string? Disks(IReadOnlyList<HardwareDiskDto>? disks) =>
        disks is null || disks.Count == 0
            ? null
            : string.Join(", ", disks
                .OrderBy(disk => disk.Name, StringComparer.OrdinalIgnoreCase)
                .Select(disk => $"{disk.Name} {disk.SizeGb} GB"));
}
