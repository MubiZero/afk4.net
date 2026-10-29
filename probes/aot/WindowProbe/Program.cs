// Проба Native AOT (docs/roadmap/windows7.md): окно ПК клуба на WebView2 без WPF и без .NET рантайма. Простое окно
// Windows (DirectNAot) с WebView2 через WebView2Aot; экран входа из ресурса, мост chrome.webview.postMessage ↔ C#.
using System.Reflection;
using System.Runtime.InteropServices;
using DirectN;
using DirectN.Extensions.Com;
using DirectN.Extensions.Utilities;
using WebView2;
using WebView2.Utilities;

namespace WindowProbe;

internal static class Program
{
    [STAThread] // WebView2 работает только в STA
    private static void Main()
    {
        using var app = new Application();
        using var window = new ProbeWindow();
        window.ResizeClient(1200, 760);
        window.Center();
        window.Show();
        window.SetForeground();
        app.Run();
    }
}

internal sealed class ProbeWindow : Window
{
    private IComObject<ICoreWebView2Controller>? controller;
    private CoreWebView2Events? events;
    private readonly string? browserVersion;

    public ProbeWindow() : base($"AFK4 AOT {RuntimeInformation.ProcessArchitecture}")
    {
        WebView2Utilities.Initialize(Assembly.GetEntryAssembly());
        browserVersion = WebView2Utilities.GetAvailableCoreWebView2BrowserVersionString();
        Text = $"{Text} · WebView2 {browserVersion ?? "не найден"}";
        InitializeWebView();
    }

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "afk4-windowprobe.log");

    private static void Log(string text) => File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {text}{Environment.NewLine}");

    private async void InitializeWebView()
    {
        try
        {
            await InitializeWebViewCore();
        }
        catch (Exception exception)
        {
            Log("FAIL " + exception);
        }
    }

    private async Task InitializeWebViewCore()
    {
        Log("start");
        using var environment = await WebView2.Functions.CreateCoreWebView2EnvironmentWithOptionsAsync(null, WebView2Utilities.GetDefaultUserDataFolder(), null)
            ?? throw new InvalidOperationException("WebView2 environment");
        controller = await environment.CreateCoreWebView2ControllerAsync(Handle) ?? throw new InvalidOperationException("WebView2 controller");
        controller.Bounds = ClientRect;
        Log($"controller bounds {ClientRect.left},{ClientRect.top},{ClientRect.right},{ClientRect.bottom}");
        var webView = controller.CoreWebView2 ?? throw new InvalidOperationException("CoreWebView2");

        events = new CoreWebView2Events(webView.Object);
        events.WebMessageReceived += (_, args) =>
        {
            args.TryGetWebMessageAsString(out var message);
            var phone = message.ToStringAndDispose();
            var reply = $"{{\"answer\":\"C# AOT принял вход {phone} в {DateTime.Now:HH:mm:ss}\"}}";
            var json = PWSTR.From(reply);
            try { webView.Object.PostWebMessageAsJson(json); } finally { PWSTR.Dispose(ref json); }
        };

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("login.html")!;
        var html = new StreamReader(stream).ReadToEnd()
            .Replace("{{info}}", $"{RuntimeInformation.OSDescription} · {RuntimeInformation.ProcessArchitecture} · AOT · WebView2 {browserVersion}");
        Log($"html {html.Length} chars");
        var page = PWSTR.From(html);
        try { Log("navigate hr " + webView.Object.NavigateToString(page)); } finally { PWSTR.Dispose(ref page); }
    }

    protected override bool OnResized(WindowResizedType type, SIZE size)
    {
        if (controller != null)
        {
            controller.Bounds = ClientRect;
        }

        return base.OnResized(type, size);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            events?.Dispose();
            var current = Interlocked.Exchange(ref controller, null);
            current?.Object.Close();
            current?.Dispose();
        }

        base.Dispose(disposing);
    }
}
