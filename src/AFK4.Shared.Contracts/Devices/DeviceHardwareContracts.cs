using System.Globalization;

namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Снимок железа ПК (спека оболочки, P9): что стоит внутри. Сравнивается с принятым — поменяли
/// видеокарту или вынули планку памяти, и клуб видит это в карточке ПК, а не узнаёт от игрока.
/// </summary>
public sealed record HardwareSnapshotDto(
    string? Cpu,
    int CpuThreads,
    /// Вся память в гигабайтах, округлённо: 15,9 ГБ Windows — это 16 ГБ в корпусе.
    int MemoryGb,
    IReadOnlyList<HardwareGpuDto> Gpus,
    string? Motherboard,
    IReadOnlyList<HardwareDiskDto> Disks,
    /// Windows и её сборка — видна, но не считается изменением железа: обновления идут каждый месяц.
    string? Os,
    string? Bios);

public sealed record HardwareGpuDto(string Name, int? MemoryGb);

/// <param name="Name">Буква диска: «C:».</param>
public sealed record HardwareDiskDto(string Name, int SizeGb);

public sealed record DeviceHardwareReportRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    DateTimeOffset CollectedAtUtc,
    HardwareSnapshotDto Snapshot);

/// <summary>Что в железе отличается от принятого: было → стало.</summary>
public sealed record HardwareChangeDto(
    /// Одно из HardwareComponentNames.
    string Component,
    string? Was,
    string? Now);

public static class HardwareComponentNames
{
    public const string Cpu = "cpu";
    public const string Memory = "memory";
    public const string Gpu = "gpu";
    public const string Motherboard = "motherboard";
    public const string Disk = "disk";
}

/// <summary>Железо ПК для карточки в Панели: сейчас, принятое и чем они отличаются.</summary>
public sealed record DeviceHardwareDto(
    HardwareSnapshotDto? Current,
    DateTimeOffset? ReportedAtUtc,
    HardwareSnapshotDto? Accepted,
    DateTimeOffset? AcceptedAtUtc,
    /// Кто принял; null — первый снимок, принятый сам.
    string? AcceptedByName,
    IReadOnlyList<HardwareChangeDto> Changes);

public static class DeviceHardwareRoutes
{
    public static string Report(Guid deviceId) => string.Create(
        CultureInfo.InvariantCulture, $"/api/devices/{deviceId:D}/hardware/report");
}
