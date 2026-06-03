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
        base.OnStartup(e);

        // One localization service shared by the window (for {loc:T} XAML literals via the
        // ambient scope) and the view-model. The active locale follows the branch locale
        // carried on the player-shell state (see PlayerShellViewModel.ApplyState).
        var localization = LocalizationService.LoadEmbedded(Locales.Default);
        LocalizationScope.Current = localization;

        var window = new MainWindow(localization);
        window.Show();
    }
}
