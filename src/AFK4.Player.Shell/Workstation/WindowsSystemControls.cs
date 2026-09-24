using System.Runtime.InteropServices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Workstation;

/// <summary>
/// Громкость и микрофон — через Core Audio (устройство по умолчанию, как у значка в панели задач),
/// раскладка — через переднее окно. Устройство берётся заново на каждый вызов: игрок мог
/// переключить наушники, и громкость должна относиться к тому, что звучит сейчас.
/// </summary>
public sealed class WindowsSystemControls : ISystemControls
{
    private static readonly Guid EventContext = Guid.NewGuid();

    public ShellSystemStateDto Read() => new(
        Volume: TryEndpoint(DataFlow.Render, endpoint =>
        {
            Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out var level));
            return (int?)Math.Round(level * 100);
        }),
        MicMuted: TryEndpoint(DataFlow.Capture, endpoint =>
        {
            Marshal.ThrowExceptionForHR(endpoint.GetMute(out var muted));
            return (bool?)muted;
        }),
        Layout: CurrentLayout());

    public void SetVolume(int percent)
    {
        var level = Math.Clamp(percent, 0, 100) / 100f;
        var context = EventContext;
        WithEndpoint(DataFlow.Render, endpoint => Marshal.ThrowExceptionForHR(endpoint.SetMasterVolumeLevelScalar(level, ref context)));
    }

    public void SetMicMuted(bool muted)
    {
        var context = EventContext;
        WithEndpoint(DataFlow.Capture, endpoint => Marshal.ThrowExceptionForHR(endpoint.SetMute(muted, ref context)));
    }

    public void SetLayout(string label)
    {
        var klid = KeyboardLayouts.KlidFor(label) ?? throw new ArgumentException($"Layout '{label}' is not offered.", nameof(label));
        var layout = LoadKeyboardLayout(klid, KlfActivate);
        if (layout == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Windows could not load keyboard layout {klid}.");
        }

        // Раскладку меняет то окно, в котором печатают: при настройках Windows по умолчанию она
        // общая для всех окон сессии.
        PostMessage(GetForegroundWindow(), WmInputLangChangeRequest, IntPtr.Zero, layout);
    }

    private static string? CurrentLayout()
    {
        var thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var layout = GetKeyboardLayout(thread).ToInt64();
        return KeyboardLayouts.LabelFor((ushort)(layout & 0xFFFF));
    }

    private static T? TryEndpoint<T>(DataFlow flow, Func<IAudioEndpointVolume, T?> read)
    {
        try
        {
            T? value = default;
            WithEndpoint(flow, endpoint => value = read(endpoint));
            return value;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            // Нет устройства (микрофон не подключён) — у этого поля нет значения, и только.
            return default;
        }
    }

    private static void WithEndpoint(DataFlow flow, Action<IAudioEndpointVolume> action)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        IMMDevice? device = null;
        object? endpoint = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia, out device));
            var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out endpoint));
            action((IAudioEndpointVolume)endpoint);
        }
        finally
        {
            if (endpoint is not null)
            {
                Marshal.ReleaseComObject(endpoint);
            }

            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            Marshal.ReleaseComObject(enumerator);
        }
    }

    private const int ClsCtxAll = 0x17;
    private const uint KlfActivate = 0x00000001;
    private const uint WmInputLangChangeRequest = 0x0050;

    private enum DataFlow
    {
        Render = 0,
        Capture = 1
    }

    private enum Role
    {
        Console = 0,
        Multimedia = 1
    }

    // Не sealed: COM-класс приводится к интерфейсу во время выполнения, компилятору это надо разрешить.
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject;

    // Порядок методов — порядок таблицы COM: объявлять можно только с начала и без пропусков.
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(DataFlow dataFlow, int stateMask, out IntPtr devices);

        [PreserveSig] int GetDefaultAudioEndpoint(DataFlow dataFlow, Role role, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);

        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);

        [PreserveSig] int GetChannelCount(out uint channels);

        [PreserveSig] int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);

        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);

        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);

        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);

        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);

        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);

        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadKeyboardLayout(string klid, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
