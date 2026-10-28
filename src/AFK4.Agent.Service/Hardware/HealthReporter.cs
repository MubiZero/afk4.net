using System.Management;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Hardware;

public interface IHealthCollector
{
    DeviceHealthDto? Collect();
}

/// <summary>
/// Здоровье ПК раз в пять минут (docs/operations/pc-health.md): нагрузка, температуры, диски, место. Чаще
/// незачем — перегрев и умирающий диск не развиваются за секунды, а слабому ПК лишний опрос WMI не нужен.
/// </summary>
public sealed class HealthReporter(
    IHealthCollector collector,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore,
    TimeProvider timeProvider,
    ILogger<HealthReporter> logger)
{
    public static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(5);

    private DateTimeOffset lastAttemptUtc = DateTimeOffset.MinValue;

    public async Task ReportIfDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (now - lastAttemptUtc < ReportEvery)
        {
            return;
        }

        lastAttemptUtc = now;
        var health = collector.Collect();
        if (health is null)
        {
            return;
        }

        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;
        using var message = new HttpRequestMessage(HttpMethod.Post, DeviceHealthRoutes.Report(agentOptions.DeviceId))
        {
            Content = JsonContent.Create(new DeviceHealthReportRequest(
                agentOptions.OrganizationId, agentOptions.BranchId, agentOptions.DeviceId, now, health))
        };
        if (credentialStore.Current is { Length: > 0 } secret)
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, secret);
        }

        try
        {
            using var response = await client.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Без связи следующий снимок через пять минут всё равно свежее этого.
            logger.LogDebug(exception, "Health report was not delivered.");
        }
    }
}

/// <summary>Вне Windows читать нечего.</summary>
public sealed class UnsupportedHealthCollector : IHealthCollector
{
    public DeviceHealthDto? Collect() => null;
}

