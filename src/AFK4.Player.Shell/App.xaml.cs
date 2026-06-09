using System.Windows;
using AFK4.Localization;

namespace AFK4.Player.Shell;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // One localization service shared by the window (for {loc:T} XAML literals via the
        // ambient scope) and the view-model. The active locale follows the branch locale
        // carried on the player-shell state (see PlayerShellViewModel.ApplyState).
        var localization = LocalizationService.LoadEmbedded(Locales.Default);
        LocalizationScope.Current = localization;

#if DEBUG
        if (e.Args.Contains("--preview"))
        {
            var preview = Preview.PreviewPlayerShell.Create(localization);
            new MainWindow(localization, preview).Show();
            base.OnStartup(e);
            return;
        }
#endif

        var window = new AFK4.Player.Shell.Web.WebViewPlayerWindow();
        window.Show();
        base.OnStartup(e);
    }
}
