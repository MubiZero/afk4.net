using AFK4.Localization;
using AFK4.Player.Shell;
using AFK4.Player.Shell.Web;
using AFK4.WebHost;

return PlayerShellProgram.Run();

internal static class PlayerShellProgram
{
    /// <summary>
    /// Одна оболочка на сессию. В киоске её запускает Windows как оболочку учётки игрока, а
    /// супервизор агента поднимает её же, если не видит, — на входе они могут успеть оба. Вторая
    /// копия дралась бы с первой за канал агента (он пускает один экземпляр) и за экран.
    /// </summary>
    private const string SingleInstanceName = @"Local\AFK4.PlayerShell.Host";

    [STAThread]
    public static int Run()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, SingleInstanceName, out var first);
        if (!first)
        {
            PlayerShellStartupLog.Write("Another Player Shell already runs in this session; this copy exits.");
            return 0;
        }

        // Последняя страховка: сбой, который не поймал ни один обработчик, иначе роняет киоск без следа, и агент
        // перезапускает его по кругу. Пишем в журнал; сами окна и мост ловят свои сбои.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            PlayerShellStartupLog.Write("Unhandled exception in Player Shell.", args.ExceptionObject as Exception);

        try
        {
            LocalizationScope.Current = LocalizationService.LoadEmbedded(Locales.Default);
            WebHostWindow.UiError += exception => PlayerShellStartupLog.Write("Player Shell UI work failed.", exception);
            WebHostWindow.Initialize();
            using var host = new PlayerShellHost();
            host.Start();
            var exitCode = WebHostWindow.RunMessageLoop();
            PlayerShellStartupLog.Write($"Player Shell window closed, exit code {exitCode}.");
            return exitCode;
        }
        catch (Exception exception)
        {
            PlayerShellStartupLog.Write("Player Shell failed to start.", exception);
            return 1;
        }
    }
}
