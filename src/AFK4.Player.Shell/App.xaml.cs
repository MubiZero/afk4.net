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
        var localization = LocalizationService.LoadEmbedded(Locales.Default);
        LocalizationScope.Current = localization;

        new AFK4.Player.Shell.Web.WebViewPlayerWindow().Show();
        base.OnStartup(e);
    }
}
