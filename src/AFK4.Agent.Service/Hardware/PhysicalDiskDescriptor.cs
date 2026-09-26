using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Win32.SafeHandles;

namespace AFK4.Agent.Service.Hardware;

/// <param name="BusType">STORAGE_BUS_TYPE: 7 — USB, 11 — SATA, 17 — NVMe.</param>
public sealed record StorageDescriptor(string? Vendor, string? Product, int BusType, bool Removable);

/// <summary>
/// Физические накопители — по описанию, которое диск отдаёт на IOCTL_STORAGE_QUERY_PROPERTY: тот же
/// источник, что у Win32_DiskDrive, но без WMI и за миллисекунды.
/// </summary>
public static class PhysicalDiskDescriptor
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlDiskGetDriveGeometryEx = 0x000700A0;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;
    private const int MaxDriveNumber = 32;

    /// <summary>
    /// Вставляются снаружи: FireWire, USB, карты SD. Вставили флешку — «железо изменилось» подниматься
    /// не должно. MMC сюда не входит: на этой шине сидит встроенная память eMMC.
    /// </summary>
    private static readonly HashSet<int> PluggedInBuses = [4, 7, 12];

    /// <summary>Не физический диск в корпусе: сетевой диск бездисковой загрузки (iSCSI), виртуальный, образ VHD.</summary>
    private static readonly HashSet<int> NotPhysicalBuses = [9, 14, 15];

    public static StorageDescriptor? Parse(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 32)
        {
            return null;
        }

        return new StorageDescriptor(
            Vendor: Text(buffer, BinaryPrimitives.ReadUInt32LittleEndian(buffer[12..])),
            Product: Text(buffer, BinaryPrimitives.ReadUInt32LittleEndian(buffer[16..])),
            BusType: BinaryPrimitives.ReadInt32LittleEndian(buffer[28..]),
            Removable: buffer[10] != 0);
    }

    public static bool IsPluggedIn(StorageDescriptor descriptor) => descriptor.Removable || PluggedInBuses.Contains(descriptor.BusType);

    public static HardwarePhysicalDiskDto? ToDisk(StorageDescriptor descriptor, long sizeBytes)
    {
        if (IsPluggedIn(descriptor) || NotPhysicalBuses.Contains(descriptor.BusType) || sizeBytes <= 0)
        {
            return null;
        }

        var model = Model(descriptor.Vendor, descriptor.Product);
        return model is null
            ? null
            : new HardwarePhysicalDiskDto(model, (int)Math.Round(sizeBytes / 1_000_000_000d), Interface(descriptor.BusType));
    }

    /// <summary>
    /// Накопители внутри корпуса. Пустой список — ни одного (бездисковая загрузка); null — ни один
    /// диск не открылся, и это «неизвестно», а не «все вынули».
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<HardwarePhysicalDiskDto>? ReadInternal()
    {
        try
        {
            var opened = 0;
            var disks = new List<HardwarePhysicalDiskDto>();
            // Номера идут с пропусками — вынули флешку, и следующий диск не сдвигается, — поэтому до потолка.
            for (var number = 0; number < MaxDriveNumber; number++)
            {
                using var drive = CreateFile($@"\\.\PhysicalDrive{number}", 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
                if (drive.IsInvalid)
                {
                    continue;
                }

                opened++;
                if (Query(drive) is { } descriptor && ToDisk(descriptor, Size(drive)) is { } disk)
                {
                    disks.Add(disk);
                }
            }

            return opened == 0
                ? null
                : disks.OrderBy(disk => disk.Model, StringComparer.OrdinalIgnoreCase).ThenBy(disk => disk.SizeGb).ToList();
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Логический диск на внешнем USB-диске или флешке. Windows зовёт такие «фиксированными», и без
    /// этой проверки подключённый внешний диск попадал в «Диски» как изменение железа. Не
    /// выяснилось — считаем внутренним, как было до проверки.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool IsPluggedInVolume(string driveName)
    {
        try
        {
            using var volume = CreateFile($@"\\.\{driveName.TrimEnd('\\')}", 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            return !volume.IsInvalid && Query(volume) is { } descriptor && IsPluggedIn(descriptor);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string? Model(string? vendor, string? product)
    {
        // Для SATA и NVMe Windows пишет в «производителя» название шины или ничего — модель целиком в product.
        if (vendor is null || vendor.Equals("ATA", StringComparison.OrdinalIgnoreCase) || vendor.Equals("NVMe", StringComparison.OrdinalIgnoreCase))
        {
            return product;
        }

        if (product is null)
        {
            return vendor;
        }

        return product.StartsWith(vendor, StringComparison.OrdinalIgnoreCase) ? product : $"{vendor} {product}";
    }

    private static string? Interface(int busType) => busType switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        6 => "Fibre Channel",
        8 => "RAID",
        10 => "SAS",
        11 => "SATA",
        13 => "eMMC",
        16 => "Storage Spaces",
        17 => "NVMe",
        18 => "SCM",
        19 => "UFS",
        _ => null
    };

    /// <summary>Строка описания: смещение от начала буфера, ASCII до нуля; у SCSI добита пробелами.</summary>
    private static string? Text(ReadOnlySpan<byte> buffer, uint offset)
    {
        if (offset == 0 || offset >= buffer.Length)
        {
            return null;
        }

        var tail = buffer[(int)offset..];
        var end = tail.IndexOf((byte)0);
        var text = Encoding.ASCII.GetString(end < 0 ? tail : tail[..end]).Trim();
        return text.Length == 0 ? null : text;
    }

    [SupportedOSPlatform("windows")]
    private static StorageDescriptor? Query(SafeFileHandle drive)
    {
        // STORAGE_PROPERTY_QUERY из нулей — StorageDeviceProperty, PropertyStandardQuery.
        var query = new byte[12];
        var output = new byte[1024];
        return DeviceIoControl(drive, IoctlStorageQueryProperty, query, query.Length, output, output.Length, out var returned, IntPtr.Zero)
            ? Parse(output.AsSpan(0, returned))
            : null;
    }

    [SupportedOSPlatform("windows")]
    private static long Size(SafeFileHandle drive)
    {
        // DISK_GEOMETRY_EX: 24 байта геометрии, за ними размер диска в байтах.
        var output = new byte[256];
        return DeviceIoControl(drive, IoctlDiskGetDriveGeometryEx, null, 0, output, output.Length, out var returned, IntPtr.Zero) && returned >= 32
            ? BinaryPrimitives.ReadInt64LittleEndian(output.AsSpan(24))
            : 0;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device, uint ioControlCode, byte[]? inBuffer, int inBufferSize, byte[] outBuffer, int outBufferSize, out int bytesReturned, IntPtr overlapped);
}
