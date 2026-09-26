using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.Versioning;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Hardware;

/// <summary>
/// Мониторы по EDID — блоку, который монитор сам отдаёт видеокарте, а Windows кладёт в реестр.
/// WMI (WmiMonitorID) читает тот же EDID, только медленнее и не из всякой сессии.
/// </summary>
public static class MonitorEdid
{
    private const string ConnectedMonitors = @"SYSTEM\CurrentControlSet\Services\monitor\Enum";
    private const uint NoSerial = 0;
    private const uint PlaceholderSerial = 0x01010101;

    private static ReadOnlySpan<byte> Header => [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    public static HardwareMonitorDto? Parse(ReadOnlySpan<byte> edid)
    {
        if (edid.Length < 128 || !edid[..8].SequenceEqual(Header))
        {
            return null;
        }

        var packed = BinaryPrimitives.ReadUInt16BigEndian(edid[8..]);
        var manufacturer = Manufacturer(packed);
        var product = BinaryPrimitives.ReadUInt16LittleEndian(edid[10..]);
        var numericSerial = BinaryPrimitives.ReadUInt32LittleEndian(edid[12..]);

        string? name = null;
        string? serial = null;
        for (var offset = 54; offset <= 108; offset += 18)
        {
            var block = edid.Slice(offset, 18);
            // Нулевая частота пикселей — текстовый блок, а не описание развёртки.
            if (block[0] != 0 || block[1] != 0 || block[2] != 0)
            {
                continue;
            }

            switch (block[3])
            {
                case 0xFC:
                    name ??= Text(block[5..]);
                    break;
                case 0xFF:
                    serial ??= Text(block[5..]);
                    break;
            }
        }

        if (serial is null && numericSerial is not NoSerial and not PlaceholderSerial)
        {
            serial = numericSerial.ToString(CultureInfo.InvariantCulture);
        }

        return new HardwareMonitorDto(
            name ?? manufacturer + product.ToString("X4", CultureInfo.InvariantCulture),
            manufacturer,
            serial);
    }

    /// <summary>
    /// Мониторы, подключённые сейчас. Пустой список — мониторов нет; null — прочитать не вышло, и
    /// тогда сервер не должен решить, что их унесли.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<HardwareMonitorDto>? ReadConnected()
    {
        try
        {
            // Служба monitor перечисляет только запущенные устройства; Enum\DISPLAY помнит все мониторы,
            // когда-либо подключённые к этому ПК.
            using var connected = Registry.LocalMachine.OpenSubKey(ConnectedMonitors);
            if (connected is null)
            {
                return [];
            }

            var monitors = new List<HardwareMonitorDto>();
            foreach (var index in connected.GetValueNames().Where(name => name.Length > 0 && name.All(char.IsAsciiDigit)))
            {
                if (connected.GetValue(index) is not string instance)
                {
                    continue;
                }

                using var parameters = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\{instance}\Device Parameters");
                if (parameters?.GetValue("EDID") is byte[] edid && Parse(edid) is { } monitor)
                {
                    monitors.Add(monitor);
                }
            }

            return monitors
                .OrderBy(monitor => monitor.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(monitor => monitor.Serial, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Manufacturer(ushort packed)
    {
        Span<char> letters = [Letter(packed >> 10), Letter(packed >> 5), Letter(packed)];
        return letters.Contains('\0') ? null : new string(letters);
    }

    private static char Letter(int bits) => (bits & 0x1F) is var value and >= 1 and <= 26 ? (char)('A' + value - 1) : '\0';

    /// <summary>Текст блока: до 13 знаков ASCII, конец — перевод строки, дальше пробелы.</summary>
    private static string? Text(ReadOnlySpan<byte> body)
    {
        var chars = new List<char>(13);
        foreach (var value in body[..Math.Min(13, body.Length)])
        {
            if (value is 0x0A or 0x00)
            {
                break;
            }

            if (value is >= 0x20 and < 0x7F)
            {
                chars.Add((char)value);
            }
        }

        var text = new string(chars.ToArray()).Trim();
        return text.Length == 0 ? null : text;
    }
}
