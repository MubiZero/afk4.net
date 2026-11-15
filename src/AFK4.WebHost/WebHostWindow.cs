using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace AFK4.WebHost;

/// <summary>
/// Окно Windows с экраном на Miniblink. Экран — собранная папка, отдаётся по своему имени (<c>https://setup.afk4.local/</c>)
/// без сети; уйти со своих страниц внутри окна нельзя, внешние ссылки открывает браузер Windows. Мост — тот же, что у
/// WebView2 (<c>postMessage</c> / событие <c>message</c>), но под именем <c>window.afk4Host</c>: в Miniblink
/// <c>window.chrome</c> неизменяем.
/// </summary>
public sealed unsafe class WebHostWindow
{
    private const string WindowClass = "AFK4WebHostWindow";
    private static readonly string LineSeparator = ((char)0x2028).ToString();
    private static readonly string ParagraphSeparator = ((char)0x2029).ToString();
    private const int BridgeMessage = 0x4146;
    private const uint InvokeMessage = Win32.WmApp + 1;
    private const int ResizeGrip = 6;
    private const int FramelessBorder = 4;

    private static readonly Dictionary<IntPtr, WebHostWindow> ByWindow = [];
    // Запросы сети и навигацию Miniblink отдаёт из своего потока, не из потока окна.
    private static readonly ConcurrentDictionary<IntPtr, WebHostWindow> ByView = new();
    private static readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> Pending = new();
    private static int uiThreadId;
    private static uint nativeUiThreadId;
    // Работа для потока окон приходит сообщением этому невидимому окну, а не потоку: Miniblink крутит свой цикл
    // сообщений и сообщения потоку выбрасывает — продолжения await терялись (стенд Windows 11, 05.11.2026).
    private static IntPtr dispatcher;
    private static double scale = 1;
    private static bool lastWindowClosed;
    private static Action? tick;
    private const uint TickTimerId = 2;

    private readonly WebHostOptions options;
    private readonly string origin;
    private readonly string contentRoot;
    private readonly List<(string Origin, string Root)> extraRoots = [];
    private readonly IntPtr background;
    private IntPtr view;
    private bool topmost;
    private bool closingByProgram;
    private bool bandDocked;
    private int bandPixels;
    private uint bandMessage;
    private (string Headline, string Text)? fallback;

    /// <summary>Сообщение экрана — JSON того, что он передал в <c>postMessage</c>.</summary>
    public event Action<string>? MessageReceived;

    /// <summary>Окно развернули или вернули: экран без рамки рисует кнопку по этому состоянию.</summary>
    public event Action<bool>? MaximizedChanged;

    /// <summary>Диагностика движка: ошибки загрузки, консоль страницы, отклонённые адреса.</summary>
    public event Action<string>? Diagnostic;

    public IntPtr Handle { get; private set; }

    public bool IsMaximized => Win32.IsZoomed(Handle) != 0;

    public bool IsMinimized => Win32.IsIconic(Handle) != 0;

    /// <summary>
    /// Окно впереди — клавиатура и мышь у него, а не у игры. Сравнивается процесс, а не окно: передним Windows считает
    /// и скрытое окно меню Miniblink (<c>_ContextMenu_</c>) — стенд Windows 11, 05.11.2026.
    /// </summary>
    public bool IsInFront
    {
        get
        {
            var front = Win32.GetForegroundWindow();
            uint processId = 0;
            return front != IntPtr.Zero && Win32.GetWindowThreadProcessId(front, (IntPtr)(&processId)) != 0
                && processId == (uint)Environment.ProcessId;
        }
    }

    public WebHostWindow(WebHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (uiThreadId == 0)
        {
            throw new InvalidOperationException("Call WebHostWindow.Initialize() on the UI thread first.");
        }

        this.options = options;
        origin = $"https://{options.VirtualHost}/";
        contentRoot = Path.GetFullPath(options.ContentRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var (host, folder) in options.ExtraHosts ?? new Dictionary<string, string>())
        {
            extraRoots.Add(($"https://{host}/", Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar));
        }

        Directory.CreateDirectory(options.DataFolder);
        // Windows ждёт цвет как 0x00BBGGRR.
        var rgb = options.BackgroundRgb;
        background = Win32.CreateSolidBrush(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));

