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
    private static double scale = 1;

    private readonly WebHostOptions options;
    private readonly string origin;
    private readonly string contentRoot;
    private readonly IntPtr background;
    private IntPtr view;

    /// <summary>Сообщение экрана — JSON того, что он передал в <c>postMessage</c>.</summary>
    public event Action<string>? MessageReceived;

    /// <summary>Окно развернули или вернули: экран без рамки рисует кнопку по этому состоянию.</summary>
    public event Action<bool>? MaximizedChanged;

    /// <summary>Диагностика движка: ошибки загрузки, консоль страницы, отклонённые адреса.</summary>
    public event Action<string>? Diagnostic;

    public IntPtr Handle { get; private set; }

    public bool IsMaximized => Win32.IsZoomed(Handle) != 0;

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
        Directory.CreateDirectory(options.DataFolder);
        // Windows ждёт цвет как 0x00BBGGRR.
        var rgb = options.BackgroundRgb;
        background = Win32.CreateSolidBrush(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));

        var style = Win32.WsClipChildren | (options.Frameless
            ? Win32.WsPopup | Win32.WsThickFrame | Win32.WsSysMenu | Win32.WsMinimizeBox | Win32.WsMaximizeBox
            : Win32.WsOverlappedWindow);
        Handle = Win32.CreateWindowEx(0, WindowClass, options.Title, style, Win32.CwUseDefault, Win32.CwUseDefault,
            Scaled(options.Width), Scaled(options.Height), IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandle(IntPtr.Zero), IntPtr.Zero);
        if (Handle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastPInvokeError()}");
        }

        ByWindow[Handle] = this;
        CenterInWorkArea();

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

        SynchronizationContext.SetSynchronizationContext(new UiContext());
    }

    /// <summary>Открыть экран: свою папку или сервер разработки.</summary>
    public void Show()
    {
        Load((options.DevServerUrl ?? new Uri(origin + "index.html")).ToString());
        Win32.ShowWindow(Handle, Win32.SwShow);
        Miniblink.mbSetFocus(view);
    }

    /// <summary>Цикл сообщений Windows до закрытия последнего окна. Возвращает код выхода.</summary>
    public static int RunMessageLoop()
    {
        Win32.Msg message;
        int result;
        while ((result = Win32.GetMessage(&message, IntPtr.Zero, 0, 0)) > 0)
        {
            if (message.Hwnd == IntPtr.Zero && message.Message == InvokeMessage)
            {
                RunPending();
                continue;
            }

            Win32.TranslateMessage(&message);
            Win32.DispatchMessage(&message);
        }

        return result < 0 ? 1 : (int)message.WParam;
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

    public void Minimize() => OnUiThread(() => Win32.ShowWindow(Handle, Win32.SwMinimize));

    public void ToggleMaximize() => OnUiThread(() => Win32.ShowWindow(Handle, IsMaximized ? Win32.SwRestore : Win32.SwMaximize));

    public void Close() => OnUiThread(() => Win32.SendMessage(Handle, Win32.WmClose, IntPtr.Zero, IntPtr.Zero));

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

        Win32.Rect client;
        Win32.GetClientRect(Handle, &client);
        // Без рамки края окна тянет сама Windows: оставляем ей тонкую полосу — над страницей мышь достаётся движку.
        var border = options.Frameless && !IsMaximized ? Scaled(FramelessBorder) : 0;
        Miniblink.mbMoveWindow(view, border, border, Math.Max(1, client.Right - 2 * border), Math.Max(1, client.Bottom - 2 * border));
    }

    private IntPtr HandleMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
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
                Miniblink.mbSetFocus(view);
                return IntPtr.Zero;
            case Win32.WmGetMinMaxInfo:
                var limits = (Win32.MinMaxInfo*)lParam;
                limits->MinTrackSize = new Win32.Point { X = Scaled(options.MinWidth), Y = Scaled(options.MinHeight) };
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
                ByWindow.Remove(Handle);
                ByView.TryRemove(view, out _);
                if (ByWindow.Count == 0)
                {
                    Win32.PostQuitMessage(0);
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
        if (!url.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = url[origin.Length..];
        var end = path.IndexOfAny(['?', '#']);
        path = Uri.UnescapeDataString(end < 0 ? path : path[..end]);
        var file = Path.GetFullPath(Path.Combine(contentRoot, path.Length == 0 ? "index.html" : path));
        byte[] body;
        string type;
        if (!file.StartsWith(contentRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(file))
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
            return ByView.TryGetValue(view, out var window) && window.TryServe(Marshal.PtrToStringUTF8((IntPtr)url)!, job) ? 1 : 0;
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
        if (address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("tg://", StringComparison.OrdinalIgnoreCase))
        {
            Win32.ShellExecute(window.Handle, "open", address, null, null, Win32.SwShow);
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnLoadingFinish(IntPtr view, IntPtr param, IntPtr frame, byte* url, int result, byte* reason)
    {
        if (result != 0 && ByView.TryGetValue(view, out var window))
        {
            window.Report($"load failed ({result}): {Marshal.PtrToStringUTF8((IntPtr)url)} {Marshal.PtrToStringUTF8((IntPtr)reason)}");
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
        Win32.PostThreadMessage(nativeUiThreadId, InvokeMessage, IntPtr.Zero, IntPtr.Zero);
    }

    private static void RunPending()
    {
        while (Pending.TryDequeue(out var item))
        {
            item.Callback(item.State);
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
