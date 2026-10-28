using System.Globalization;

namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Здоровье ПК (docs/operations/pc-health.md): то, что Windows отдаёт без драйвера ядра. Точной температуры
/// процессора и оборотов корпусных вентиляторов среди этого нет — для них нужен драйвер, а его Defender
/// помечает как уязвимый; ставить такое на ПК клубов не будем. Любое поле null — не прочиталось на этом ПК.
/// </summary>
public sealed record DeviceHealthDto(
    int? CpuLoadPercent,
    int? MemoryUsedPercent,
    /// Самая горячая тепловая зона ACPI — датчик платы, у многих ПК близкий к процессору; у многих его нет.
    int? BoardTemperatureC,
    IReadOnlyList<GpuHealthDto> Gpus,
    IReadOnlyList<DiskHealthDto> Disks,
    IReadOnlyList<VolumeSpaceDto> Volumes);

/// <summary>Видеокарта NVIDIA (через NVML драйвера). AMD и встроенная графика пока не читаются.</summary>
public sealed record GpuHealthDto(string Name, int? TemperatureC, int? FanPercent, int? LoadPercent);

/// <param name="Health">Оценка самой Windows (<see cref="DiskHealthNames"/>); null — не знает.</param>
public sealed record DiskHealthDto(
    string Model,
    string? Health,
    int? TemperatureC,
    int? WearPercent,
    int? PowerOnHours,
    long? ReadErrorsUncorrected);

/// <param name="Name">Буква тома: «C:».</param>
public sealed record VolumeSpaceDto(string Name, int FreeGb, int SizeGb, bool IsSystem);

public sealed record DeviceHealthReportRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    DateTimeOffset ObservedAtUtc,
    DeviceHealthDto Health);

public static class DeviceHealthRoutes
{
    public static string Report(Guid deviceId) => string.Create(
        CultureInfo.InvariantCulture, $"/api/devices/{deviceId:D}/health/report");
}

public static class DiskHealthNames
{
    public const string Healthy = "healthy";
    public const string Warning = "warning";
    public const string Unhealthy = "unhealthy";
}

/// <summary>Худшая проблема ПК: код для карты и Панели, уровень — звать ли владельца.</summary>
public static class DeviceHealthIssueNames
{
    public const string DiskFailing = "disk_failing";
    public const string DiskWarning = "disk_warning";
    public const string DiskHot = "disk_hot";
    public const string DiskWorn = "disk_worn";
    public const string GpuOverheating = "gpu_overheating";
    public const string GpuHot = "gpu_hot";
    public const string BoardHot = "board_hot";
    public const string LowDiskSpace = "low_disk_space";

    /// <summary>Критично — сообщить владельцу: ПК вот-вот потеряет данные или выключится от перегрева.</summary>
    public static bool IsCritical(string? issue) => issue is DiskFailing or GpuOverheating;
}

/// <summary>
/// Пороги — по паспортным пределам: NVIDIA снижает частоты около 83–87 °C и выключается ближе к 95; SSD и HDD
/// живут до 60–70 °C; износ SSD Windows считает в процентах ресурса.
/// </summary>
public static class DeviceHealthRules
{
    public const int GpuHotC = 87;
    public const int GpuOverheatingC = 95;
    public const int BoardHotC = 90;
    public const int DiskHotC = 60;
    public const int DiskWornPercent = 90;

    /// <summary>Свободного места на системном томе меньше этого — обновления Windows и игр перестают ставиться.</summary>
    public const int LowSystemSpaceGb = 10;

    /// <summary>Самая серьёзная проблема из показателей; null — всё в порядке.</summary>
    public static string? WorstIssue(DeviceHealthDto health)
    {
        if (health.Disks.Any(disk => disk.Health == DiskHealthNames.Unhealthy || disk.ReadErrorsUncorrected > 0))
        {
            return DeviceHealthIssueNames.DiskFailing;
        }

        if (health.Gpus.Any(gpu => gpu.TemperatureC >= GpuOverheatingC))
        {
            return DeviceHealthIssueNames.GpuOverheating;
        }

        if (health.Disks.Any(disk => disk.Health == DiskHealthNames.Warning))
        {
            return DeviceHealthIssueNames.DiskWarning;
        }

        if (health.Gpus.Any(gpu => gpu.TemperatureC >= GpuHotC))
        {
            return DeviceHealthIssueNames.GpuHot;
        }

        if (health.BoardTemperatureC >= BoardHotC)
        {
            return DeviceHealthIssueNames.BoardHot;
        }

        if (health.Disks.Any(disk => disk.TemperatureC >= DiskHotC))
        {
            return DeviceHealthIssueNames.DiskHot;
        }

        if (health.Disks.Any(disk => disk.WearPercent >= DiskWornPercent))
        {
            return DeviceHealthIssueNames.DiskWorn;
        }

        return health.Volumes.Any(volume => volume.IsSystem && volume.FreeGb < LowSystemSpaceGb)
            ? DeviceHealthIssueNames.LowDiskSpace
            : null;
    }
}