        // Киоск — голое окно: без рамки, меню окна и кнопок, — его не двигают, не тянут и не закрывают.
        var style = Win32.WsClipChildren | (options.Kiosk ? Win32.WsPopup
            : options.Frameless ? Win32.WsPopup | Win32.WsThickFrame | Win32.WsSysMenu | Win32.WsMinimizeBox | Win32.WsMaximizeBox
            : Win32.WsOverlappedWindow);
        topmost = options.Kiosk;
        var (startX, startY, startWidth, startHeight) = options.InitialBounds
            ?? (Win32.CwUseDefault, Win32.CwUseDefault, Scaled(options.Width), Scaled(options.Height));
        Handle = Win32.CreateWindowEx(topmost ? Win32.WsExTopmost : 0, WindowClass, options.Title, style, startX, startY,
            startWidth, startHeight, IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandle(IntPtr.Zero), IntPtr.Zero);
        if (Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastPInvokeError()}");
        }

        ByWindow[Handle] = this;
        if (options.Kiosk)
        {
            CoverMonitor();
        }
        else if (options.InitialBounds is null)
        {
            CenterInWorkArea();
        }

        if (options.BorderRgb is { } border)
        {
            // Windows 11: скруглённые углы и тонкий контур у окна без рамки; на старых системах вызов ничего не делает.
            var corner = Win32.DwmwcpRound;
            Win32.DwmSetWindowAttribute(Handle, Win32.DwmwaWindowCornerPreference, &corner, sizeof(int));
            var color = (int)(((border & 0xFF) << 16) | (border & 0xFF00) | ((border >> 16) & 0xFF));
            Win32.DwmSetWindowAttribute(Handle, Win32.DwmwaBorderColor, &color, sizeof(int));
        }

        view = Miniblink.mbCreateWebWindow(Miniblink.WindowTypeControl, Handle, 0, 0, 1, 1);
        ByView[view] = this;
        Miniblink.mbSetCookieJarFullPath(view, Path.Combine(options.DataFolder, "cookies.dat"));
        Miniblink.mbSetLocalStorageFullPath(view, Path.Combine(options.DataFolder, "LocalStorage"));
        Miniblink.mbSetContextMenuEnabled(view, 0);
        Miniblink.mbSetNavigationToNewWindowEnable(view, 0);
        Miniblink.mbSetDragDropEnable(view, 0);
        Miniblink.mbOnJsQuery(view, &OnQuery, IntPtr.Zero);
        Miniblink.mbOnLoadUrlBegin(view, &OnLoadBegin, IntPtr.Zero);
        Miniblink.mbOnNavigation(view, &OnNavigation, IntPtr.Zero);
        Miniblink.mbOnLoadingFinish(view, &OnLoadingFinish, IntPtr.Zero);
        Miniblink.mbOnConsole(view, &OnConsole, IntPtr.Zero);
        LayoutView();
        Miniblink.mbShowWindow(view, 1);
    }

    /// <summary>
    /// Один раз за процесс, в потоке окон: DLL своей разрядности, масштаб Windows, движок и класс окна. Поток становится
    /// потоком окон — его <see cref="SynchronizationContext"/> возвращает продолжения <c>await</c> сюда.
    /// </summary>
    public static void Initialize()
    {
        if (uiThreadId != 0)
        {
            return;
        }

        uiThreadId = Environment.CurrentManagedThreadId;
        nativeUiThreadId = Win32.GetCurrentThreadId();

        Win32.SetProcessDPIAware();
        scale = Win32.SystemScale();
        Miniblink.UseBundledLibrary();
        Miniblink.mbInit(null);
        // Только после mbInit: раньше движок падает. Без него страница рисуется в 100%, а нажатия уходят мимо.
        Miniblink.mbEnableHighDPISupport();

        fixed (char* className = WindowClass)
        {
            var windowClass = new Win32.WndClassEx
            {
                Size = sizeof(Win32.WndClassEx),
                WndProc = &WindowProcedure,
                Instance = Win32.GetModuleHandle(IntPtr.Zero),
                Icon = Win32.LoadIcon(Win32.GetModuleHandle(IntPtr.Zero), 32512),
                Cursor = Win32.LoadCursor(IntPtr.Zero, 32512),
                ClassName = className
            };
            if (Win32.RegisterClassEx(&windowClass) == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastPInvokeError()}");
            }
        }

        dispatcher = Win32.CreateWindowEx(0, WindowClass, string.Empty, 0, 0, 0, 0, 0, Win32.HwndMessage, IntPtr.Zero,
            Win32.GetModuleHandle(IntPtr.Zero), IntPtr.Zero);
        if (dispatcher == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx (dispatcher) failed: {Marshal.GetLastPInvokeError()}");
        }

        SynchronizationContext.SetSynchronizationContext(new UiContext());
        NativeCard.RegisterClass(scale);
    }

    /// <summary>Масштаб Windows для окон программы (1 = 100%): размеры в точках умножаются на него.</summary>
    public static double Scale => scale;

    /// <summary>
    /// Звать <paramref name="action"/> в потоке окон каждые <paramref name="milliseconds"/>: опрос окон, ввода и звука.
    /// Один таймер на процесс; сбой в действии ловит вызывающий.
    /// </summary>
    public static void SetTick(int milliseconds, Action action)
    {
        tick = action;
        Win32.SetTimer(dispatcher, TickTimerId, (uint)milliseconds, IntPtr.Zero);
    }

    /// <summary>Страница видна или спрятана: пока впереди игра, экран игрока скрыт и не рисует кадры.</summary>
    public void SetPageVisible(bool visible) => OnUiThread(() =>
    {
        // Окно движка не прячем (mbShowWindow(0) на Windows 7 x86 роняло Miniblink: c0000005 в mb132_x32.dll, 15.11.2026):
        // страница сама узнаёт, что скрыта, как в браузере, и перестаёт тикать (visibilitychange).
        var state = visible ? "visible" : "hidden";
        var script = "(function(){try{Object.defineProperty(document,'visibilityState',{value:'" + state + "',configurable:true});"
            + "Object.defineProperty(document,'hidden',{value:" + (visible ? "false" : "true") + ",configurable:true});"
            + "document.dispatchEvent(new Event('visibilitychange'));}catch(e){}})();";
        WithUtf8(script, text => Miniblink.mbRunJs(view, Miniblink.mbWebFrameGetMainFrame(view), text, 0, null, IntPtr.Zero, IntPtr.Zero));
        if (visible && fallback is null)
        {
            FocusView();
        }
    });

    /// <summary>
    /// Запасной экран, нарисованный самим окном (GDI, без движка): остаток времени и пояснение. Нужен, когда сам
    /// движок упал и страница не нарисует ничего — закрытый экран всё равно закрыт.
    /// </summary>
    public void ShowFallback(string headline, string text) => OnUiThread(() =>
    {
        fallback = (headline, text);
        // Движок не прячем, а сжимаем до точки (скрытие окна движка роняло Miniblink): запасной экран рисует само окно.
        LayoutView();
        Win32.InvalidateRect(Handle, null, 1);
    });

    /// <summary>Убрать запасной экран и вернуть страницу.</summary>
    public void HideFallback() => OnUiThread(() =>
    {
        if (fallback is null)
        {
            return;
        }

        fallback = null;
        LayoutView();
        Win32.InvalidateRect(Handle, null, 1);
    });

    /// <summary>
    /// Встать полосой вдоль верхнего края (appbar, как панель задач): Windows вычитает её из рабочей области, и
    /// развёрнутые окна техника под неё не залезают. Высота — в точках; повторный вызов перестраивает полосу.
    /// </summary>
    public void DockBand(int heightPoints) => OnUiThread(() =>
    {
        bandPixels = Scaled(heightPoints);
        if (!bandDocked)
        {
            bandMessage = Win32.RegisterWindowMessage("AFK4.MaintenanceBand");
            var data = BandData();
            Win32.SHAppBarMessage(Win32.AbmNew, &data);
            bandDocked = true;
        }

        PositionBand();
    });

    /// <summary>Снять полосу: рабочая область Windows возвращается целиком, окно — снова на весь монитор.</summary>
    public void UndockBand() => OnUiThread(() =>
    {
        if (!bandDocked)
        {
            return;
        }

        var data = BandData();
        Win32.SHAppBarMessage(Win32.AbmRemove, &data);
        bandDocked = false;
        bandPixels = 0;
        if (options.Kiosk)
        {
            CoverMonitor();
        }
    });

    public bool IsBandDocked => bandDocked;

    /// <summary>
    /// Окно киоска занимает своё место, что бы ни случилось с ним после <see cref="DockBand"/> и <see cref="UndockBand"/>:
    /// полоса — свой край, иначе весь монитор. Звать из опроса. На стенде Windows 7 x86 (15.11.2026) окно после постановки
    /// в полосу откатывалось на весь экран, а после снятия полосы оставалось полосой — запертый ПК был бы открыт.
    /// </summary>
    public void ReassertLayout() => OnUiThread(() =>
    {
        if (!options.Kiosk || IsMinimized)
        {
            return;
        }

        Win32.Rect rect;
        Win32.GetWindowRect(Handle, &rect);
        if (bandDocked)
        {
            if (rect.Bottom - rect.Top != bandPixels || rect.Top != 0)
            {
                Report($"band moved to {rect.Left},{rect.Top} {rect.Right},{rect.Bottom}; docking again");
                PositionBand();
            }

            return;
        }

        var info = new Win32.MonitorInfo { Size = sizeof(Win32.MonitorInfo) };
        Win32.GetMonitorInfo(Win32.MonitorFromWindow(Handle, Win32.MonitorDefaultToNearest), &info);
        if (rect.Left != info.Monitor.Left || rect.Top != info.Monitor.Top || rect.Right != info.Monitor.Right || rect.Bottom != info.Monitor.Bottom)
        {
            Report($"window moved to {rect.Left},{rect.Top} {rect.Right},{rect.Bottom}; covering the monitor again");
            CoverMonitor();
        }
    });

    private Win32.AppBarData BandData() => new()
    {
        Size = sizeof(Win32.AppBarData),
        Hwnd = Handle,
        CallbackMessage = bandMessage
    };

    private void PositionBand()
    {
        var data = BandData();
        data.Edge = Win32.AbeTop;
        data.Rect = new Win32.Rect { Left = 0, Top = 0, Right = Win32.GetSystemMetrics(Win32.SmCxScreen), Bottom = bandPixels };
        // Windows может сдвинуть полосу под другую панель вдоль того же края — спрашиваем, где можно.
        Win32.SHAppBarMessage(Win32.AbmQueryPos, &data);
        data.Rect.Bottom = data.Rect.Top + bandPixels;
        Win32.SHAppBarMessage(Win32.AbmSetPos, &data);
        Win32.MoveWindow(Handle, data.Rect.Left, data.Rect.Top, data.Rect.Right - data.Rect.Left, data.Rect.Bottom - data.Rect.Top, 1);
    }

    private void PaintFallback()
    {
        var paint = default(Win32.PaintStruct);
        var dc = Win32.BeginPaint(Handle, &paint);
        try
        {
            Win32.Rect area;
            Win32.GetClientRect(Handle, &area);
            Win32.FillRect(dc, &area, background);
            if (fallback is not { } card)
            {
                return;
            }

            Win32.SetBkMode(dc, Win32.Transparent);
            var big = Win32.SegoeUi(Scaled(48), 600);
            var small = Win32.SegoeUi(Scaled(18), 400);
            var old = Win32.SelectObject(dc, big);
            var height = area.Bottom - area.Top;
            var middle = height / 2;
            Win32.SetTextColor(dc, Win32.Colorref(0xE5E7EB));
            var top = new Win32.Rect { Left = area.Left, Top = middle - Scaled(70), Right = area.Right, Bottom = middle };
            DrawText(dc, card.Headline, ref top, Win32.DtCenter | Win32.DtNoPrefix);
            Win32.SelectObject(dc, small);
            Win32.SetTextColor(dc, Win32.Colorref(0x9CA3AF));
            var bottom = new Win32.Rect { Left = area.Left + Scaled(40), Top = middle + Scaled(8), Right = area.Right - Scaled(40), Bottom = height };
            DrawText(dc, card.Text, ref bottom, Win32.DtCenter | Win32.DtWordBreak | Win32.DtNoPrefix);
            Win32.SelectObject(dc, old);
            Win32.DeleteObject(big);
            Win32.DeleteObject(small);
        }
        finally
        {
            Win32.EndPaint(Handle, &paint);
        }
    }

    private static void DrawText(IntPtr dc, string text, ref Win32.Rect rect, uint format)
    {
        fixed (char* chars = text)
        fixed (Win32.Rect* area = &rect)
        {
            Win32.DrawText(dc, chars, text.Length, area, format);
        }
    }

    /// <summary>
    /// Открыть экран: свою папку или сервер разработки. <paramref name="activate"/> = false — окно появляется, но
    /// фокус остаётся у того, кто впереди: киоск перезапустился посреди игры и не должен выдёргивать игрока из неё.
    /// </summary>
    public void Show(bool activate = true, bool loadPage = true)
    {
        if (loadPage)
        {
            Load((options.DevServerUrl ?? new Uri(origin + "index.html")).ToString());
        }

        Win32.ShowWindow(Handle, options.StartMaximized ? Win32.SwMaximize : activate ? Win32.SwShow : Win32.SwShowNoActivate);
        if (activate)
        {
            // Запущенного службой или планировщиком Windows вперёд не пускает: запертый ПК за блокнотом не спрятать.
            if (!IsInFront)
            {
                BringToFront();
            }

            FocusView();
        }
    }

    /// <summary>«Поверх всех» включить или снять, не забирая фокус: игра впереди остаётся впереди.</summary>
    public void SetTopmost(bool onTop) => OnUiThread(() =>
    {
        topmost = onTop;
        Win32.SetWindowPos(Handle, onTop ? Win32.HwndTopmost : Win32.HwndNoTopmost, 0, 0, 0, 0,
            Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate);
    });

    /// <summary>
    /// Вывести окно вперёд с фокусом — «Продлить» поверх игры, блокировка. Windows не даёт фоновому процессу отнять
    /// передний план: поток окна на миг подключается ко вводу того, кто впереди, а не хватило — нажатие Alt, после
    /// него переключиться можно. «Поверх всех» после этого — как было.
    /// </summary>
    public void BringToFront() => OnUiThread(() =>
    {
        if (IsMinimized)
        {
            Win32.ShowWindow(Handle, Win32.SwRestore);
        }

        if (options.Kiosk)
        {
            CoverMonitor();
        }

        var keepTopmost = topmost;
        Win32.SetWindowPos(Handle, Win32.HwndTopmost, 0, 0, 0, 0, Win32.SwpNoMove | Win32.SwpNoSize);
        var front = Win32.GetForegroundWindow();
        var frontThread = front == IntPtr.Zero ? 0 : Win32.GetWindowThreadProcessId(front, IntPtr.Zero);
        var attached = frontThread != 0 && frontThread != nativeUiThreadId && Win32.AttachThreadInput(nativeUiThreadId, frontThread, 1) != 0;
        try
        {
            Win32.BringWindowToTop(Handle);
            Win32.SetForegroundWindow(Handle);
        }
        finally
        {
            if (attached)
            {
                Win32.AttachThreadInput(nativeUiThreadId, frontThread, 0);
            }
        }

        if (!IsInFront)
        {
            Win32.keybd_event(Win32.VkMenu, 0, 0, UIntPtr.Zero);
            Win32.keybd_event(Win32.VkMenu, 0, Win32.KeyEventKeyUp, UIntPtr.Zero);
            Win32.SetForegroundWindow(Handle);
        }

        SetTopmost(keepTopmost);
        FocusView();
    });

    // Клавиатура — окну движка, а не внешнему: иначе поле на экране подсвечено, а набранное пропадает, пока по
    // странице не щёлкнут мышью (стенд Windows 7, 05.11.2026).
    private void FocusView()
    {
        Win32.SetFocus(Miniblink.mbGetHostHWND(view));
        Miniblink.mbSetFocus(view);
    }

    /// <summary>Цикл сообщений Windows до закрытия последнего окна. Возвращает код выхода.</summary>
    public static int RunMessageLoop()
    {
        var message = default(Win32.Msg);
        var result = 0;
        // Не только по WM_QUIT: его, как и прочие сообщения потоку, выбрасывает цикл Miniblink, и после закрытия
        // последнего окна процесс висел без окон (стенды Windows 11 и 7, 05.11.2026).
        while (!lastWindowClosed && (result = Win32.GetMessage(&message, IntPtr.Zero, 0, 0)) > 0)
        {
            Win32.TranslateMessage(&message);
            Win32.DispatchMessage(&message);
        }

        return lastWindowClosed ? 0 : result < 0 ? 1 : (int)message.WParam;
    }

    /// <summary>Послать экрану сообщение: <paramref name="json"/> придёт в <c>event.data</c> уже разобранным.</summary>
    public void PostMessageAsJson(string json)
    {
        // U+2028 и U+2029 допустимы в JSON, но обрывают строку в JS-скрипте.
        var script = "window.__afk4HostDispatch && window.__afk4HostDispatch("
            + json.Replace(LineSeparator, "\\u2028", StringComparison.Ordinal).Replace(ParagraphSeparator, "\\u2029", StringComparison.Ordinal)
            + ");";
        OnUiThread(() => WithUtf8(script, text => Miniblink.mbRunJs(view, Miniblink.mbWebFrameGetMainFrame(view), text, 0, null, IntPtr.Zero, IntPtr.Zero)));
    }

    /// <summary>Работа, поставленная в поток окон (<see cref="RunOnUiThread"/>, продолжения await), упала с исключением.</summary>
    public static event Action<Exception>? UiError;

    /// <summary>Страница загрузилась (главный документ).</summary>
    public event Action? PageLoaded;

    /// <summary>Страница не загрузилась (сбой, а не отмена переходом): киоск показывает запасной экран и пробует снова.</summary>
    public event Action? PageLoadFailed;

    /// <summary>Загрузить экран заново — после сбоя загрузки.</summary>
    public void Reload() => OnUiThread(() => Load((options.DevServerUrl ?? new Uri(origin + "index.html")).ToString()));

    /// <summary>
    /// Выполнить в потоке окон, не дожидаясь: кадры канала агента и события моста приходят из других потоков, а окнами
    /// и движком можно управлять только отсюда.
    /// </summary>
    public static void RunOnUiThread(Action action) => Post(_ => action(), null);

    /// <summary>Окно сейчас закрывают (крестик, Alt+F4, команда программы): последний случай сохранить положение.</summary>
    public event Action? Closing;

    /// <summary>Рабочая область главного экрана без панели задач, пиксели.</summary>
    public static (int Left, int Top, int Width, int Height) PrimaryWorkArea()
    {
        Win32.Rect area;
        Win32.SystemParametersInfo(Win32.SpiGetWorkArea, 0, &area, 0);
        return (area.Left, area.Top, area.Right - area.Left, area.Bottom - area.Top);
    }

    /// <summary>Все экраны вместе, пиксели.</summary>
    public static (int Left, int Top, int Width, int Height) VirtualScreen() => (
        Win32.GetSystemMetrics(Win32.SmXVirtualScreen), Win32.GetSystemMetrics(Win32.SmYVirtualScreen),
        Win32.GetSystemMetrics(Win32.SmCxVirtualScreen), Win32.GetSystemMetrics(Win32.SmCyVirtualScreen));

    /// <summary>Обычное положение окна (куда оно вернётся из развёрнутого) и признак «развёрнуто»: для запоминания между запусками.</summary>
    public ((int X, int Y, int Width, int Height) Bounds, bool Maximized) GetPlacement()
    {
        var placement = new Win32.WindowPlacement { Length = sizeof(Win32.WindowPlacement) };
        Win32.GetWindowPlacement(Handle, &placement);
        var rect = placement.NormalPosition;
        return ((rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top), placement.ShowCmd == Win32.SwMaximize);
    }

    /// <summary>Тянуть край окна без рамки (команда экрана): <c>left</c>, <c>top-right</c> и так далее.</summary>
    public void BeginResize(string? edge) => OnUiThread(() =>
    {
        var direction = edge switch
        {
            "left" => 1, "right" => 2, "top" => 3, "top-left" => 4, "top-right" => 5, "bottom" => 6, "bottom-left" => 7, "bottom-right" => 8, _ => 0
        };
        if (direction == 0)
        {
            return;
        }

        Win32.ReleaseCapture();
        Win32.SendMessage(Handle, Win32.WmSysCommand, (IntPtr)(0xF000 + direction), IntPtr.Zero);
    });

    public void Minimize() => OnUiThread(() => Win32.ShowWindow(Handle, Win32.SwMinimize));

    public void ToggleMaximize() => OnUiThread(() => Win32.ShowWindow(Handle, IsMaximized ? Win32.SwRestore : Win32.SwMaximize));

    public void Close() => OnUiThread(() =>
    {
        closingByProgram = true;
        Win32.SendMessage(Handle, Win32.WmClose, IntPtr.Zero, IntPtr.Zero);
    });

    /// <summary>Перетаскивание окна без рамки за заголовок, который нарисовал экран: зовётся, пока кнопка мыши нажата.</summary>
    public void BeginDrag() => OnUiThread(() =>
    {
        Win32.ReleaseCapture();
        Win32.SendMessage(Handle, Win32.WmNcLButtonDown, Win32.HtCaption, IntPtr.Zero);
    });

    /// <summary>Сообщение вместо экрана — когда сам экран показать нельзя (не собран, не загрузился).</summary>
    public void ShowStatus(string title, string text) => OnUiThread(() =>
    {
        var html = $"""
            <!doctype html><html lang="ru"><head><meta charset="utf-8"><style>
            body{"{"}margin:0;height:100vh;display:grid;place-items:center;background:#0b0f0e;color:#e7ecea;font:15px/1.5 "Segoe UI",sans-serif{"}"}
            main{"{"}max-width:420px;padding:28px;border:1px solid #24302c;border-radius:10px{"}"}
            h1{"{"}font-size:20px;font-weight:600;margin:0 0 12px{"}"}p{"{"}margin:0;color:#a9b5b0{"}"}
            </style></head><body><main><h1>{WebUtility.HtmlEncode(title)}</h1><p>{WebUtility.HtmlEncode(text)}</p></main></body></html>
            """;
        WithUtf8(html, page => WithUtf8(origin + "status", baseUrl => Miniblink.mbLoadHtmlWithBaseUrl(view, page, baseUrl)));
    });

    private void Load(string url) => WithUtf8(url, text => Miniblink.mbLoadURL(view, text));

    private int Scaled(int points) => (int)Math.Round(points * scale);

    private void CenterInWorkArea()
    {
        var work = WorkArea();
        Win32.Rect window;
        Win32.GetWindowRect(Handle, &window);
        // Окно не больше рабочей области: 740 точек при 125% — 925 пикселей, выше экрана 1366×768.
        var width = Math.Min(window.Right - window.Left, work.Right - work.Left);
        var height = Math.Min(window.Bottom - window.Top, work.Bottom - work.Top);
        Win32.MoveWindow(Handle, work.Left + (work.Right - work.Left - width) / 2, work.Top + (work.Bottom - work.Top - height) / 2, width, height, 0);
    }

    // Весь монитор, а не рабочая область: киоск закрывает и панель задач.
    private void CoverMonitor()
    {
        // Полоса сама занимает свой край: на весь монитор её не растягивать.
        if (bandDocked)
        {
            return;
        }

        var info = new Win32.MonitorInfo { Size = sizeof(Win32.MonitorInfo) };
        Win32.GetMonitorInfo(Win32.MonitorFromWindow(Handle, Win32.MonitorDefaultToNearest), &info);
        Win32.SetWindowPos(Handle, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left,
            info.Monitor.Bottom - info.Monitor.Top, Win32.SwpNoZOrder | Win32.SwpNoActivate);
    }

    private Win32.Rect WorkArea()
    {
        var info = new Win32.MonitorInfo { Size = sizeof(Win32.MonitorInfo) };
        Win32.GetMonitorInfo(Win32.MonitorFromWindow(Handle, Win32.MonitorDefaultToNearest), &info);
        return info.Work;
    }

    private void LayoutView()
    {
        if (view == IntPtr.Zero)
        {
            return;
        }

        if (fallback is not null)
        {
            Miniblink.mbMoveWindow(view, 0, 0, 1, 1);
            return;
        }

        Win32.Rect client;
        Win32.GetClientRect(Handle, &client);
        // Без рамки края окна тянет сама Windows: оставляем ей тонкую полосу — над страницей мышь достаётся движку.
        var border = options.Frameless && !IsMaximized ? Scaled(FramelessBorder) : 0;
        Miniblink.mbMoveWindow(view, border, border, Math.Max(1, client.Right - 2 * border), Math.Max(1, client.Bottom - 2 * border));
    }

    private IntPtr HandleMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        // Панель задач переехала или сменилось разрешение — Windows просит полосу встать заново.
        if (bandDocked && message == bandMessage && (int)wParam == Win32.AbnPosChanged)
        {
            PositionBand();
            return IntPtr.Zero;
        }

        switch (message)
        {
            case Win32.WmPaint when fallback is not null:
                PaintFallback();
                return IntPtr.Zero;
            case Win32.WmSize:
                LayoutView();
                MaximizedChanged?.Invoke((int)wParam == Win32.SizeMaximized);
                return IntPtr.Zero;
            case Win32.WmEraseBackground:
                Win32.Rect area;
                Win32.GetClientRect(Handle, &area);
                Win32.FillRect(wParam, &area, background);
                return 1;
            case Win32.WmSetFocus:
                FocusView();
                return IntPtr.Zero;
            case Win32.WmDisplayChange when options.Kiosk:
                // Игра сменила разрешение и вышла: без этого киоск остаётся в её размере, а угол рабочего стола открыт.
                if (bandDocked)
                {
                    PositionBand();
                }
                else
                {
                    CoverMonitor();
                }

                return IntPtr.Zero;
            case Win32.WmSysCommand when options.Kiosk && ((int)wParam & 0xFFF0) == Win32.ScClose:
                // Alt+F4 приходит сюда: киоск закрывает только программа.
                return IntPtr.Zero;
            case Win32.WmClose when options.Kiosk && !closingByProgram:
                return IntPtr.Zero;
            case Win32.WmClose:
                Closing?.Invoke();
                return Win32.DefWindowProc(Handle, message, wParam, lParam);
            case Win32.WmGetMinMaxInfo:
                var limits = (Win32.MinMaxInfo*)lParam;
                // Минимум не больше рабочей области: на экране 800×600 и 1024×768 окно 960×640 иначе уезжает за край
                // вместе с кнопками «свернуть» и «закрыть» (стенд Windows 7, 05.11.2026).
                var work = WorkArea();
                // Киоску минимум не нужен: полоса обслуживания ниже любого «минимального» окна (56 точек).
                limits->MinTrackSize = options.Kiosk
                    ? new Win32.Point { X = 1, Y = 1 }
                    : new Win32.Point
                    {
                        X = Math.Min(Scaled(options.MinWidth), work.Right - work.Left),
                        Y = Math.Min(Scaled(options.MinHeight), work.Bottom - work.Top)
                    };
                if (options.Frameless)
                {
                    // Развёрнутое окно без рамки — ровно рабочая область, а не весь экран поверх панели задач.
                    var info = new Win32.MonitorInfo { Size = sizeof(Win32.MonitorInfo) };
                    Win32.GetMonitorInfo(Win32.MonitorFromWindow(Handle, Win32.MonitorDefaultToNearest), &info);
                    limits->MaxPosition = new Win32.Point { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
                    limits->MaxSize = new Win32.Point { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
                }

                return IntPtr.Zero;
            case Win32.WmNcCalcSize when options.Frameless && wParam != IntPtr.Zero:
                return IntPtr.Zero;
            case Win32.WmNcHitTest when options.Frameless:
                return HitTest(lParam);
            case Win32.WmDestroy:
                Report("window destroyed");
                if (bandDocked)
                {
                    var data = BandData();
                    Win32.SHAppBarMessage(Win32.AbmRemove, &data);
                    bandDocked = false;
                }

                ByWindow.Remove(Handle);
                ByView.TryRemove(view, out _);
                if (ByWindow.Count == 0)
                {
                    lastWindowClosed = true;
                    Win32.PostQuitMessage(0);
                    // Закрытие мог разобрать вложенный цикл Miniblink, пока главный ждёт в GetMessage: WM_QUIT он
                    // выбросит, и будить главный цикл станет некому — процесс висел без окон (Windows 7 x86, AOT).
                    // Таймер приходит, пока очередь пуста, и цикл увидит, что окон больше нет.
                    Win32.SetTimer(dispatcher, 1, 50, IntPtr.Zero);
                }

                return IntPtr.Zero;
            default:
                return Win32.DefWindowProc(Handle, message, wParam, lParam);
        }
    }

    private IntPtr HitTest(IntPtr lParam)
    {
        if (IsMaximized)
        {
            return Win32.HtClient;
        }

        var x = (short)(lParam.ToInt64() & 0xFFFF);
        var y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
        Win32.Rect window;
        Win32.GetWindowRect(Handle, &window);
        var grip = Scaled(ResizeGrip);
        var left = x < window.Left + grip;
        var right = x >= window.Right - grip;
        var top = y < window.Top + grip;
        var bottom = y >= window.Bottom - grip;
        return (top, bottom, left, right) switch
        {
            (true, _, true, _) => Win32.HtTopLeft,
            (true, _, _, true) => Win32.HtTopRight,
            (_, true, true, _) => Win32.HtBottomLeft,
            (_, true, _, true) => Win32.HtBottomRight,
            (true, _, _, _) => Win32.HtTop,
            (_, true, _, _) => Win32.HtBottom,
            (_, _, true, _) => Win32.HtLeft,
            (_, _, _, true) => Win32.HtRight,
            _ => Win32.HtClient
        };
    }

    // Свой «сайт» экрана: файлы из папки, в index.html — мост и настройки до скриптов страницы.
    private bool TryServe(string url, IntPtr job)
    {
        var prefix = origin;
        var root = contentRoot;
        if (!url.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
        {
            var extra = extraRoots.FindIndex(candidate => url.StartsWith(candidate.Origin, StringComparison.OrdinalIgnoreCase));
            if (extra < 0)
            {
                return false;
            }

            (prefix, root) = extraRoots[extra];
        }

        var path = url[prefix.Length..];
        var end = path.IndexOfAny(['?', '#']);
        path = Uri.UnescapeDataString(end < 0 ? path : path[..end]);
        var file = Path.GetFullPath(Path.Combine(root, path.Length == 0 ? "index.html" : path));
        byte[] body;
        string type;
        if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
        {
            body = "Not found"u8.ToArray();
            type = "text/plain";
            Report($"not found: {url}");
        }
        else
        {
            body = File.ReadAllBytes(file);
            type = ContentType(file);
            if (type == "text/html")
            {
                body = WithStartupScripts(body);
            }
        }

        fixed (byte* data = body)
        {
            Miniblink.mbNetSetData(job, data, body.Length);
        }


        // Тип — после данных и ещё заголовком ответа: до данных Miniblink его теряет, и модульный скрипт Vite
        // отвергается («MIME type of ""») — стенд 05.11.2026.
        WithUtf8(type, text => Miniblink.mbNetSetMIMEType(job, text));
        Miniblink.mbNetSetHTTPHeaderField(job, "Content-Type", type, 1);

        return true;
    }

    private byte[] WithStartupScripts(byte[] html)
    {
        var page = Encoding.UTF8.GetString(html);
        var scripts = "<script>" + BridgeScript + options.InitScript + "</script>";
        var head = page.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        return Encoding.UTF8.GetBytes(head < 0 ? scripts + page : page.Insert(head + "<head>".Length, scripts));
    }

    // Тот же вид, что у window.chrome.webview: экраны не знают, в каком движке они живут.
    private const string BridgeScript =
        "(function () { var listeners = [];" +
        " window.afk4Host = {" +
        "  postMessage: function (message) { window.mbQuery(" + "16710" + ", JSON.stringify(message), function () {}); }," +
        "  addEventListener: function (type, listener) { if (type === 'message') listeners.push(listener); }," +
        "  removeEventListener: function (type, listener) { var at = listeners.indexOf(listener); if (at >= 0) listeners.splice(at, 1); }" +
        " };" +
        " window.__afk4HostDispatch = function (data) { listeners.slice().forEach(function (listener) {" +
        "  try { listener({ data: data }); } catch (error) { setTimeout(function () { throw error; }); } }); };" +
        "})();";

    private bool IsAllowed(string url) =>
        url.StartsWith(origin, StringComparison.OrdinalIgnoreCase)
        || extraRoots.Exists(candidate => url.StartsWith(candidate.Origin, StringComparison.OrdinalIgnoreCase))
        || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        || (options.DevServerUrl is { } dev && url.StartsWith(dev.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));

    private void Report(string text) => Diagnostic?.Invoke(text);

    private static string ContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html",
        ".js" or ".mjs" => "text/javascript",
        ".css" => "text/css",
        ".json" or ".map" => "application/json",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".ico" => "image/x-icon",
        ".woff2" => "font/woff2",
        ".woff" => "font/woff",
        ".ttf" => "font/ttf",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        _ => "application/octet-stream"
    };

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (hwnd == dispatcher && message == InvokeMessage)
            {
                RunPending();
                return IntPtr.Zero;
            }

            if (hwnd == dispatcher && message == Win32.WmTimer && (uint)wParam == TickTimerId)
            {
                tick?.Invoke();
                return IntPtr.Zero;
            }

            return ByWindow.TryGetValue(hwnd, out var window)
                ? window.HandleMessage(message, wParam, lParam)
                : Win32.DefWindowProc(hwnd, message, wParam, lParam);
        }
        catch (Exception exception)
        {
            ByWindow.GetValueOrDefault(hwnd)?.Report("window: " + exception);
            return Win32.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnQuery(IntPtr view, IntPtr param, IntPtr state, long queryId, int customMessage, byte* request)
    {
        fixed (byte* empty = "\0"u8)
        {
            Miniblink.mbResponseQuery(view, queryId, customMessage, empty);
        }

        if (customMessage != BridgeMessage || !ByView.TryGetValue(view, out var window))
        {
            return;
        }

        try
        {
            window.MessageReceived?.Invoke(Marshal.PtrToStringUTF8((IntPtr)request) ?? "null");
        }
        catch (Exception exception)
        {
            window.Report("bridge: " + exception);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnLoadBegin(IntPtr view, IntPtr param, byte* url, IntPtr job)
    {
        try
        {
            if (!ByView.TryGetValue(view, out var window))
            {
                return 0;
            }

            var address = Marshal.PtrToStringUTF8((IntPtr)url)!;
            if (window.TryServe(address, job))
            {
                return 1;
            }

            if (window.options.RequestAuthorization?.Invoke(address) is { } authorization)
            {
                Miniblink.mbNetSetHTTPHeaderField(job, "Authorization", authorization, 0);
            }

            return 0;
        }
        catch (Exception exception)
        {
            ByView.GetValueOrDefault(view)?.Report("content: " + exception);
            Miniblink.mbNetCancelRequest(job);
            return 1;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnNavigation(IntPtr view, IntPtr param, int navigationType, byte* url)
    {
        var address = Marshal.PtrToStringUTF8((IntPtr)url) ?? string.Empty;
        if (!ByView.TryGetValue(view, out var window) || window.IsAllowed(address))
        {
            return 1;
        }

        window.Report("navigation blocked: " + address);
        if (window.options.OpenExternalLinks
            && (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("tg://", StringComparison.OrdinalIgnoreCase)))
        {
            Win32.ShellExecute(window.Handle, "open", address, null, null, Win32.SwShow);
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnLoadingFinish(IntPtr view, IntPtr param, IntPtr frame, byte* url, int result, byte* reason)
    {
        if (!ByView.TryGetValue(view, out var window))
        {
            return;
        }

        if (result == 0)
        {
            RunOnUiThread(() => window.PageLoaded?.Invoke());
            return;
        }

        window.Report($"load failed ({result}): {Marshal.PtrToStringUTF8((IntPtr)url)} {Marshal.PtrToStringUTF8((IntPtr)reason)}");
        // 1 — сбой загрузки; остальное (отмена переходом, блокировка чужого адреса) — не повод закрывать экран.
        if (result == 1)
        {
            RunOnUiThread(() => window.PageLoadFailed?.Invoke());
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnConsole(IntPtr view, IntPtr param, int level, byte* message, byte* source, uint line, byte* stack)
    {
        // Только предупреждения и ошибки страницы (mbLevelWarning, mbLevelError, mbLevelRevokedError): на уровне
        // «log» Miniblink сам пишет каждый вызов моста.
        if (level is 2 or 3 or 6 && ByView.TryGetValue(view, out var window))
        {
            window.Report($"console {level}: {Marshal.PtrToStringUTF8((IntPtr)message)} @ {Marshal.PtrToStringUTF8((IntPtr)source)}:{line}");
        }
    }

    private static void OnUiThread(Action action)
    {
        if (Environment.CurrentManagedThreadId == uiThreadId)
        {
            action();
        }
        else
        {
            Post(_ => action(), null);
        }
    }

    private static void Post(SendOrPostCallback callback, object? state)
    {
        Pending.Enqueue((callback, state));
        Win32.PostMessage(dispatcher, InvokeMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private static void RunPending()
    {
        while (Pending.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            catch (Exception exception)
            {
                // Одна упавшая работа не должна ронять остальные, но и пропадать бесследно ей нельзя.
                UiError?.Invoke(exception);
            }
        }
    }

    private delegate void Utf8Action(byte* text);

    private static void WithUtf8(string text, Utf8Action action)
    {
        var bytes = Encoding.UTF8.GetBytes(text + "\0");
        fixed (byte* pointer = bytes)
        {
            action(pointer);
        }
    }

    private sealed class UiContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => WebHostWindow.Post(callback, state);

        public override void Send(SendOrPostCallback callback, object? state)
        {
            if (Environment.CurrentManagedThreadId == uiThreadId)
            {
                callback(state);
                return;
            }

            using var done = new ManualResetEventSlim();
            WebHostWindow.Post(_ => { callback(state); done.Set(); }, null);
            done.Wait();
        }

        public override SynchronizationContext CreateCopy() => this;
    }
}
