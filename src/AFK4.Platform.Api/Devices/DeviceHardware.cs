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

        // Накопители и мониторы сравниваются, только когда известны с обеих сторон: null — старый агент
        // или не прочиталось, и считать это «всё вынули» — ложная тревога. Пустой список — известно,
        // что ничего нет: унесённый монитор клубу важен, как вынутая видеокарта. Флешка сюда не
        // попадает — агент отсеивает съёмные накопители сам.
        if (accepted.PhysicalDisks is not null && current.PhysicalDisks is not null)
        {
            Compare(changes, HardwareComponentNames.PhysicalDisk, PhysicalDisks(accepted.PhysicalDisks), PhysicalDisks(current.PhysicalDisks));
        }

        if (accepted.Monitors is not null && current.Monitors is not null)
        {
            Compare(changes, HardwareComponentNames.Monitor, Monitors(accepted.Monitors), Monitors(current.Monitors));
        }

        return changes;
    }

    /// <summary>
    /// Отпечаток того, что сравнивается: одинаковый — одно и то же железо. Накопители и мониторы входят,
    /// только когда известны, — снимок старого агента даёт тот же отпечаток, что и до них.
    /// </summary>
    public static string Fingerprint(HardwareSnapshotDto snapshot)
    {
        var parts = new List<string?>
        {
            Clean(snapshot.Cpu),
            Gigabytes(snapshot.MemoryGb),
            Gpus(snapshot.Gpus),
            Clean(snapshot.Motherboard),
            Disks(snapshot.Disks)
        };
        if (snapshot.PhysicalDisks is not null)
        {
            parts.Add("drives:" + PhysicalDisks(snapshot.PhysicalDisks));
        }

        if (snapshot.Monitors is not null)
        {
            parts.Add("monitors:" + Monitors(snapshot.Monitors));
        }

        return string.Join('|', parts);
    }

    /// <summary>
    /// Снимок, где неизвестные накопители и мониторы (null) взяты из known. Так сходятся отпечаток и
    /// сверка: принятый снимок без них получает первую опись как норму (как первый снимок целиком),
    /// а нынешний, где они не прочитались, считается неизменным.
    /// </summary>
    public static HardwareSnapshotDto WithKnownParts(HardwareSnapshotDto snapshot, HardwareSnapshotDto? known) =>
        known is null
            ? snapshot
            : snapshot with
            {
                PhysicalDisks = snapshot.PhysicalDisks ?? known.PhysicalDisks,
                Monitors = snapshot.Monitors ?? known.Monitors
            };

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

    // По модели и размеру: шину не сравниваем — её название зависит от драйвера, а не от диска.
    private static string? PhysicalDisks(IReadOnlyList<HardwarePhysicalDiskDto> disks) =>
        disks.Count == 0
            ? null
            : string.Join(", ", disks
                .Select(disk => $"{Clean(disk.Model)} {disk.SizeGb} GB")
                .Order(StringComparer.OrdinalIgnoreCase));

    // По модели и серийнику: такой же монитор с другим серийником — подмена.
    private static string? Monitors(IReadOnlyList<HardwareMonitorDto> monitors) =>
        monitors.Count == 0
            ? null
            : string.Join(", ", monitors
                .Select(monitor => Clean(monitor.Serial) is { } serial ? $"{Clean(monitor.Name)} ({serial})" : Clean(monitor.Name))
                .Order(StringComparer.OrdinalIgnoreCase));
}
