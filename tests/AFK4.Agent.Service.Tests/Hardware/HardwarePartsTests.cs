using System.Buffers.Binary;
using System.Text;
using AFK4.Agent.Service.Hardware;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Tests.Hardware;

/// <summary>Монитор узнаётся по EDID — те же 128 байт, что Windows читает из самого монитора.</summary>
public sealed class MonitorEdidTests
{
    [Fact]
    public void ReadsTheModel_TheMaker_AndTheSerial()
    {
        var monitor = MonitorEdid.Parse(Edid(name: "S24R35x", serial: "H4ZN500123"))!;

        Assert.Equal("S24R35x", monitor.Name);
        Assert.Equal("SAM", monitor.Manufacturer);
        Assert.Equal("H4ZN500123", monitor.Serial);
    }

    [Fact]
    public void WithoutAModelName_TheMakerAndProductCodeStandIn()
    {
        var monitor = MonitorEdid.Parse(Edid(name: null, serial: null, numericSerial: 0))!;

        Assert.Equal("SAM0F9A", monitor.Name);
        Assert.Null(monitor.Serial);
    }

    // Не у всех мониторов серийник текстом: тогда он числом в заголовке, а заглушки 0 и 01010101 — не серийник.
    [Fact]
    public void WithoutATextSerial_TheNumericOneIsUsed_ButNotAPlaceholder()
    {
        Assert.Equal("123456789", MonitorEdid.Parse(Edid(name: "DELL P2419H", serial: null, numericSerial: 123456789))!.Serial);
        Assert.Null(MonitorEdid.Parse(Edid(name: "DELL P2419H", serial: null, numericSerial: 0x01010101))!.Serial);
    }

    [Fact]
    public void SomethingThatIsNotAnEdid_IsNotAMonitor()
    {
        Assert.Null(MonitorEdid.Parse([]));
        Assert.Null(MonitorEdid.Parse(new byte[128]));
        Assert.Null(MonitorEdid.Parse(Edid(name: "S24R35x", serial: null).AsSpan(0, 100)));
    }

    internal static byte[] Edid(string? name, string? serial, uint numericSerial = 0)
    {
        var edid = new byte[128];
        new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 }.CopyTo(edid, 0);
        // «SAM»: три буквы по пять бит, старшим байтом вперёд.
        BinaryPrimitives.WriteUInt16BigEndian(edid.AsSpan(8), (ushort)((('S' - 'A' + 1) << 10) | (('A' - 'A' + 1) << 5) | ('M' - 'A' + 1)));
        BinaryPrimitives.WriteUInt16LittleEndian(edid.AsSpan(10), 0x0F9A);
        BinaryPrimitives.WriteUInt32LittleEndian(edid.AsSpan(12), numericSerial);
        // Первый блок — развёртка (частота пикселей не ноль), текстовые блоки — дальше.
        edid[54] = 0x01;
        edid[55] = 0x1D;
        if (name is not null) Descriptor(edid, 72, 0xFC, name);
        if (serial is not null) Descriptor(edid, 90, 0xFF, serial);
        return edid;
    }

    private static void Descriptor(byte[] edid, int offset, byte tag, string text)
    {
        edid[offset + 3] = tag;
        var body = edid.AsSpan(offset + 5, 13);
        body.Fill(0x20);
        var bytes = Encoding.ASCII.GetBytes(text);
        bytes.CopyTo(body);
        if (bytes.Length < 13) body[bytes.Length] = 0x0A;
    }
}

/// <summary>Физический накопитель — по описанию, которое отдаёт сам диск; флешка в USB железом ПК не считается.</summary>
public sealed class PhysicalDiskDescriptorTests
{
    private const long OneTerabyte = 1_000_204_886_016;

    [Fact]
    public void AnNvmeDrive_IsReportedByModelSizeAndBus()
    {
        var disk = PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor("NVMe", "Samsung SSD 980 PRO 1TB", busType: 17))!, OneTerabyte);

        Assert.Equal(new HardwarePhysicalDiskDto("Samsung SSD 980 PRO 1TB", 1000, "NVMe"), disk);
    }

    [Fact]
    public void AVendorIsKept_WhenItIsARealName_AndNotRepeated()
    {
        Assert.Equal("WDC WD10EZEX-08WN4A0", PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor("WDC     ", "WD10EZEX-08WN4A0", busType: 11))!, OneTerabyte)!.Model);
        Assert.Equal("Samsung SSD 860 EVO 500GB", PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor(null, "Samsung SSD 860 EVO 500GB", busType: 11))!, OneTerabyte)!.Model);
        Assert.Equal("KINGSTON SA400S37240G", PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor("KINGSTON", "KINGSTON SA400S37240G", busType: 11))!, OneTerabyte)!.Model);
    }

    // Флешку вставили — это не «железо изменилось». Как и смонтированный образ диска или сетевой диск бездисковой загрузки.
    [Theory]
    [InlineData(7, false)]   // USB
    [InlineData(12, false)]  // SD
    [InlineData(14, false)]  // виртуальный
    [InlineData(15, false)]  // образ VHD
    [InlineData(9, false)]   // iSCSI
    [InlineData(11, true)]   // SATA, но съёмный носитель
    public void RemovableVirtualAndNetworkDrives_AreNotThePcsHardware(int busType, bool removable)
    {
        Assert.Null(PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor("Kingston", "DataTraveler 3.0", busType, removable))!, 32_000_000_000));
    }

    // Логический диск на внешнем USB-диске Windows зовёт фиксированным; а C: бездисковой загрузки (iSCSI) — диск ПК.
    [Theory]
    [InlineData(7, false, true)]
    [InlineData(12, false, true)]
    [InlineData(11, true, true)]
    [InlineData(11, false, false)]
    [InlineData(17, false, false)]
    [InlineData(13, false, false)] // eMMC — встроенная память
    [InlineData(9, false, false)]
    public void APluggedInVolume_IsTheOneOnUsbOrRemovableMedia(int busType, bool removable, bool pluggedIn)
    {
        Assert.Equal(pluggedIn, PhysicalDiskDescriptor.IsPluggedIn(PhysicalDiskDescriptor.Parse(Descriptor("WD", "Elements 2620", busType, removable))!));
    }

    [Fact]
    public void ADriveWithoutAModelOrSize_IsSkipped()
    {
        Assert.Null(PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor(null, null, busType: 11))!, OneTerabyte));
        Assert.Null(PhysicalDiskDescriptor.ToDisk(PhysicalDiskDescriptor.Parse(Descriptor(null, "Samsung SSD 860 EVO 500GB", busType: 11))!, 0));
        Assert.Null(PhysicalDiskDescriptor.Parse(new byte[8]));
    }

    /// <summary>STORAGE_DEVICE_DESCRIPTOR: смещения строк от начала буфера, строки с нулём в конце.</summary>
    private static byte[] Descriptor(string? vendor, string? product, int busType, bool removable = false)
    {
        var buffer = new byte[256];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), (uint)buffer.Length);
        buffer[10] = removable ? (byte)1 : (byte)0;
        if (vendor is not null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12), 64);
            Encoding.ASCII.GetBytes(vendor).CopyTo(buffer, 64);
        }

        if (product is not null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), 128);
            Encoding.ASCII.GetBytes(product).CopyTo(buffer, 128);
        }

        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28), busType);
        return buffer;
    }
}
