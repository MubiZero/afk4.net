using System.Runtime.InteropServices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Workstation;

/// <summary>
/// Громкость и микрофон — через Core Audio (устройство по умолчанию, как у значка в панели задач),
/// раскладка — через переднее окно. Устройство берётся заново на каждый вызов: игрок мог
/// переключить наушники, и громкость должна относиться к тому, что звучит сейчас.
/// COM — вызовами по таблице методов, без ComImport: во Native AOT встроенного COM-взаимодействия нет
/// (docs/roadmap/windows7.md).
/// </summary>
public sealed unsafe partial class WindowsSystemControls : ISystemControls
{
    private static readonly Guid EventContext = Guid.NewGuid();
    private static readonly Guid ClsidDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
    private static readonly Guid IidEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    public ShellSystemStateDto Read() => new(
        Volume: TryEndpoint(DataFlow.Render, endpoint =>
        {
            float level;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, float*, int>)Slot(endpoint, GetMasterVolumeLevelScalarSlot))(endpoint, &level));
            return (int?)Math.Round(level * 100);
        }),
        MicMuted: TryEndpoint(DataFlow.Capture, endpoint =>
        {
            int muted;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int*, int>)Slot(endpoint, GetMuteSlot))(endpoint, &muted));
            return (bool?)(muted != 0);
        }),
        Layout: CurrentLayout());

    public void SetVolume(int percent)
    {
        var level = Math.Clamp(percent, 0, 100) / 100f;
        WithEndpoint(DataFlow.Render, endpoint =>
        {
            var context = EventContext;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, float, Guid*, int>)Slot(endpoint, SetMasterVolumeLevelScalarSlot))(endpoint, level, &context));
        });
    }

    public void SetMicMuted(bool muted)
    {
        WithEndpoint(DataFlow.Capture, endpoint =>
        {
            var context = EventContext;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, Guid*, int>)Slot(endpoint, SetMuteSlot))(endpoint, muted ? 1 : 0, &context));
        });
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

    private static T? TryEndpoint<T>(DataFlow flow, Func<IntPtr, T?> read)
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

    private static void WithEndpoint(DataFlow flow, Action<IntPtr> action)
    {
        // Поток мог быть не готов к COM: готовим сами и снимаем только то, что подняли. RPC_E_CHANGED_MODE (поток уже
        // в другом режиме) — не помеха: Core Audio живёт и так.
        var init = CoInitializeEx(IntPtr.Zero, CoinitApartmentThreaded);
        IntPtr enumerator = IntPtr.Zero, device = IntPtr.Zero, endpoint = IntPtr.Zero;
        try
        {
            var clsid = ClsidDeviceEnumerator;
            var iid = IidDeviceEnumerator;
            Check(CoCreateInstance(&clsid, IntPtr.Zero, ClsCtxAll, &iid, &enumerator));
            IntPtr found;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, int, int, IntPtr*, int>)Slot(enumerator, GetDefaultAudioEndpointSlot))(
                enumerator, (int)flow, RoleMultimedia, &found));
            device = found;
            var volumeIid = IidEndpointVolume;
            IntPtr volume;
            Check(((delegate* unmanaged[Stdcall]<IntPtr, Guid*, int, IntPtr, IntPtr*, int>)Slot(device, ActivateSlot))(
                device, &volumeIid, ClsCtxAll, IntPtr.Zero, &volume));
            endpoint = volume;
            action(endpoint);
        }
        finally
        {
            Release(endpoint);
            Release(device);
            Release(enumerator);
            if (init >= 0)
            {
                CoUninitialize();
            }
        }
    }

    private static void Check(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    // Объект COM — указатель на указатель на таблицу методов.
    private static void* Slot(IntPtr instance, int index) => (*(void***)instance)[index];

    private static void Release(IntPtr instance)
    {
        if (instance != IntPtr.Zero)
        {
            ((delegate* unmanaged[Stdcall]<IntPtr, uint>)Slot(instance, ReleaseSlot))(instance);
        }
    }

    // Номера в таблице методов: IUnknown — 0..2, дальше по порядку объявления в заголовках Windows.
    private const int ReleaseSlot = 2;
    private const int GetDefaultAudioEndpointSlot = 4; // IMMDeviceEnumerator
    private const int ActivateSlot = 3; // IMMDevice
    private const int SetMasterVolumeLevelScalarSlot = 7; // IAudioEndpointVolume
    private const int GetMasterVolumeLevelScalarSlot = 9;
    private const int SetMuteSlot = 14;
    private const int GetMuteSlot = 15;

    private const int ClsCtxAll = 0x17;
    private const uint CoinitApartmentThreaded = 2;
    private const int RoleMultimedia = 1;
    private const uint KlfActivate = 0x00000001;
    private const uint WmInputLangChangeRequest = 0x0050;

    private enum DataFlow
    {
        Render = 0,
        Capture = 1
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(IntPtr reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, IntPtr outer, int context, Guid* iid, IntPtr* result);

    [LibraryImport("user32.dll", EntryPoint = "LoadKeyboardLayoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr LoadKeyboardLayout(string klid, uint flags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetKeyboardLayout(uint threadId);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    private static partial int PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
