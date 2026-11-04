// Проба Miniblink (Chromium одной DLL, github.com/weolar/miniblink49) вместо WebView2: .NET 10 Native AOT + mb132,
// Windows 7–11, x86 и x64. Открывает адрес из командной строки, сам проверяет мост страница ↔ C# (mbQuery) и пишет
// журнал и память в %TEMP%\afk4-blinkprobe.log. DLL (mb132_x32.dll / mb132_x64.dll) кладётся рядом с exe.
//   BlinkProbe.exe https://demo.afk4.net/shell/
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

static unsafe class Program
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "afk4-blinkprobe.log");
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    private static void Log(string text) =>
        File.AppendAllText(LogPath, $"{Clock.ElapsedMilliseconds,7} ms  {text}{Environment.NewLine}");

    [STAThread]
    private static void Main(string[] args)
    {
        File.WriteAllText(LogPath, string.Empty);
        var url = args.FirstOrDefault() ?? "https://demo.afk4.net/shell/";
        Log($"start {Environment.OSVersion} {RuntimeInformation.ProcessArchitecture} AOT {!RuntimeFeature.IsDynamicCodeSupported} url {url}");

        NativeLibrary.SetDllImportResolver(typeof(Program).Assembly, (name, assembly, path) =>
            name == "miniblink"
                ? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, Environment.Is64BitProcess ? "mb132_x64.dll" : "mb132_x32.dll"))
                : IntPtr.Zero);

        try
        {
            // Окно в масштабе Windows (125–150% на ноутбуках и больших мониторах): иначе страница рисуется в 100%,
            // занимает часть окна, а нажатия уходят мимо.
            mbInit(null);
            mbEnableHighDPISupport();
            Log("init");
            var view = mbCreateWebWindow(0, IntPtr.Zero, 0, 0, 1100, 700);
            Log($"view {view}");
            mbOnJsQuery(view, &OnQuery, IntPtr.Zero);
            mbOnDocumentReady(view, &OnReady, IntPtr.Zero);
            mbOnTitleChanged(view, &OnTitle, IntPtr.Zero);
            mbOnDestroy(view, &OnDestroy, IntPtr.Zero);
            mbOnLoadUrlBegin(view, &OnLoadBegin, IntPtr.Zero);
            mbOnLoadUrlEnd(view, &OnLoadEnd, IntPtr.Zero);
            PageUrl = url;
            Call(url, text => mbLoadURL(view, text));
            mbMoveToCenter(view);
            mbShowWindow(view, 1);
            new Thread(ReportMemory) { IsBackground = true }.Start();
            mbRunMessageLoop();
            Log("loop ended");
        }
        catch (Exception exception)
        {
            Log("fail " + exception);
        }
    }

    private static void ReportMemory()
    {
        for (var i = 0; i < 8; i++)
        {
            Thread.Sleep(5000);
            using var self = Process.GetCurrentProcess();
            Log($"memory private {self.PrivateMemorySize64 / 1048576} MB, working set {self.WorkingSet64 / 1048576} MB, threads {self.Threads.Count}");
        }
    }

    // Мост сам себя проверяет: страница зовёт mbQuery, C# отвечает, ответ попадает в заголовок окна.
    private const string BridgeCheck =
        "var r = []; function t(n, f) { try { r.push(n + '=' + f()); } catch (e) { r.push(n + ' err ' + e.message); } }" +
        "t('lang', function () { return navigator.language + '/' + navigator.languages; });" +
        "t('default', function () { return Intl.DateTimeFormat().resolvedOptions().locale; });" +
        "t('ru num', function () { return new Intl.NumberFormat('ru-RU').format(1480.5); });" +
        "t('ru date', function () { return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', weekday: 'short' }).format(new Date(2026, 10, 6)); });" +
        "t('tg date', function () { return new Intl.DateTimeFormat('tg', { month: 'long' }).format(new Date(2026, 10, 6)); });" +
        "t('supported', function () { return Intl.DateTimeFormat.supportedLocalesOf(['ru', 'ru-RU', 'tg', 'en']); });" +
        "t('plural', function () { return new Intl.PluralRules('ru').select(5); });" +
        "t('rel', function () { return new Intl.RelativeTimeFormat('ru').format(-1, 'day'); });" +
        "t('collator', function () { return ['б', 'а', 'я'].sort(new Intl.Collator('ru').compare); });" +
        "window.mbQuery(7, r.join(' ; '), function (code, answer) { document.title = 'bridge: ' + answer; });";

    // В Miniblink window.chrome — неизменяемый геттер, который каждый раз отдаёт новый объект: ни заменить его, ни
    // дописать в него webview нельзя. Настоящая программа подставит мост под своим именем; проба, чтобы не трогать
    // код экранов, на лету меняет в скриптах страницы window.chrome на это имя.
    private const string BridgeName = "window.afk4chrome";

    private static string PageUrl = string.Empty;
    private static readonly List<byte[]> PatchedBodies = [];

    private static bool IsPageScript(string url) =>
        url.StartsWith(PageUrl, StringComparison.Ordinal) && url.EndsWith(".js", StringComparison.Ordinal);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnLoadBegin(IntPtr view, IntPtr param, byte* url, IntPtr job)
    {
        if (IsPageScript(Marshal.PtrToStringUTF8((IntPtr)url)!))
        {
            mbNetHookRequest(job);
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnLoadEnd(IntPtr view, IntPtr param, byte* url, IntPtr job, byte* buffer, int length)
    {
        var address = Marshal.PtrToStringUTF8((IntPtr)url)!;
        if (!IsPageScript(address))
        {
            return;
        }

        var script = Encoding.UTF8.GetString(buffer, length);
        var body = Encoding.UTF8.GetBytes(script.Replace("window.chrome", BridgeName, StringComparison.Ordinal));
        PatchedBodies.Add(body);
        fixed (byte* data = body)
        {
            mbNetSetData(job, data, body.Length);
        }

        Log($"patched {address[(address.LastIndexOf('/') + 1)..]} {length} -> {body.Length} bytes");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnConsole(IntPtr view, IntPtr param, int level, byte* message, byte* source, uint line, byte* stack) =>
        Log($"console {level}: {Marshal.PtrToStringUTF8((IntPtr)message)} @ {Marshal.PtrToStringUTF8((IntPtr)source)}:{line}");

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnReady(IntPtr view, IntPtr param, IntPtr frame)
    {
        Log("document ready");
        Call(BridgeCheck, text => mbRunJs(view, mbWebFrameGetMainFrame(view), text, 1, null, IntPtr.Zero, IntPtr.Zero));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnQuery(IntPtr view, IntPtr param, IntPtr state, long queryId, int customMessage, byte* request)
    {
        var text = Marshal.PtrToStringUTF8((IntPtr)request);
        Log($"query {customMessage}: {text}");
        Call($"C# AOT ответил в {DateTime.Now:HH:mm:ss}", answer => mbResponseQuery(view, queryId, customMessage, answer));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnTitle(IntPtr view, IntPtr param, byte* title) =>
        Log("title " + Marshal.PtrToStringUTF8((IntPtr)title));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnDestroy(IntPtr view, IntPtr param, IntPtr unused)
    {
        mbExitMessageLoop();
        return 1;
    }

    private delegate void Utf8Action(byte* text);

    private static void Call(string text, Utf8Action action)
    {
        var bytes = Encoding.UTF8.GetBytes(text + "\0");
        fixed (byte* pointer = bytes)
        {
            action(pointer);
        }
    }

    private const string Mb = "miniblink";

    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbInit(void* settings);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbEnableHighDPISupport();
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern IntPtr mbCreateWebWindow(int type, IntPtr parent, int x, int y, int width, int height);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbLoadURL(IntPtr view, byte* url);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbMoveToCenter(IntPtr view);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbShowWindow(IntPtr view, int show);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbRunMessageLoop();
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbExitMessageLoop();
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern IntPtr mbWebFrameGetMainFrame(IntPtr view);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbRunJs(IntPtr view, IntPtr frame, byte* script, int isInClosure, void* callback, IntPtr param, IntPtr unused);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbOnConsole(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, int, byte*, byte*, uint, byte*, void> callback, IntPtr param);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbOnLoadUrlBegin(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, byte*, IntPtr, int> callback, IntPtr param);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbOnLoadUrlEnd(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, byte*, IntPtr, byte*, int, void> callback, IntPtr param);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbNetHookRequest(IntPtr job);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbNetSetData(IntPtr job, byte* data, int length);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbResponseQuery(IntPtr view, long queryId, int customMessage, byte* response);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbOnJsQuery(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, long, int, byte*, void> callback, IntPtr param);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbOnDocumentReady(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, void> callback, IntPtr param);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern void mbOnTitleChanged(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, byte*, void> callback, IntPtr param);
    [DllImport(Mb, CallingConvention = CallingConvention.StdCall)] private static extern int mbOnDestroy(IntPtr view, delegate* unmanaged[Stdcall]<IntPtr, IntPtr, IntPtr, int> callback, IntPtr param);
}
