using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Hardware;

public interface IHardwareSnapshotCollector
{
    /// <summary>Снимок железа или null — на этой системе собрать нечем.</summary>
    HardwareSnapshotDto? Collect();
}

public interface IHardwareReporter
{
    /// <summary>Отправить снимок, если пора: при старте, раз в сутки и когда железо изменилось.</summary>
    Task ReportIfDueAsync(CancellationToken cancellationToken);
}

public static class HardwareSchedule
{
    /// <summary>Как часто смотреть на железо: планку памяти не меняют на ходу, но и неделю ждать незачем.</summary>
    public static readonly TimeSpan CollectEvery = TimeSpan.FromHours(6);

    /// <summary>Как часто присылать без изменений: сервер видит, что ПК жив и опись свежая.</summary>
    public static readonly TimeSpan ReportEvery = TimeSpan.FromHours(24);

    public static bool ShouldReport(string? lastFingerprint, string fingerprint, DateTimeOffset lastReportedUtc, DateTimeOffset now) =>
        lastFingerprint != fingerprint || now - lastReportedUtc >= ReportEvery;

    /// <summary>Отпечаток снимка на стороне агента — только чтобы не слать одно и то же.</summary>
    public static string Fingerprint(HardwareSnapshotDto snapshot) => string.Join(
        '|',
        snapshot.Cpu,
        snapshot.CpuThreads,
        snapshot.MemoryGb,
        string.Join(',', snapshot.Gpus.Select(gpu => $"{gpu.Name}:{gpu.MemoryGb}")),
        snapshot.Motherboard,
        string.Join(',', snapshot.Disks.Select(disk => $"{disk.Name}:{disk.SizeGb}")),
        snapshot.Os,
        snapshot.Bios);
}

public sealed class HardwareReporter(
    IHardwareSnapshotCollector collector,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore,
    TimeProvider timeProvider,
    ILogger<HardwareReporter> logger) : IHardwareReporter
{
    private DateTimeOffset lastCollectedUtc = DateTimeOffset.MinValue;
    private DateTimeOffset lastReportedUtc = DateTimeOffset.MinValue;
    private string? lastFingerprint;

    public async Task ReportIfDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (now - lastCollectedUtc < HardwareSchedule.CollectEvery)
        {
            return;
        }

        lastCollectedUtc = now;
        var snapshot = collector.Collect();
        if (snapshot is null)
        {
            return;
        }

        var fingerprint = HardwareSchedule.Fingerprint(snapshot);
        if (!HardwareSchedule.ShouldReport(lastFingerprint, fingerprint, lastReportedUtc, now))
        {
            return;
        }

        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;
        using var message = new HttpRequestMessage(HttpMethod.Post, DeviceHardwareRoutes.Report(agentOptions.DeviceId))
        {
            Content = JsonContent.Create(new DeviceHardwareReportRequest(
                agentOptions.OrganizationId, agentOptions.BranchId, agentOptions.DeviceId, now, snapshot))
        };
        if (credentialStore.Current is { Length: > 0 } secret)
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secret);
        }

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();
            lastFingerprint = fingerprint;
            lastReportedUtc = now;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Не вышло — повтор через пять минут, а не через шесть часов и не на каждом сердцебиении.
            lastCollectedUtc = now - HardwareSchedule.CollectEvery + TimeSpan.FromMinutes(5);
            logger.LogWarning(exception, "Hardware snapshot was not delivered; will retry.");
        }
    }
}

/// <summary>Вне Windows собирать нечем.</summary>
public sealed class UnsupportedHardwareSnapshotCollector : IHardwareSnapshotCollector
{
    public HardwareSnapshotDto? Collect() => null;
}

/// <summary>
/// Железо из реестра и системных вызовов, без WMI: служба на слабых ПК зала не должна минутами
/// ждать ответа WMI, а реестр отвечает сразу. Что не прочиталось — пусто, а не падение.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareSnapshotCollector : IHardwareSnapshotCollector
{
    private const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public HardwareSnapshotDto? Collect() => new(
        Cpu: Read(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString")?.Trim(),
        CpuThreads: Environment.ProcessorCount,
        MemoryGb: MemoryGb(),
        Gpus: Gpus(),
        Motherboard: Join(Read(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardManufacturer"), Read(@"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct")),
        Disks: DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => new HardwareDiskDto(drive.Name.TrimEnd('\\'), (int)Math.Round(drive.TotalSize / 1_000_000_000d)))
            .ToList(),
        Os: Join(Read(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName"),
            Read(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion"),
            Read(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild") is { } build ? $"build {build}" : null),
        Bios: Join(Read(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVendor"), Read(@"HARDWARE\DESCRIPTION\System\BIOS", "BIOSVersion")));

    private static IReadOnlyList<HardwareGpuDto> Gpus()
    {
        using var root = Registry.LocalMachine.OpenSubKey(DisplayClass);
        if (root is null)
        {
            return [];
        }

        var gpus = new List<HardwareGpuDto>();
        foreach (var name in root.GetSubKeyNames().Where(name => name.All(char.IsAsciiDigit)))
        {
            try
            {
                using var adapter = root.OpenSubKey(name);
                if (adapter?.GetValue("DriverDesc") is not string description ||
                    description.Contains("Remote Display", StringComparison.OrdinalIgnoreCase) ||
                    description.Contains("Indirect Display", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var bytes = adapter.GetValue("HardwareInformation.qwMemorySize") switch
                {
                    long value => value,
                    byte[] raw when raw.Length >= 8 => BitConverter.ToInt64(raw, 0),
                    _ => adapter.GetValue("HardwareInformation.MemorySize") switch
                    {
                        int value => (long)(uint)value,
                        byte[] raw when raw.Length >= 4 => BitConverter.ToUInt32(raw, 0),
                        _ => 0L
                    }
                };
                gpus.Add(new HardwareGpuDto(description.Trim(), bytes > 0 ? (int)Math.Round(bytes / 1_073_741_824d) : null));
            }
            catch (System.Security.SecurityException)
            {
            }
        }

        return gpus.DistinctBy(gpu => gpu.Name).ToList();
    }

    private static int MemoryGb()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (int)Math.Round(status.TotalPhysical / 1_073_741_824d) : 0;
    }

    private static string? Read(string key, string value)
    {
        using var subKey = Registry.LocalMachine.OpenSubKey(key);
        return subKey?.GetValue(value) is string text && text.Trim().Length > 0 ? text.Trim() : null;
    }

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return present.Count == 0 ? null : string.Join(' ', present);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
}
