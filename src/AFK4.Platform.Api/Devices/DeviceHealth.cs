using System.Globalization;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

/// <summary>Короткая подробность для сообщения владельцу: какой диск и что с ним, сколько градусов у видеокарты.</summary>
public static class DeviceHealth
{
    public static string Detail(DeviceHealthDto health, string issue) => issue switch
    {
        DeviceHealthIssueNames.DiskFailing => health.Disks
            .Where(disk => disk.Health == DiskHealthNames.Unhealthy || disk.ReadErrorsUncorrected > 0)
            .Select(disk => disk.Model)
            .FirstOrDefault() ?? string.Empty,
        DeviceHealthIssueNames.GpuOverheating => health.Gpus
            .Where(gpu => gpu.TemperatureC is not null)
            .OrderByDescending(gpu => gpu.TemperatureC)
            .Select(gpu => string.Create(CultureInfo.InvariantCulture, $"{gpu.TemperatureC} °C"))
            .FirstOrDefault() ?? string.Empty,
        _ => string.Empty
    };
}