/// <summary>
/// Без драйвера ядра: нагрузка — GetSystemTimes, память — GlobalMemoryStatusEx, плата — тепловые зоны ACPI,
/// видеокарта — NVML из драйвера NVIDIA, диски — хранилище Windows (MSFT_PhysicalDisk и счётчики надёжности).
/// Каждый источник читается отдельно: не нашёлся один — остальные всё равно приходят.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHealthCollector(
    ILogger<WindowsHealthCollector> logger,
    AFK4.Agent.Service.Network.INetworkIdentityProvider? network = null) : IHealthCollector
{
    private (ulong Idle, ulong Busy)? previousCpu;

    public DeviceHealthDto? Collect() => new(
        CpuLoadPercent: CpuLoad(),
        MemoryUsedPercent: MemoryUsed(),
        BoardTemperatureC: Safe(BoardTemperature),
        Gpus: Safe(NvidiaGpus.Read) ?? [],
        Disks: Safe(Disks) ?? [],
        Volumes: Volumes(),
        // Включение по сети: агент сам держит ПК готовым и докладывает, что вышло.
        WakeOnLan: AFK4.Agent.Service.Power.WakeOnLanReadiness.Ensure(network?.Current?.MacAddress));

    private int? CpuLoad()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return null;
        }

        // Ядро включает простой: занятое — ядро и пользователь минус простой. Нагрузка — между двумя снимками.
        var current = (Idle: idle, Busy: kernel + user - idle);
        var previous = previousCpu;
        previousCpu = current;
        if (previous is not { } last || current.Idle + current.Busy <= last.Idle + last.Busy)
        {
            return null;
        }

        var busy = current.Busy - last.Busy;
        var total = busy + current.Idle - last.Idle;
        return (int)Math.Round(100d * busy / total);
    }

    private static int? MemoryUsed()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? (int)status.MemoryLoad : null;
    }

    private static int? BoardTemperature()
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\cimv2", "SELECT HighPrecisionTemperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");
        int? hottest = null;
        foreach (var zone in searcher.Get())
        {
            // Десятые доли кельвина.
            if (zone["HighPrecisionTemperature"] is uint tenthsKelvin && tenthsKelvin > 0)
            {
                var celsius = (int)Math.Round(tenthsKelvin / 10d - 273.15);
                if (celsius is > 0 and < 150) hottest = Math.Max(hottest ?? int.MinValue, celsius);
            }
        }

        return hottest;
    }

    private static IReadOnlyList<DiskHealthDto> Disks()
    {
        var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
        var counters = new Dictionary<string, ManagementBaseObject>(StringComparer.Ordinal);
        using (var reliability = new ManagementObjectSearcher(scope, new ObjectQuery(
            "SELECT DeviceId, Temperature, Wear, PowerOnHours, ReadErrorsUncorrected FROM MSFT_StorageReliabilityCounter")))
        {
            foreach (var counter in reliability.Get())
            {
                if (counter["DeviceId"] is string id) counters[id] = counter;
            }
        }

        var disks = new List<DiskHealthDto>();
        using var physical = new ManagementObjectSearcher(scope, new ObjectQuery(
            "SELECT DeviceId, FriendlyName, HealthStatus, BusType FROM MSFT_PhysicalDisk"));
        foreach (var disk in physical.Get())
        {
            // Флешки и карты (USB = 7, SD = 12) — не диски ПК.
            if (disk["BusType"] is ushort bus && bus is 7 or 12)
            {
                continue;
            }

            counters.TryGetValue(disk["DeviceId"] as string ?? string.Empty, out var counter);
            disks.Add(new DiskHealthDto(
                Model: (disk["FriendlyName"] as string ?? "?").Trim(),
                Health: disk["HealthStatus"] switch
                {
                    (ushort)0 => DiskHealthNames.Healthy,
                    (ushort)1 => DiskHealthNames.Warning,
                    (ushort)2 => DiskHealthNames.Unhealthy,
                    _ => null
                },
                TemperatureC: AsInt(counter?["Temperature"]) is > 0 and var temperature ? temperature : null,
                WearPercent: AsInt(counter?["Wear"]),
                PowerOnHours: AsInt(counter?["PowerOnHours"]),
                ReadErrorsUncorrected: counter?["ReadErrorsUncorrected"] is ulong errors ? (long)errors : null));
        }

        return disks;
    }

    private static IReadOnlyList<VolumeSpaceDto> Volumes()
    {
        var system = Path.GetPathRoot(Environment.SystemDirectory);
        return DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => new VolumeSpaceDto(
                drive.Name.TrimEnd('\\'),
                (int)(drive.AvailableFreeSpace / 1_000_000_000),
                (int)Math.Round(drive.TotalSize / 1_000_000_000d),
                string.Equals(drive.Name, system, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static int? AsInt(object? value) => value switch
    {
        byte number => number,
        ushort number => number,
        uint number => (int)Math.Min(number, int.MaxValue),
        ulong number => (int)Math.Min(number, int.MaxValue),
        _ => null
    };

    private T? Safe<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception exception) when (exception is ManagementException or COMException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            logger.LogDebug(exception, "Health source is not available on this PC.");
            return default;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

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
}

/// <summary>
/// Видеокарты NVIDIA через nvml.dll, которую ставит драйвер NVIDIA (System32). Нет драйвера — пустой список.
/// Работает из службы без входа пользователя, как nvidia-smi.
/// </summary>
internal static class NvidiaGpus
{
    private const int Success = 0;
    private const int TemperatureGpu = 0;

    public static IReadOnlyList<GpuHealthDto> Read()
    {
        if (!NativeLibrary.TryLoad("nvml.dll", typeof(NvidiaGpus).Assembly, DllImportSearchPath.System32, out var library))
        {
            return [];
        }

        try
        {
            var init = Bind<NvmlInit>(library, "nvmlInit_v2");
            var shutdown = Bind<NvmlShutdown>(library, "nvmlShutdown");
            if (init is null || shutdown is null || init() != Success)
            {
                return [];
            }

            try
            {
                var count = Bind<NvmlGetCount>(library, "nvmlDeviceGetCount_v2");
                var byIndex = Bind<NvmlGetHandle>(library, "nvmlDeviceGetHandleByIndex_v2");
                var name = Bind<NvmlGetName>(library, "nvmlDeviceGetName");
                var temperature = Bind<NvmlGetTemperature>(library, "nvmlDeviceGetTemperature");
                var fan = Bind<NvmlGetUint>(library, "nvmlDeviceGetFanSpeed");
                var utilization = Bind<NvmlGetUtilization>(library, "nvmlDeviceGetUtilizationRates");
                if (count is null || byIndex is null || count(out var devices) != Success)
                {
                    return [];
                }

                var gpus = new List<GpuHealthDto>();
                for (uint index = 0; index < devices; index++)
                {
                    if (byIndex(index, out var device) != Success) continue;
                    var buffer = new StringBuilder(96);
                    var gpuName = name?.Invoke(device, buffer, (uint)buffer.Capacity) == Success ? buffer.ToString() : "NVIDIA";
                    gpus.Add(new GpuHealthDto(
                        gpuName,
                        temperature?.Invoke(device, TemperatureGpu, out var celsius) == Success ? (int)celsius : null,
                        fan?.Invoke(device, out var fanPercent) == Success ? (int)fanPercent : null,
                        utilization?.Invoke(device, out var rates) == Success ? (int)rates.Gpu : null));
                }

                return gpus;
            }
            finally
            {
                shutdown();
            }
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }

    private static T? Bind<T>(IntPtr library, string name) where T : Delegate =>
        NativeLibrary.TryGetExport(library, name, out var address) ? Marshal.GetDelegateForFunctionPointer<T>(address) : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct Utilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlInit();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlShutdown();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlGetCount(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlGetHandle(uint index, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate int NvmlGetName(IntPtr device, StringBuilder name, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlGetTemperature(IntPtr device, int sensor, out uint celsius);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlGetUint(IntPtr device, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NvmlGetUtilization(IntPtr device, out Utilization rates);
}
